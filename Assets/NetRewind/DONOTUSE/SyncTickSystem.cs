using System.Collections.Generic;
using Unity.Netcode;

namespace NetRewind.DONOTUSE
{
    public class SyncTickSystem : NetworkBehaviour
    {
        #if Server
        private static List<SyncTickSystem> instances = new List<SyncTickSystem>();

        public override void OnNetworkSpawn()
        {
            instances.Add(this);
        }
        
        public override void OnNetworkDespawn()
        {
            instances.Remove(this);
        }

        public static void UpdateSystem(uint _)
        {
            uint serverTick = NetworkRunner.Runner.CurrentTick;
            
            foreach (var instance in instances)
                instance.OnSyncInfoRPC(serverTick);
        }
        #endif
        
        #if Client
        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void OnSyncInfoRPC(uint simulationTick)
        {
            
        }
        #endif
    }
}