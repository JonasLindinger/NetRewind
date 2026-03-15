using NetRewind.Utils.CustomDataStructures;
using Unity.Netcode;

namespace NetRewind.Utils.Sync
{
    [DataType]
    public struct DefaultPlayerData : IData
    {
        // DO NOT DELETE THIS SCRIPT !!!
        
        // This is a placeholder that gets used in the case, that no Input Data is used.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
    }
}