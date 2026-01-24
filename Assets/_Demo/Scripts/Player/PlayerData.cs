using NetRewind.Utils.Input.Data;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    [DataType]
    public struct PlayerData : IData
    {
        public uint TickToRollbackToWhenShooting;
        public Vector2 Rotation;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref TickToRollbackToWhenShooting);
        }
    }
}