using System.Collections.Generic;
using NetRewind.Utils.Input.Data;
using Unity.Netcode;
using Unity.VisualScripting;

namespace NetRewind.Utils.Simulation.State
{
    [StateType]
    public struct NetObjectState : IState
    {
        #if Client
        private static Dictionary<uint, uint> _receivedEvents = new Dictionary<uint, uint>(); // EventId -> Tick to remove this event from list
        #endif

        public ushort InputOwnerClientId;
        public Event[] Events;

        public NetObjectState(ushort inputOwnerClientId, Event[] events)
        {
            InputOwnerClientId = inputOwnerClientId;
            Events = events;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InputOwnerClientId);
            serializer.SerializeValue(ref Events);
            
            #if Client
            // Filter out events that have already been received
            if (!NetworkManager.Singleton.IsServer && serializer.IsReader)
            {
                List<Event> eventsForThisState = new List<Event>();

                foreach (var @event in Events)
                {
                    uint eventId = @event.EventId;
                    if (_receivedEvents.ContainsKey(eventId))
                    {
                        if (_receivedEvents[eventId] <= Simulation.CurrentTick)
                            // Unmark this event as received and don't ignore this event
                            _receivedEvents.Remove(eventId);
                        else
                            // Ignore this event
                            continue;
                    }
                    
                    _receivedEvents.Add(@event.EventId, Simulation.CurrentTick + (NetRunner.EventPackageLossToAccountFor * 2));
                    eventsForThisState.Add(@event);
                }

                Events = eventsForThisState.ToArray();
            }
            #endif
        }

        public uint Compare(IState localState, IState serverState)
        {
            NetObjectState local = (NetObjectState) localState;
            NetObjectState server = (NetObjectState) serverState;

            if (local.InputOwnerClientId != server.InputOwnerClientId)
            {
                return 1;
            }

            /* Don't compare this. since it doesn't really make sense
            if (local.Events != server.Events)
            {
                return 1;
            }
            */

            return 0;
        }
    }
}