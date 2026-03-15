using System;
using System.Collections.Generic;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Features;
using NetRewind.Utils.Input;
using NetRewind.Utils.Sync;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetRewind
{
    public abstract class NetObject : NetworkBehaviour
    {
        #region Variables
        #if Server
        private static List<RollbackInfo> _rollbackInfos = new List<RollbackInfo>();
        #endif
        
        private static NetObject _globalInputDataSource; // The NetObject, that sends the player input data. static because there can only be one!

        public static Dictionary<ulong, NetObject> NetworkObjects = new Dictionary<ulong, NetObject>();
        #if Server
        public static Dictionary<SendingMode, List<NetObject>> StateSendingList = new Dictionary<SendingMode, List<NetObject>>();
        /// <summary>
        /// Don't use this!
        /// </summary>
        public Dictionary<NetObject, uint> InteractedNetObjects = new Dictionary<NetObject, uint>();
        #endif

        // Getter
        public bool IsPredicted => isPredicted;
        #if Server
        public SendingMode StateSendingMode => NetObjectSyncGroup?.SendingMode ?? privateStateSendingMode;
        #endif
        [HideInInspector] public SendingMode privateStateSendingMode; // The sending mode this object should normally be in

        #if Server
        [HideInInspector] public NetObjectSyncGroup NetObjectSyncGroup;
        #endif

        public bool SyncVisual { get; private set; } = true;

        [Header("Networking")]
        [SerializeField] private bool shouldPredict = true; // Default value;
        #if UNITY_EDITOR
        [ShowOnly]
        #endif
        [SerializeField] private bool isPredicted;
        [SerializeField] private SendingMode initialSendingMode = SendingMode.Full; // The sending mode this object starts in.
        public Transform visual;
        [Space(10)]

        private CircularBuffer<ObjectState> _states = new CircularBuffer<ObjectState>(SnapshotContainer.SnapshotBufferSize);

        #if Server
        private List<Event> _events = new List<Event>();
        private Dictionary<uint, uint> _eventCounts = new Dictionary<uint, uint>();
        #endif

        #if Client
        private uint _lastReceivedStateTick;
        private uint _firstRecordedState;
        #endif

        #if Server
        private uint _tickToLeaveGroup = uint.MaxValue;
        #endif

        public bool IsInputOwner => _inputOwnerClientId == NetworkManager.Singleton.LocalClientId;
        public ushort InputOwnerClientId => _inputOwnerClientId;
        private ushort _inputOwnerClientId = ushort.MaxValue;

        public bool HasInputOwner => _inputOwnerClientId != ushort.MaxValue;

        private Vector3 _visualVelocity;
        
        private byte[] InputData { get; set; }
        private IData Data { get; set; }
        private uint TickOfTheInput { get; set; }
        
        [ShowOnly] 
        public bool queuedToBeDestroyed;
        
        #if Server
        private uint _tickToDestroyThisObject = uint.MaxValue;
        #endif
        #endregion

        #region Inspector
        private void OnValidate()
        {
            isPredicted = shouldPredict;
        }
        #endregion

        #region OnNetworkSpawn
        public override void OnNetworkSpawn()
        {
            NetworkObjects.Add(NetworkObjectId, this);
            queuedToBeDestroyed = false;

            if (visual)
            {
                visual.SetParent(null);
                visual.name = name + "[" + NetworkObjectId + "] Visual";
            }
            else
            {
                // There is no visual to sync!
                SetVisualSyncMode(false);
            }

            isPredicted = shouldPredict;
            privateStateSendingMode = initialSendingMode;
            
            #if Server
            if (IsServer)
            {
                StateSendingList.TryAdd(StateSendingMode, new List<NetObject>());
                StateSendingList[StateSendingMode].Add(this);
            }
            #endif
            
            NetSpawn();
        }
        #endregion

        #region OnNetworkDespawn
        public override void OnNetworkDespawn()
        {
            NetworkObjects.Remove(NetworkObjectId);
            
            #if Server
            if (NetObjectSyncGroup != null)
                LeaveSyncGroup();
            
            if (IsServer)
            {
                if (StateSendingList.TryGetValue(StateSendingMode, out List<NetObject> list))
                {
                    list.Remove(this);
                    if (list.Count == 0)
                        StateSendingList.Remove(StateSendingMode);
                }
            }            
            #endif

            // If we are the global input data source, don't be it. : )
            if (_globalInputDataSource == this)
                _globalInputDataSource = null;

            if (visual)
                Destroy(visual.gameObject);
            
            NetDespawn();
        }
        #endregion
        
        #region Global Input Data Source

        private bool CouldDeliverInputDataSource()
        {
            try
            {
                OnGetInputDataToSend();
                
                // The method was overwritten without an error, so we are capable to deliver input data source
                return true;
            }
            catch (NotImplementedException e)
            {
                // The method wasn't overwritten
                return false;
            }
        }
        
        #endregion
        
        #region Update
        private void Update()
        {
            #if Client
            if (Simulation.IsCorrectingGameState)
                return;
            #endif
            
            if (visual && SyncVisual)
            {
                visual.position = Vector3.SmoothDamp(
                    visual.position,
                    transform.position,
                    ref _visualVelocity,
                    NetRewind.Simulation.TimeBetweenTicks // seconds
                );
                
                visual.rotation = transform.rotation;
            }
            
            NetUpdate();
        }
        #endregion

        #region Input Owner

        public void SetInputOwner(ulong newInputOwnerClientId)
        {
            if (newInputOwnerClientId > ushort.MaxValue)
                throw new Exception("Input owner client id is too big! ushort is supported, but your ulong is too big to cast to ushort.");

            SetInputOwner((ushort) newInputOwnerClientId);
        }
        
        public void SetInputOwner(ushort newInputOwnerClientId)
        {
            if (InputOwnerClientId == newInputOwnerClientId)
                return; // Nothing changed!
         
            bool wasTheOwner = IsInputOwner;
            bool wouldBeInputOwner = newInputOwnerClientId == NetworkManager.Singleton.LocalClientId;
            bool needToUpdate = wasTheOwner != wouldBeInputOwner;
            
            _inputOwnerClientId = newInputOwnerClientId; // Update variable
            
            if (needToUpdate)
                HandleInputOwnerUpdate();
        }
        
        public void RemoveInputOwner() => SetInputOwner(ushort.MaxValue);
        
        private void HandleInputOwnerUpdate()
        {
            if (IsInputOwner)
            {
                // -> Just became input owner
                
                // Set the global input data source, if the net object has one.
                if (_globalInputDataSource == null && CouldDeliverInputDataSource())
                    _globalInputDataSource = this;
            }
            else
            {
                // -> We aren't the input owner anymore.
                
                // Reset the global input data source, if it was the one.
                if (_globalInputDataSource == this)
                    _globalInputDataSource = null;
            }
        }
        #endregion

        #region Apply/Update State
        
        #region Apply State
        public static void TryApplyState(ulong networkId, IState state, NetObjectState netObjectState, bool playEvents)
        {
            try
            {
                NetObject obj = NetworkObjects[networkId];
                obj.ApplyNetObjectState(netObjectState);
                obj.ApplyState(state);
                #if Client
                if (!obj.IsServer && playEvents) // Don't do this as the server / host.
                    obj.PlayEvents(netObjectState);
                #endif
            }
            catch (Exception e)
            {
                throw new Exception("Failed to apply state: " + e);
            }
        }
        public void TryApplyState(IState state, NetObjectState netObjectState, bool playEvents) => TryApplyState(NetworkObjectId, state, netObjectState, playEvents);
        #endregion
        
        #region Update State
        #if Client
        private static void TryUpdateState(ulong networkId, IState state, NetObjectState netObjectState, bool playEvents)
        {
            try
            {
                NetObject obj = NetworkObjects[networkId];
                obj.ApplyNetObjectState(netObjectState);
                obj.UpdateState(state);
                if (!obj.IsServer) // Don't do this as the server / host.
                    obj.PlayEvents(netObjectState);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to update state: " + e);
            }
        }
        #endif
        #if Client
        public void TryUpdateState(IState state, NetObjectState netObjectState, bool playEvents) => TryUpdateState(NetworkObjectId, state, netObjectState, playEvents);
        #endif
        #endregion
        
        #region Apply Partial State
        public void TryApplyPartialState(IState serverState, uint result) => ApplyPartialState(serverState, result);
        #endregion
        
        #endregion
        
        #region NetworkObject State
        
        public void ApplyNetObjectState(NetObjectState state)
        {
            // Look for changes
            if (queuedToBeDestroyed != state.QueuedToBeDestroyed)
            {
                // Something changed
                if (queuedToBeDestroyed)
                {
                    // Not destroyed anymore
                    ExitDestroyedState();
                }
                else
                {
                    // Now being destroyed
                    EnterDestroyedState();
                }
            }
            
            // Apply values
            SetInputOwner(state.InputOwnerClientId);
            queuedToBeDestroyed = state.QueuedToBeDestroyed;
            
            visual.gameObject.SetActive(!queuedToBeDestroyed);
        }
        
        private IState GetNetObjectState()
        {
            // Getting events to save/send/compare
            Event[] events = Array.Empty<Event>();
            #if Server
            if (IsServer)
                events = _events.ToArray();
            #endif
            
            // Creating the NetObjectState
            NetObjectState netObjectState = new NetObjectState(
                _inputOwnerClientId,
                events,
                queuedToBeDestroyed
            );
            
            return netObjectState;
        }
        #endregion
        
        #region Event
        #if Client
        public void PlayEvents(NetObjectState netObjectState)
        {
            if (IsServer) return;
            
            foreach (Event e in netObjectState.Events)
                OnEvent(e.Data);
        }
        #endif
        #endregion
        
        #region Tick
        public static void RunTick(uint tick)
        {
            foreach (var kvp in NetworkObjects)
            {
                NetObject obj = kvp.Value;
                obj.InternalTick(tick);
            }
        }

        private void InternalTick(uint tick)
        {
            #if Server
            // Check for destruction
            if (IsServer && tick >= _tickToDestroyThisObject)
            {
                Destroy(gameObject);
            }
            #endif
            
            #if Server
            // Check if this object should leave the sync group.
            if (IsServer && tick >= _tickToLeaveGroup) 
                LeaveSyncGroup();
            #endif
            
            // ----- Check for destruction -----
            if (queuedToBeDestroyed)
                return;
            
            // ----- Handle input -----
            // 1. Reset input
            InputData = null;
            Data = null;
            TickOfTheInput = 0;
                
            // 2. Get input as the input owner
            InputState inputState = new InputState();
            bool gotInput = false;
            #if Client
            // As the input owner and a client, get the local input
            if (IsInputOwner && IsClient)
            {
                // -> Local client, get local input
                inputState = InputContainer.GetInput(tick);
                gotInput = true;
            }
            #endif
                
            #if Server
            // 2. Get input as server, who isn't the input owner.
            bool shouldSearchForReceavedInput = IsServer && !IsInputOwner; // Server has to run the input. But not here, if the server is the input owner -> Is Host and play's this object.
            bool playerSentInput = InputTransportLayer.SentInput(InputOwnerClientId);
            if (shouldSearchForReceavedInput && playerSentInput && HasInputOwner)
            {
                // Not local client -> get input from InputTransportLayer
                try
                {
                    inputState = InputTransportLayer.GetInput(InputOwnerClientId, tick);
                    gotInput = true;
                }
                catch (Exception e)
                {
                    Debug.Log("No input found!");
                }
            }
            #endif

            // Set input if input was found
            if (gotInput)
            {
                InputData = inputState.Input;
                Data = inputState.Data;
                TickOfTheInput = inputState.Tick;
            }
            else
            {
                // No input found for this tick / NetObject
            }
            
            Tick(tick);
        }
        #endregion
        
        #region HasInputForThisTick
        protected bool HasInputForThisTick(uint tick)
            => TickOfTheInput == tick;
        #endregion
        
        #region GetSnapshotState
        public ObjectState GetSnapshotState(uint tick, bool clearEvents)
        {
            IState netObjectState = GetNetObjectState();
            IState state = GetCurrentState();
            
            #if Server
            if (clearEvents)
            {
                int i = 0;
                foreach (var @event in _events.ToArray())
                {
                    _eventCounts[@event.EventId]++;

                    if (_eventCounts[@event.EventId] >= NetRunner.EventPackageLossToAccountFor)
                    {
                        _eventCounts.Remove(@event.EventId);
                        _events.RemoveAt(i);
                    }

                    i++;
                }
            }
            #endif
            
            #if Client
            if (_firstRecordedState == 0)
                _firstRecordedState = tick;
            #endif
            
            ObjectState objectState = new ObjectState(tick, state, netObjectState);
            
            if (!IsServer)
            {
                // Todo: only save this in here, when the tick % sendingMode == 0 ...? Should help performance.
                _states.Store(tick, objectState);
            }
            
            return objectState;
        }
        
        public IState GetStateAtTick(uint tick) => _states.Get(tick).State;
        #endregion

        #region Collider Rollback
        #if Server
        public static void RunAllRollbacks()
        {
            // Handle every rollback
            foreach (RollbackInfo rollbackInfo in _rollbackInfos)
                rollbackInfo.NetObject.HandleColliderRollbackCalls(rollbackInfo.Tick, rollbackInfo.Method);
            
            // Mark every rollback as done.
            _rollbackInfos.Clear();
        }
        
        private void HandleColliderRollbackCalls(uint tickToRollbackTo, Action method)
        {
            // Save the current state
            Snapshot currentSnapshot = SnapshotContainer.GetCurrentSnapshot(0); // 0 because it doesn't matter anyway.
            
            // Get the state to rollback to
            Snapshot snapshotToRollbackTo = SnapshotContainer.GetSnapshot(tickToRollbackTo);
            
            // Rollback
            foreach (var kvp in snapshotToRollbackTo.States)
            {
                ulong networkId = kvp.Key;
                IState state = kvp.Value;
                NetObjectState netObjectState = (NetObjectState) snapshotToRollbackTo.NetObjectStates[networkId];
                TryApplyState(networkId, state, netObjectState, false);
            }
            
            // Run the method / code
            method();
            
            // Return to the current state
            foreach (var kvp in currentSnapshot.States)
            {
                ulong networkId = kvp.Key;
                IState state = kvp.Value;
                NetObjectState netObjectState = (NetObjectState) currentSnapshot.NetObjectStates[networkId];
                TryApplyState(networkId, state, netObjectState, false);
            }
        }
        
        public void RunCodeInRollback(uint tickToRollbackTo, Action method)
        {
            if (tickToRollbackTo == 0)
            {
                // No rollback possible
                Debug.LogWarning("Cannot rollback to tick 0! This is probably because the client hasn't received any state yet.");
                return;
            }
            
            _rollbackInfos.Add(new RollbackInfo()
            {
                NetObject = this,
                Tick = tickToRollbackTo,
                Method = method
            });
        }
        #endif
        #endregion
        
        #region PlayerInput Data
        #if Client
        public static IData GetPlayerInputData()
        {
            try
            {
                if (_globalInputDataSource == null)
                    throw new NotImplementedException("No global input data source found!"); // Will be catched and will return DefaultPlayerData
                
                return _globalInputDataSource.OnGetInputDataToSend();
            }
            catch (Exception e)
            {
                return new DefaultPlayerData();
            }
        }
        #endif
        #endregion

        #region Visual Sync Mode
        public void SetVisualSyncMode(bool shouldSync)
        {
            if (SyncVisual == shouldSync)
                return;

            SyncVisual = shouldSync;

            if (visual == null)
                return;

            if (SyncVisual)
            {
                // When re-enabling sync, snap immediately so we don't "smooth" from a stale state/velocity
                _visualVelocity = Vector3.zero;
                visual.position = transform.position;
                visual.rotation = transform.rotation;
            }
        }
        
        #if Client
        public static void SetAllVisualState()
        {
            foreach (var kvp in NetworkObjects)
            {
                // ulong networkId = kvp.Key;
                NetObject netObject = kvp.Value;
                
                // only do this, if the object's visual should be synced.
                if (!netObject.SyncVisual) continue;
                
                netObject.visual.position = netObject.transform.position;
                netObject.visual.rotation = netObject.transform.rotation;
            }
        }
        #endif
        #endregion
        
        #region Virtual Methods
        protected virtual void NetSpawn() { }
        protected virtual void NetDespawn() { }
        protected virtual void NetUpdate() { }
        protected virtual void Tick(uint tick) { }
        protected virtual IData OnGetInputDataToSend() { throw new NotImplementedException(); }
        protected virtual IState GetCurrentState() { return new DefaultObjectState(); }
        protected virtual void UpdateState(IState state) { throw new NotImplementedException(); }
        protected virtual void ApplyState(IState state) { throw new NotImplementedException(); }
        protected virtual void ApplyPartialState(IState state, uint part) { throw new NotImplementedException(); }
        protected virtual void EnterDestroyedState() { throw new NotImplementedException("Please implement this method, just to be aware, that you should handle this. No matter if needed or not. But you will probably need it in the future, so please just implement it and think about if you need to handle any logic in there."); }
        protected virtual void ExitDestroyedState() { throw new NotImplementedException("Please implement this method, just to be aware, that you should handle this. No matter if needed or not. But you will probably need it in the future, so please just implement it and think about if you need to handle any logic in there."); }
        #if Client
        protected virtual void OnEvent(IData eventData) { throw new NotImplementedException("Override the OnEvent method to handle events!"); }
        #endif
        #endregion
        
        #region Input Decoders

        protected bool GetButton(uint tick, string inputName)
        {
            if (!HasInputForThisTick(tick))
                throw new Exception("No input for this tick found! The input saved isn't for this tick.");
            
            return InputSender.GetInstance().GetButton(InputSender.ButtonInputReferences[inputName], InputData);
        }

        protected Vector2 GetVector2(uint tick, string inputName)
        {
            if (!HasInputForThisTick(tick))
                throw new Exception("No input for this tick found! The input saved isn't for this tick.");
            
            return InputSender.GetInstance().GetVector2(InputSender.Vector2InputReferences[inputName], InputData);
        }
        #endregion
        
        #region Input Data
        protected T GetData<T>(uint tick) where T : IData
        {
            if (!HasInputForThisTick(tick))
                throw new Exception("No input for this tick found! The data saved isn't for this tick.");
            
            if (Data == null)
                throw new Exception("No Data available!");

            if (Data.GetType() != typeof(T))
                throw new Exception("Cannot cast " + Data.GetType() + " into " + typeof(T) + "!");
            
            return (T) Data;
        }
        #endregion
        
        #region Local Input
        #if Client
        protected Dictionary<string, InputAction> InputActions => InputSender.Actions;
        #endif
        #endregion
        
        #region Network Interactions
        #if Server
        public static void RegisterInteraction(uint tick, NetObject obj1, NetObject obj2)
        {
            if (!NetworkManager.Singleton.IsServer)
                throw new Exception("You can't call this, since you aren't a server!");

            if (obj1.NetObjectSyncGroup != null && obj2.NetObjectSyncGroup != null)
                if (obj1.NetObjectSyncGroup == obj2.NetObjectSyncGroup) 
                { /* Ignore, since they are in the same group. */ }
                else
                    // Merge groups
                    obj1.NetObjectSyncGroup.MergeWith(tick, obj2.NetObjectSyncGroup);
            else if (obj1.NetObjectSyncGroup != null)
                // Let obj2 join obj1's group.
                obj2.EnterSyncGroup(tick, obj1.NetObjectSyncGroup);
            else if (obj2.NetObjectSyncGroup != null)
                // Let obj1 join obj2's group.
                obj1.EnterSyncGroup(tick, obj2.NetObjectSyncGroup);
            else
            {
                // Create a new group and let them join.
                NetObjectSyncGroup newGroup = new NetObjectSyncGroup(obj1.StateSendingMode);
                obj1.EnterSyncGroup(tick, newGroup);
                obj2.EnterSyncGroup(tick, newGroup);
            }
        }
        #endif
        #endregion

        #region Network Object Sync Group 
        #if Server
        public void EnterSyncGroup(uint tick, NetObjectSyncGroup group)
        {
            if (!IsServer)
                throw new Exception("You can't call this, since you aren't a server!");

            if (NetObjectSyncGroup != null)
                throw new Exception("To enter a group, you first have to leave the current one!");
            
            // Remove from the current list.
            StateSendingList[StateSendingMode].Remove(this);
            if (StateSendingList[StateSendingMode].Count == 0)
                StateSendingList.Remove(StateSendingMode);
            
            NetObjectSyncGroup = group; // -> changes the StateSendingMode
            NetObjectSyncGroup.Members.Add(this); // Join
            
            // Add to the new list
            StateSendingList.TryAdd(StateSendingMode, new List<NetObject>());
            StateSendingList[StateSendingMode].Add(this);

            // Change the state sending mode of the group, if needed.
            if ((byte) privateStateSendingMode < (byte) StateSendingMode)
                NetObjectSyncGroup.SendingMode = StateSendingMode;

            _tickToLeaveGroup = tick + SnapshotContainer.SnapshotBufferSize;
        }
        #endif
        
        #if Server
        public void LeaveSyncGroup()
        {
            if (!IsServer)
                throw new Exception("You can't call this, since you aren't a server!");

            if (NetObjectSyncGroup == null)
                throw new Exception("You are not in a group!");
            
            // Remove from the current list.
            StateSendingList[StateSendingMode].Remove(this);
            if (StateSendingList[StateSendingMode].Count == 0)
                StateSendingList.Remove(StateSendingMode);

            // Recalculate the sending mode of the group.
            if (NetObjectSyncGroup.Members.Count > 0) 
            {
                byte newSendingMode = byte.MaxValue;
                foreach (var member in NetObjectSyncGroup.Members)
                {
                    if ((byte) member.privateStateSendingMode < newSendingMode)
                        newSendingMode = (byte) member.privateStateSendingMode;
                }
            
                NetObjectSyncGroup.SendingMode = (SendingMode) newSendingMode;
            }
            
            NetObjectSyncGroup.Members.Remove(this); // Leave
            NetObjectSyncGroup = null; // -> changes the StateSendingMode
            
            // Add to the new list
            StateSendingList.TryAdd(StateSendingMode, new List<NetObject>());
            StateSendingList[StateSendingMode].Add(this);
            
            _tickToLeaveGroup = uint.MaxValue;
        }
        #endif
        #endregion
        
        #region State Sending Mode
        #if Server
        public void ChangeStateSendingMode(SendingMode newMode)
        {
            if (!IsServer)
            {
                Debug.LogError("You can't call this, since you aren't a server!");
                return;
            }

            if ((byte) privateStateSendingMode == (byte) newMode)
                return; // Nothing changed.

            if (NetObjectSyncGroup == null)
            {
                // Remove from the current list.
                StateSendingList[StateSendingMode].Remove(this);
                if (StateSendingList[StateSendingMode].Count == 0)
                    StateSendingList.Remove(StateSendingMode);
                
                // Update variable
                privateStateSendingMode = newMode;
                
                // Add to the new list
                StateSendingList.TryAdd(StateSendingMode, new List<NetObject>());
                StateSendingList[StateSendingMode].Add(this);
            }
            else
            {
                // Update variable
                privateStateSendingMode = newMode;
                
                // -> Switch later when we are no longer in the current group.
            }
        }
        #endif
        #endregion
        
        #region Events
        #if Server
        public void RegisterEvent(uint tick, IData data)
        {
            if (!IsServer)
                throw new Exception("You can't call this, since you aren't a server!");
            
            Event e = new Event(tick, data);
            
            _events.Add(e);
            _eventCounts.Add(e.EventId, 0);
        }
        
        public bool HasEvents() => _events.Count > 0;
        #endif
        #endregion
        
        #region Prediction State
        public void ChangePredictionState(bool shouldBePredicted)
        {
            bool previousState = isPredicted;
            isPredicted = shouldBePredicted;

            if (previousState == shouldBePredicted)
                return;
            
            // Something changed!

            UpdateObjectHandling();
        }
        
        private void UpdateObjectHandling()
        {
            if (isPredicted)
            {
                // Is not predicted
                if (IsInputOwner)
                {
                    // Input owner and predicted
                    // -> just check the state with the server state.
                }
                else
                {
                    // Not the input owner and predicted
                    // -> apply states and reconcile (if needed)
                }
            }
            else
            {
                // Is no longer predicted
                if (IsInputOwner)
                {
                    // Input owner but not predicted???
                    Debug.LogWarning("If you are the owner, you should always predict!");
                }
                else
                {
                    // Not the input owner and not predicted
                    // -> just apply the state.
                }
            }
        }
        #endregion
        
        #region Destroying
        
        #if Server
        public void NetDestroy(uint tick)
        {
            if (!IsServer) 
                throw new Exception("You can't call this, since you aren't a server!");
            
            _tickToDestroyThisObject = tick + SnapshotContainer.SnapshotBufferSize + 1; // The +1 is just to be save. I don't know if it's necessary, but it won't hurt.
            queuedToBeDestroyed = true;
            
            name += "(queued to be destroyed)";
            visual.gameObject.SetActive(false);
            EnterDestroyedState();
        }
        #endif
        
        #endregion
    }
}