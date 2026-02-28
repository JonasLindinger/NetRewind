using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation.State
{
    public struct ObjectState : INetworkSerializable
    {
        public uint Tick;
        public IState State;
        public IState NetObjectState;

        public ObjectState(uint tick, IState state, IState netObjectState)
        {
            Tick = tick;
            State = state;
            NetObjectState = netObjectState;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);

            #region Serialize State
            if (serializer.IsReader)
            {
                // Reader
                ushort stateType = 0;
                serializer.SerializeValue(ref stateType); // Read the state type
                
                State = StateTypeRegistry.Create(stateType);
                if (State != null)
                    State.NetworkSerialize(serializer);
            }
            else
            {
                // Writer
                ushort stateType = StateTypeRegistry.GetId(State.GetType());
                serializer.SerializeValue(ref stateType);
                
                State.NetworkSerialize(serializer);
            }
            #endregion
            
            #region Serialize NetObject State
            if (serializer.IsReader)
            {
                // Reader
                ushort stateType = 0;
                serializer.SerializeValue(ref stateType); // Read the state type
                
                NetObjectState = StateTypeRegistry.Create(stateType);
                if (NetObjectState != null)
                    NetObjectState.NetworkSerialize(serializer);
            }
            else
            {
                // Writer
                ushort stateType = StateTypeRegistry.GetId(NetObjectState.GetType());
                serializer.SerializeValue(ref stateType);
                
                NetObjectState.NetworkSerialize(serializer);
            }
            #endregion
        }
    }
}