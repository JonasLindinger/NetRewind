using NetRewind.Utils.Simulation.Data;
using Unity.Netcode;

namespace NetRewind.Utils.Simulation.State
{
    [DataType]
    public struct DefaultPlayerData : IData
    {
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            
        }
    }
}