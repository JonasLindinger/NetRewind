using System;
using System.Collections.Generic;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Input;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetRewind.Utils.Simulation
{
    public abstract class NetObject : NetworkBehaviour
    {
        private static IInputDataSource _inputDataSource;
        
        // Todo: Add child NetObject...?
        public static Dictionary<ulong, NetObject> NetworkObjects = new Dictionary<ulong, NetObject>();
        
        [Header("Networking")]
        [SerializeField] private SendingMode stateSendingMode = SendingMode.Full;
        [SerializeField] private Transform visual;
        [Space(10)]
        
        #if Client
        private CircularBuffer<ObjectState> _states = new CircularBuffer<ObjectState>(SnapshotContainer.SnapshotBufferSize);
        private uint _lastReceivedStateTick;
        private uint _firstRecordedState;
        #endif

        #if Server
        private ObjectState _latestSavedState;
        #endif
        
        private ITick _tickInterface;
        private IInputListener _inputListener;
        private IStateHolder _stateHolder;
        
        private Vector3 _visualVelocity;
        
        public override void OnNetworkSpawn()
        {
            TryGetComponent(out _tickInterface);
            TryGetComponent(out _inputListener);
            TryGetComponent(out _stateHolder);
            
            if (IsOwner && _inputDataSource == null)
                TryGetComponent(out _inputDataSource);
            
            NetworkObjects.Add(NetworkObjectId, this);
            
            visual.SetParent(null); // Unparent from here.
            DontDestroyOnLoad(visual.gameObject);
            
            NetSpawn();
        }

        public override void OnNetworkDespawn()
        {
            NetworkObjects.Remove(NetworkObjectId);

            if (TryGetComponent(out IInputDataSource tempPlayer) && tempPlayer == _inputDataSource)
                _inputDataSource = null;
            
            DontDestroyOnLoad(visual.gameObject);
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
            
            if (visual != null)
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

        public static void ApplyState(ulong clientId, IState state)
        {
            try
            {
                NetworkObjects[clientId]._stateHolder.ApplyState(state);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to apply state: " + e);
            }
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
            if (!(IsServer || IsPredicted()))
                return;
            
            #if Server
            if (tick % (byte) stateSendingMode == 0 && IsServer)
                SendStateRPC(_latestSavedState);
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

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Reliable)]
        private void SendStateRPC(ObjectState serverObjectState)
        {
            // Todo: make this an option to toggle between predict everything (Sending one snapshot) and predict predicted objects only (this) (sending single states)
            #if Client
            // Only accept new states!
            if (serverObjectState.Tick <= _lastReceivedStateTick)
                return;
            
            if (serverObjectState.Tick > Simulation.CurrentTick)
            {
                // Debug.LogWarning("Received state for tick " + serverObjectState.TickOfTheInput + " but we are at tick " + Simulation.CurrentTick + "! IGNORING / Waiting for tick adjustments!");
                return;
            }
            
            // Check if our buffer is even capable of containing the snapshot at that tick.
            if (serverObjectState.Tick <= Simulation.CurrentTick - SnapshotContainer.SnapshotBufferSize) 
                return;
            
            // Check if we should have the snapshot at that tick.
            if (serverObjectState.Tick < _firstRecordedState) 
                return;
            
            // Only accept if we don't already do correction.
            if (Simulation.IsCorrectingGameState) return;
            
            // --- Accept the state ---
            _lastReceivedStateTick = serverObjectState.Tick;
            
            IState serverState = serverObjectState.State;
            
            try
            {
                ObjectState clientObjectState = _states.Get(serverObjectState.Tick);
                IState localState = clientObjectState.State;
                
                OnStateReceived(serverObjectState.Tick, localState, serverState);
            }
            catch (KeyNotFoundException e) { }
            #endif
        }

        private void OnStateReceived(uint serverStateTick, IState localState, IState serverState)
        {
            #if Client
            // Check if we are predicting this object.
            if (!IsPredicted())
            {
                // No prediction -> just apply the state.
                _stateHolder.UpdateState(serverState);
                return;
            }
            
            // Prediction -> compare to prediction.
            try
            {
                uint result = localState.Compare(localState, serverState);

                switch (result)
                {
                    case (uint) CompareResult.Equal:
                        // Everything is fine.
                        break;
                    case (uint) CompareResult.WorldCorrection:
                        // Apply the entire server state and recalculate some ticks to be ahead of the server again.
                        SnapshotTransportLayer.RequestSnapshot();
                        break;
                    default:
                        // Apply only a part of the server state.
                        // Don't use try catch here, because if we receive states, we should sync them!
                        _stateHolder.ApplyPartialState(serverState, result);
                        break;
                }
            }
            catch (Exception e)
            {
                
            }
            
            #endif
        }
        
        public IState GetSnapshotState(uint tick)
        {
            if (_stateHolder == null)
                throw new NotImplementedException();
            
            IState state = _stateHolder.GetCurrentState();
            #if Server
            if (IsServer) 
            {
                ObjectState objectState = new ObjectState(tick, state);
                _latestSavedState = objectState;
            }
            #endif
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

        public static IData GetPlayerInputData()
        {
            if (_inputDataSource != null)
                return _inputDataSource.OnInputData();
            
            return new DefaultPlayerData();
        }

        protected virtual void NetSpawn() { }
        protected virtual void NetDespawn() { }
        protected virtual void NetUpdate() { }
        
        protected bool GetButton(int id) => InputSender.GetInstance().GetButton(id, _inputListener.InputData);
        protected Vector2 GetVector2(int id) => InputSender.GetInstance().GetVector2(id, _inputListener.InputData);

        protected T GetData<T>() where T : IData => (T) _inputListener.Data;
        protected Dictionary<string, InputAction> InputActions => InputSender.Actions;
        protected abstract bool IsPredicted();
    }
}