using NetRewind.Utils.Input.Data;
using Unity.Netcode;

namespace _Demo.Scripts.Player
{
    [DataType]
    public struct ShootEvent : IData
    {
        public ulong ShooterClientId => _shooterClientId;
        private ushort _shooterClientId; // ushort to save space.

        public ShootEvent(ulong shooterClientId)
        {
            _shooterClientId = (ushort) shooterClientId;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _shooterClientId);
        }
    }
}