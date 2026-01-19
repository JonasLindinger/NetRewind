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
        private static IInputDataSource _inputDataSource;
        
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
        [FormerlySerializedAs("privateSendingMode")] [HideInInspector] public SendingMode privateStateSendingMode; // The sending mode this object should normally be in
        
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
        
        #if Client
        private CircularBuffer<ObjectState> _states = new CircularBuffer<ObjectState>(SnapshotContainer.SnapshotBufferSize);
        private uint _lastReceivedStateTick;
        private uint _firstRecordedState;
        #endif

        #if Server
        private uint _tickToLeaveGroup = uint.MaxValue;
        #endif

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
            
            if (IsOwner && _inputDataSource == null)
                TryGetComponent(out _inputDataSource);
            
            NetworkObjects.Add(NetworkObjectId, this);
            
            visual.SetParent(null);
            visual.name = name + " (" + NetworkObjectId + ") Visual";

            isPredicted = shouldPredict;
            privateStateSendingMode = initialSendingMode;
            
            NetSpawn();

            #if Server
            if (IsServer)
            {
                StateSendingList.TryAdd(StateSendingMode, new List<NetObject>());
                StateSendingList[StateSendingMode].Add(this);
            }
            #endif
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

            if (TryGetComponent(out IInputDataSource tempPlayer) && tempPlayer == _inputDataSource)
                _inputDataSource = null;
            
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
            
            if (visual != null && SyncVisual)
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
            if (IsOwner && _inputListener != null)
                _inputListener.NetOwnerUpdate();
            #endif
            
            NetUpdate();
        }

        public void TryApplyPartialState(IState serverState, uint result) => _stateHolder.ApplyPartialState(serverState, result);
        
        public static void TryApplyState(ulong networkId, IState state)
        {
            try
            {
                NetworkObjects[networkId]._stateHolder.ApplyState(state);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to apply state: " + e);
            }
        }
        public void TryApplyState(IState state) => TryApplyState(NetworkObjectId, state);
        
        public static void TryUpdateState(ulong networkId, IState state)
        {
            try
            {
                NetworkObjects[networkId]._stateHolder.UpdateState(state);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to update state: " + e);
            }
        }
        public void TryUpdateState(IState state) => TryUpdateState(NetworkObjectId, state);

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
            if (!(IsServer || IsPredicted))
                return;

            #if Server
            if (IsServer && tick >= _tickToLeaveGroup) 
                LeaveSyncGroup();
            #endif
            
            if (_inputListener != null)
            {
                #if Client
                if (IsOwner && IsClient)
                {
                    // -> Local client, get local input
                    InputState inputState = InputContainer.GetInput(tick);
                    _inputListener.InputData = inputState.Input;
                    _inputListener.Data = inputState.Data;
                    _inputListener.TickOfTheInput = inputState.Tick;
                }
                #endif
                #if Server 
                if (IsServer && !IsOwner && InputTransportLayer.SentInput(OwnerClientId))
                {
                    // Not local client -> get input from InputTransportLayer
                    try
                    {
                        InputState inputState = InputTransportLayer.GetInput(OwnerClientId, tick);
                        _inputListener.InputData = inputState.Input;
                        _inputListener.Data = inputState.Data;
                        _inputListener.TickOfTheInput = inputState.Tick;
                    }
                    catch (Exception e)
                    {
                        Debug.Log("No input found!");
                    }
                }
                #endif
                
                if (_inputListener.InputData != null && _inputListener.Data != null)
                    _tickInterface?.Tick(tick);
            }
            else
                _tickInterface?.Tick(tick);
        }

        public bool HasInputForThisTick(uint tick)
        {
            if (_inputListener == null)
                return false;
            
            return _inputListener.TickOfTheInput == tick;
        }
        
        public IState GetSnapshotState(uint tick)
        {
            if (_stateHolder == null)
                throw new Exception("");
            
            IState state = _stateHolder.GetCurrentState();
            #if Client
            if (_firstRecordedState == 0)
                _firstRecordedState = tick;
            
            if (!IsServer)
            {
                // Todo: only save this in here, when the tick % sendingMode == 0 ...? Should help performance.
                _states.Store(tick, new ObjectState(tick, state));
            }
            #endif
            return state;
        }

        #if Client
        public IState GetStateAtTick(uint tick) => _states.Get(tick).State;
        #endif
        
        public static IData GetPlayerInputData()
        {
            if (_inputDataSource != null)
                return _inputDataSource.OnInputData();
            
            return new DefaultPlayerData();
        }
        
        public void SetVisualSyncMode(bool shouldSync) => SyncVisual = shouldSync;

        protected virtual void NetSpawn() { }
        protected virtual void NetDespawn() { }
        protected virtual void NetUpdate() { }
        
        protected bool GetButton(int id) => InputSender.GetInstance().GetButton(id, _inputListener.InputData);
        protected Vector2 GetVector2(int id) => InputSender.GetInstance().GetVector2(id, _inputListener.InputData);

        protected T GetData<T>() where T : IData => (T) _inputListener.Data;
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
                if (IsOwner)
                {
                    // Owner and predicted
                    // -> just check the state with the server state.
                }
                else
                {
                    // Not the owner and predicted
                    // -> apply states and reconcile (if needed)
                }
            }
            else
            {
                // Is no longer predicted
                if (IsOwner)
                {
                    // Owner but not predicted???
                    Debug.LogWarning("If you are the owner, you should always predict!");
                }
                else
                {
                    // Not the owner and not predicted
                    // -> just apply the state.
                }
            }
        }
    }
}