using Unity.Netcode;

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
            State.NetworkSerialize(serializer);
        }
    }
}