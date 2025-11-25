using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation.State
{
    public struct ObjectState : INetworkSerializable
    {
        public uint Tick;
        public IState State;

        public ObjectState(uint tick, IState state)
        {
            Tick = tick;
            State = state;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);

            #region Serialize State
            if (serializer.IsReader)
            {
                // Reader
                int stateType = 0;
                serializer.SerializeValue(ref stateType); // Read the state type
                
                State = StateFactory.Create(stateType);
                if (State == null)
                    Debug.LogWarning("Failed to create state of type id: " + stateType + ". Please add your state type to the StateFactory!");
                else
                    State.NetworkSerialize(serializer);
            }
            else
            {
                // Writer
                int stateType = State.GetStateType();
                serializer.SerializeValue(ref stateType);
                
                State.NetworkSerialize(serializer);
            }
            #endregion
        }
    }
}