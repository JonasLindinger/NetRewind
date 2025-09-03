using NetRewind.DONOTUSE;
using NetRewind.Utils;
using Unity.Netcode;
using UnityEngine;

namespace _Demo
{
    public class PlayerState : IState
    {
        public Vector3 Position;
        public Vector3 Rotation;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }

        static PlayerState() => StateFactory.Register((int) StateTypes.Player, () => new PlayerState());
        
        public int GetStateType() => (int) StateTypes.Player;
    }
}