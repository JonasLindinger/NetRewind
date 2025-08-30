using System;
using Unity.Netcode;

namespace NetRewind.DONOTUSE
{
    public class NetworkClientConnection : NetworkBehaviour
    {
        #if Client
        public static event Action<uint> OnStartTickSystem = delegate { };
        #endif
        
        public override void OnNetworkSpawn()
        {
            #if Server
            if (IsServer)
            {
                // If this is not the host, start the client tick system
                if (!IsOwner) 
                    OnStartClientTickSystemRPC(NetworkRunner.Runner.CurrentTick);
            }
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void OnStartClientTickSystemRPC(uint simulationTickOffset)
        {
            #if Client
            ulong ms = NetworkRunner.Runner.GetRTTToServer() / 2;
            float msPerTick = 1000f / NetworkRunner.Runner.SimulationTickRate;
            int passedTicks = (int)(ms / msPerTick);
            
            uint simulationTickOffsetWithBuffer = (uint) (simulationTickOffset + passedTicks);
            simulationTickOffsetWithBuffer += NetworkRunner.Runner.ClientServerOffsetBuffer; // Add an offset, just for possible future jitter
            
            OnStartTickSystem?.Invoke(simulationTickOffsetWithBuffer);
            #endif
        }
    }
}