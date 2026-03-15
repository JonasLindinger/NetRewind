using NetRewind.Utils.CustomDataStructures;
using Unity.Netcode;

namespace NetRewind.Utils.Sync
{
    [StateType]
    public struct DefaultObjectState : IState
    {
        // DO NOT DELETE THIS SCRIPT !!!
        
        // This is a placeholder that gets used in the case, that no State is used.
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter { }
        
        public uint Compare(IState localState, IState serverState)
        {
            // Always pretend that everything is fine.
            return 0;
        }
    }
}