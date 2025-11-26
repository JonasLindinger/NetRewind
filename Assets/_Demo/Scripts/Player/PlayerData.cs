using NetRewind.Utils.Input.Data;
using Unity.Netcode;

namespace _Demo.Scripts.Player
{
    [DataType]
    public struct PlayerData : IData
    {
        public float YRotation;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref YRotation);
        }
    }
}