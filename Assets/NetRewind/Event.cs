using NetRewind.Utils.CustomDataStructures;
using Unity.Netcode;

namespace NetRewind
{
    public struct Event : INetworkSerializable
    {
        private const ushort MaxEventsCoExisting = ushort.MaxValue;
        private static uint _eventCounter;

        // Getter
        /// <summary>
        /// Event id's is recycled! So don't rely on this to be unique. They are unique, but not forever! After the amount of the variable "MaxEventsCoExisting" every id get's recycled.
        /// </summary>
        public ushort EventId => _eventId;
        public uint Tick => _tick;
        public IData Data => _data;
        
        private ushort _eventId;
        private uint _tick;
        private IData _data;
        private uint _sentAmount; // The amount of time this event has been sent.
        
        public Event(uint tick, IData eventData)
        {
            _eventCounter++;
            _eventCounter = _eventCounter % MaxEventsCoExisting;

            _eventId = (ushort)_eventCounter;
            _tick = tick;
            _data = eventData;

            _sentAmount = 0;
        }
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _eventId);
            serializer.SerializeValue(ref _tick);
            
            #region Serialize event data
            if (serializer.IsReader)
            {
                // Reader
                ushort stateType = 0;
                serializer.SerializeValue(ref stateType); // Read the state type
                
                _data = DataTypeRegistry.Create(stateType);
                if (_data != null)
                    _data.NetworkSerialize(serializer);
            }
            else
            {
                // Writer
                ushort stateType = DataTypeRegistry.GetId(_data.GetType());
                serializer.SerializeValue(ref stateType);
                
                _data.NetworkSerialize(serializer);
            }
            #endregion
        }
    }
}