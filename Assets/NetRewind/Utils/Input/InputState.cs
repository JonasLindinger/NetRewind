using Unity.Netcode;

namespace NetRewind.Utils.Input
{
    public struct InputState : INetworkSerializable
    {
        public uint Tick;
        public byte[] Input;
        
        public void NetworkSerialize<U>(BufferSerializer<U> serializer)
            where U : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Input);
        }
    }
}