using Unity.Netcode;

namespace NetRewind.Utils.Simulation.State
{
    [StateType]
    public struct NetObjectState : IState
    {
        public ulong InputOwnerClientId;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InputOwnerClientId);
        }

        public uint Compare(IState localState, IState serverState)
        {
            NetObjectState local = (NetObjectState) localState;
            NetObjectState server = (NetObjectState) serverState;

            if (local.InputOwnerClientId != server.InputOwnerClientId)
            {
                return 1;
            }

            return 0;
        }
    }
}