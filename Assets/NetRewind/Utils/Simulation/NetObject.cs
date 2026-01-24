using System;
using System.Collections.Generic;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Features.ShowOnly;
using NetRewind.Utils.Input;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation.State;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace NetRewind.Utils.Simulation
{
    public abstract class NetObject : NetworkBehaviour
    {
        private static IInputDataSource _globalInputDataSource; // static because there can only be one!
        
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
        #if Client
        private uint _lastReceivedStateTick;
        private uint _firstRecordedState;
        #endif

        #if Server
        private uint _tickToLeaveGroup = uint.MaxValue;
        #endif

        public bool IsInputOwner => _inputOwnerClientId == NetworkManager.Singleton.LocalClientId;
        public ulong InputOwnerClientId => _inputOwnerClientId;
        private ulong _inputOwnerClientId = ulong.MaxValue;
        
        private IInputDataSource _inputDataSource;
        private ITick _tickInterface;
        private IInputListener _inputListener;
        private IStateHolder _stateHolder;
        
        private Vector3 _visualVelocity;

        private void OnValidate()
        {
            isPredicted = shouldPredict;
        }

        public override void OnNetworkSpawn()
        {
            TryGetComponent(out _tickInterface);
            TryGetComponent(out _inputListener);
            TryGetComponent(out _stateHolder);
            
            TryGetComponent(out _inputDataSource);
            
            NetworkObjects.Add(NetworkObjectId, this);
            
            visual.SetParent(null);
            visual.name = name + "[" + NetworkObjectId + "] Visual";

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

            if (_inputDataSource == _globalInputDataSource && _inputDataSource != null)
                _globalInputDataSource = null;
            
            visual.SetParent(gameObject.transform);
            Destroy(visual.gameObject);
            
            NetDespawn();
        }

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
                    Simulation.TimeBetweenTicks // seconds
                );
                
                visual.rotation = transform.rotation;
            }
            
            #if Client
            if (IsInputOwner && _inputListener != null)
                _inputListener.NetInputOwnerUpdate();
            #endif
            
            NetUpdate();
        }

        public void SetInputOwner(ulong newInputOwnerClientId)
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
        
        public void RemoveInputOwner() => SetInputOwner(ulong.MaxValue);
        
        private void HandleInputOwnerUpdate()
        {
            if (IsInputOwner)
            {
                // -> Just became input owner
                
                // Set the global input data source, if the net object has one.
                if (_inputDataSource != null)
                    _globalInputDataSource = _inputDataSource;
            }
            else
            {
                // -> We aren't the input owner anymore.
                
                // Reset the global input data source, if it was the one.
                if (_globalInputDataSource == _inputDataSource && _inputDataSource != null)
                    _globalInputDataSource = null;
            }
        }

        public void TryApplyPartialState(IState serverState, uint result) => _stateHolder.ApplyPartialState(serverState, result);
        
        public static void TryApplyState(ulong networkId, IState state, IState netObjectState)
        {
            try
            {
                NetworkObjects[networkId].SetNetObjectState(netObjectState);
                NetworkObjects[networkId]._stateHolder.ApplyState(state);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to apply state: " + e);
            }
        }
        public void TryApplyState(IState state, IState netObjectState) => TryApplyState(NetworkObjectId, state, netObjectState);
        
        public static void TryUpdateState(ulong networkId, IState state, IState netObjectState)
        {
            try
            {
                NetworkObjects[networkId].SetNetObjectState(netObjectState);
                NetworkObjects[networkId]._stateHolder.UpdateState(state);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to update state: " + e);
            }
        }
        public void TryUpdateState(IState state, IState netObjectState) => TryUpdateState(NetworkObjectId, state, netObjectState);

        private void SetNetObjectState(IState netObjectState)
        {
            NetObjectState state = (NetObjectState) netObjectState;
            
            SetInputOwner(state.InputOwnerClientId);
        }
        
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
            // Check if this object should leave the sync group.
            if (IsServer && tick >= _tickToLeaveGroup) 
                LeaveSyncGroup();
            #endif
            
            // If this is an inputListener, try to get input
            if (_inputListener != null)
            {
                // Reset input
                _inputListener.InputData = null;
                _inputListener.Data = null;
                _inputListener.TickOfTheInput = 0;
                
                // Get input
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
                bool shouldRunAsServer = IsServer && !IsInputOwner; // Server has to run the input. But not here, if the server is the input owner -> Is Host and play's this object.
                bool sentInput = InputTransportLayer.SentInput(InputOwnerClientId);
                bool hasInputOwner = _inputOwnerClientId != ulong.MaxValue;
                if (shouldRunAsServer && sentInput && hasInputOwner)
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

                // Set input if there is any
                if (gotInput)
                {
                    _inputListener.InputData = inputState.Input;
                    _inputListener.Data = inputState.Data;
                    _inputListener.TickOfTheInput = inputState.Tick;
                }
            }
            
            // Trigger the tick
            _tickInterface?.Tick(tick);
        }

        public bool HasInputForThisTick(uint tick)
        {
            if (_inputListener == null)
                return false;
            
            return _inputListener.TickOfTheInput == tick;
        }
        
        public ObjectState GetSnapshotState(uint tick)
        {
            if (_stateHolder == null)
                throw new Exception("No state holder found!");
            
            IState state = _stateHolder.GetCurrentState();
            IState netObjectState = GetNetObjectState();
            
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

        private IState GetNetObjectState()
        {
            return new NetObjectState()
            {
                InputOwnerClientId = _inputOwnerClientId,
            };
        }
        
        public IState GetStateAtTick(uint tick) => _states.Get(tick).State;
        
        public static IData GetPlayerInputData()
        {
            return _globalInputDataSource != null ? 
                _globalInputDataSource.OnInputData() :
                new DefaultPlayerData();
        }
        
        public void SetVisualSyncMode(bool shouldSync) => SyncVisual = shouldSync;

        protected virtual void NetSpawn() { }
        protected virtual void NetDespawn() { }
        protected virtual void NetUpdate() { }
        
        protected bool GetButton(string inputName) => InputSender.GetInstance().GetButton(InputSender.ButtonInputReferences[inputName], _inputListener.InputData);
        protected Vector2 GetVector2(string inputName) => InputSender.GetInstance().GetVector2(InputSender.Vector2InputReferences[inputName], _inputListener.InputData);

        protected T GetData<T>() where T : IData
        {
            if (_inputListener == null)
                throw new Exception("This object doesn't have an input listener!");
            
            if (_inputListener.Data == null)
                throw new Exception("No Data available!");

            if (_inputListener.Data.GetType() != typeof(T))
                throw new Exception("Cannot cast " + _inputListener.Data.GetType() + " into " + typeof(T) + "!");
            
            return (T) _inputListener.Data;
        }
        protected Dictionary<string, InputAction> InputActions => InputSender.Actions;
        
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
    }
}