using System.Collections.Generic;
using NetRewind.Utils.Input.Data;
using Unity.Netcode;

namespace NetRewind.Utils.Simulation
{
    public struct Event : INetworkSerializable
    {
        private static uint _eventCounter = 0;

        // Getter
        public uint EventId => _eventId;
        public uint Tick => _tick;
        public IData Data => _data;
        
        private uint _eventId;
        private uint _tick;
        private IData _data;

        #if Server
        // For the server, no need to serialize
        public uint TickToDeleteTheEvent { get; private set; }
        #endif
        
        public Event(uint tick, IData eventData)
        {
            _eventId = _eventCounter++;
            _tick = tick;
            _data = eventData;
            #if Server
            TickToDeleteTheEvent = NetworkManager.Singleton.IsServer ? NetRunner.EventPackageLossToAccountFor : 0;
            #endif
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _eventId);
            serializer.SerializeValue(ref _tick);
            
            #region Serialize event data
            if (serializer.IsReader)
            {
                // Reader
                int stateType = 0;
                serializer.SerializeValue(ref stateType); // Read the state type
                
                _data = DataTypeRegistry.Create(stateType);
                if (_data != null)
                    _data.NetworkSerialize(serializer);
            }
            else
            {
                // Writer
                int stateType = DataTypeRegistry.GetId(_data.GetType());
                serializer.SerializeValue(ref stateType);
                
                _data.NetworkSerialize(serializer);
            }
            #endregion
        }
    }
}