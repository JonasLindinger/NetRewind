using System.Collections.Generic;
using NetRewind.Utils.Input.Data;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;

namespace NetRewind.Utils.Simulation
{
    public struct Event : INetworkSerializable
    {
        private const ushort MaxEventsCoExisting = 2048;
        private static uint _eventCounter = 1;

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

        #if Server
        // For the server, no need to serialize
        public uint TickToDeleteTheEvent { get; private set; }
        #endif
        
        public Event(uint tick, IData eventData)
        {
            _eventId = (ushort) (MaxEventsCoExisting % _eventCounter++);
            _tick = tick;
            _data = eventData;
            #if Server
            TickToDeleteTheEvent = NetworkManager.Singleton.IsServer ? NetRunner.EventPackageLossToAccountFor : 0; // Todo: Maybe multiply it by the sending mode!?
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