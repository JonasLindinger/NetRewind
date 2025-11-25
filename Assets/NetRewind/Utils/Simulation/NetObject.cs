using System;
using System.Collections.Generic;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Input;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public abstract class NetObject : NetworkBehaviour
    {
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
        
        private Vector3 _visualVelocity;
        
        public override void OnNetworkSpawn()
        {
            NetworkObjects.Add(NetworkObjectId, this);
            
            visual.SetParent(null); // Unparent from here.
            DontDestroyOnLoad(visual.gameObject);
            
            NetSpawn();
        }

        public override void OnNetworkDespawn()
        {
            NetworkObjects.Remove(NetworkObjectId);
            
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
            }

            NetUpdate();
        }

        public static void ApplyState(ulong clientId, IState state)
        {
            try
            {
                NetworkObjects[clientId].ApplyState(state);
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
            #if Server
            if (tick % (byte) stateSendingMode == 0 && IsServer)
                SendStateRPC(_latestSavedState);
            #endif
            
            OnTickTriggered(tick);
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
                // Debug.LogWarning("Received state for tick " + serverObjectState.Tick + " but we are at tick " + Simulation.CurrentTick + "! IGNORING / Waiting for tick adjustments!");
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
                
                OnStateReceived(localState, serverState);
            }
            catch (KeyNotFoundException e) { }
            #endif
        }

        protected virtual void OnStateReceived(IState localState, IState serverState)
        {
            #if Client
            UpdateState(serverState);
            #endif
        }
        
        public IState GetSnapshotState(uint tick)
        {
            IState state = GetCurrentState();
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

        protected virtual void NetSpawn()
        {
            
        }
        protected virtual void NetDespawn()
        {
            
        }
        protected virtual void NetUpdate()
        {
            
        }
        protected abstract void OnTickTriggered(uint tick);
        protected virtual IState GetCurrentState()
        {
            // If this runs, no state is being synced.
            throw new System.NotImplementedException();
        }
        protected virtual void UpdateState(IState state)
        {
            // This should never run!
            throw new System.NotImplementedException("Implement the UpdateState method in your subclass, when implementing the GetCurrentState method!");
        }
        protected virtual void ApplyState(IState state)
        {
            throw new System.NotImplementedException("Implement the ApplyState method in your subclass, when implementing the GetCurrentState method!");
        }
        protected virtual void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException("Implement the ApplyPartialState method in your subclass, when implementing the GetCurrentState method and using the CompareResult.PartialCorrection result!");
        }

        protected abstract bool IsPredicted();
    }
}