using System;
using System.Collections.Generic;
using NetRewind.Utils.CustomDataStructures;

namespace NetRewind.Utils.Simulation.State
{
    public static class SnapshotContainer
    {
        public const uint SnapshotBufferSize = 1024; // Todo: Configurable
        
        private static CircularBuffer<Snapshot> _snapshots = new CircularBuffer<Snapshot>(SnapshotBufferSize);
        private static uint _latestTakenSnapshotTick;
        
        public static void TakeSnapshot(uint tick)
        {
            Snapshot snapshot = GetCurrentSnapshot(tick);
            
            _snapshots.Store(tick, snapshot);
            _latestTakenSnapshotTick = tick;
        }

        public static Snapshot GetLatestSnapshot()
        {
            Snapshot snapshot = _snapshots.Get(_latestTakenSnapshotTick);
            return snapshot;
        }

        private static Snapshot GetCurrentSnapshot(uint tick)
        {
            Snapshot snapshot = new Snapshot(tick);

            foreach (var kvp in RegisteredNetworkObject.NetworkObjects)
            {
                ulong networkId = kvp.Key;
                RegisteredNetworkObject networkedObject = kvp.Value;

                try
                {
                    IState state = networkedObject.GetSnapshotState(tick);
                    snapshot.States.Add(networkId, state);
                }
                catch (NotImplementedException e)
                {
                    // We found a stateless object. Ignore it.'
                }
            }
            
            return snapshot;
        }
    }
}