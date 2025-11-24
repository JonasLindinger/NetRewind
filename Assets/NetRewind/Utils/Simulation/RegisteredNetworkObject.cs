using System.Collections.Generic;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Input;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public abstract class RegisteredNetworkObject : NetworkBehaviour
    {
        public static Dictionary<ulong, RegisteredNetworkObject> NetworkObjects = new Dictionary<ulong, RegisteredNetworkObject>();
        
        [Header("State sync")]
        [SerializeField] private SendingMode stateSendingMode = SendingMode.Full;
        
        #if Client
        private CircularBuffer<ObjectState> _states = new CircularBuffer<ObjectState>(SnapshotContainer.SnapshotBufferSize);
        #endif

        #if Server
        private ObjectState _latestSavedState;
        #endif
        
        public override void OnNetworkSpawn()
        {
            NetworkObjects.Add(NetworkObjectId, this);
        }

        public override void OnNetworkDespawn()
        {
            NetworkObjects.Remove(NetworkObjectId);
        }

        public static void RunTick(uint tick)
        {
            foreach (var kvp in NetworkObjects)
            {
                RegisteredNetworkObject obj = kvp.Value;
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
            #if Client
            IState serverState = serverObjectState.State;
            
            ObjectState clientObjectState = _states.Get(serverObjectState.Tick);
            IState localState = clientObjectState.State;
            
            OnStateReceived(localState, serverState);
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
            if (!IsServer)
            {
                // Todo: only save this in here, when the tick % sendingMode == 0 ...? Should help performance.
                _states.Store(tick, new ObjectState(tick, state));
            }
            #endif
            return state;
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
    }
}