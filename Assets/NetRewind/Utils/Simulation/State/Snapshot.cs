using System.Collections.Generic;
using Unity.Netcode;

namespace NetRewind.Utils.Simulation.State
{
    public struct Snapshot : INetworkSerializable
    {
        public uint Tick => _tick;
        private uint _tick;
        public Dictionary<ulong, IState> States;
        public Dictionary<ulong, IState> NetObjectStates;

        public Snapshot(uint tick)
        {
            _tick = tick;
            States = new Dictionary<ulong, IState>();
            NetObjectStates = new Dictionary<ulong, IState>();
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Serializing TickOfTheInput
            serializer.SerializeValue(ref _tick);

            #region Serlialize Dictionary
            if (serializer.IsWriter) // Sending side
            {
                int count = States.Count;
                serializer.SerializeValue(ref count); // Serialize the count

                foreach (var kvp in States)
                {
                    ulong networkId = kvp.Key;
                    IState state = kvp.Value;

                    // Serialize the networkId
                    serializer.SerializeValue(ref networkId);
        
                    // Serialize the state type
                    int stateType = StateTypeRegistry.GetId(state.GetType());
                    serializer.SerializeValue(ref stateType);

                    // Let the state serialize itself
                    state.NetworkSerialize(serializer);
                }
            }
            else // Receiving side
            {
                if (States == null)
                    States = new Dictionary<ulong, IState>();
                
                int count = 0;
                serializer.SerializeValue(ref count); // Read the count

                for (int i = 0; i < count; i++)
                {
                    ulong networkId = 0;
                    serializer.SerializeValue(ref networkId); // Read the networkId

                    int stateType = 0;
                    serializer.SerializeValue(ref stateType); // Read the state type

                    // Create an instance using a factory/registry
                    IState state = StateTypeRegistry.Create(stateType);
                    if (state == null) continue;
                    state.NetworkSerialize(serializer);
                    States[networkId] = state;
                }
            }
            #endregion
            
            #region Serlialize Dictionary
            if (serializer.IsWriter) // Sending side
            {
                int count = NetObjectStates.Count;
                serializer.SerializeValue(ref count); // Serialize the count

                foreach (var kvp in NetObjectStates)
                {
                    ulong networkId = kvp.Key;
                    IState state = kvp.Value;

                    // Serialize the networkId
                    serializer.SerializeValue(ref networkId);
        
                    // Serialize the state type
                    int stateType = StateTypeRegistry.GetId(state.GetType());
                    serializer.SerializeValue(ref stateType);

                    // Let the state serialize itself
                    state.NetworkSerialize(serializer);
                }
            }
            else // Receiving side
            {
                if (NetObjectStates == null)
                    NetObjectStates = new Dictionary<ulong, IState>();
                
                int count = 0;
                serializer.SerializeValue(ref count); // Read the count

                for (int i = 0; i < count; i++)
                {
                    ulong networkId = 0;
                    serializer.SerializeValue(ref networkId); // Read the networkId

                    int stateType = 0;
                    serializer.SerializeValue(ref stateType); // Read the state type

                    // Create an instance using a factory/registry
                    IState state = StateTypeRegistry.Create(stateType);
                    if (state == null) continue;
                    state.NetworkSerialize(serializer);
                    NetObjectStates[networkId] = state;
                }
            }
            #endregion
        }
    }
}