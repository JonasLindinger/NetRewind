using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation.State
{
    public class SnapshotTransportLayer : NetworkBehaviour
    {
        #if Client
        private static SnapshotTransportLayer _localInstance;
        #endif

        public override void OnNetworkSpawn()
        {
            #if Client
            if (IsOwner)
                _localInstance = this;
            #endif
        }

        public override void OnNetworkDespawn()
        {
            #if Client
            if (IsOwner)
                _localInstance = this;
            #endif
        }

        #if Client
        public static void RequestSnapshot()
        {
            if (_localInstance == null)
            {
                Debug.LogWarning("Failed to request snapshot. No local instance of SnapshotTransportLayer found.");
                return;
            }

            _localInstance.RequestSnapshotRPC();
        }
        #endif

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void RequestSnapshotRPC()
        {
            #if Server
            try 
            {
                Snapshot snapshot = SnapshotContainer.GetLatestSnapshot();

                SendSnapshotRPC(snapshot);
            }
            catch (KeyNotFoundException e) 
            {
                Debug.LogWarning("Latest Snapshot not found!");
            }
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void SendSnapshotRPC(Snapshot snapshot)
        {
            #if Client
            Simulation.InitReconciliation(snapshot);
            #endif
        }
    }
}