using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.DONOTUSE
{
    public class SyncTickSystem : NetworkBehaviour
    {
        #if Client
        public static event Action<uint> CalculateExtraTicks = delegate { };
        public static event Action<uint> CalculateLessTicks = delegate { };
        public static event Action<uint> SetTick = delegate { };
        #endif
        
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
                if (!instance.IsOwner) // If this is not the host, sync the tick
                    instance.OnSyncInfoRPC(serverTick);
        }
        #endif
        
        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void OnSyncInfoRPC(uint simulationTick)
        {
            #if Client
            if (NetworkRunner.Runner.CurrentTick == 0) return; // Check if we have started the tick system
            
            ulong ms = NetworkRunner.Runner.GetRTTToServer() / 2;
            float msPerTick = 1000f / NetworkRunner.Runner.SimulationTickRate;
            int passedTicks = (int)(ms / msPerTick);

            uint buffer = NetworkRunner.Runner.ClientServerOffsetBuffer;
            
            uint targetSimulationTick = (uint) (simulationTick + passedTicks);
            targetSimulationTick += buffer; // Add an offset, just for possible future jitter

            int difference = (int) (targetSimulationTick - NetworkRunner.Runner.CurrentTick);
            uint absDifference = (uint)Mathf.Abs(difference);
            
            if (difference > 0)
            {
                // Calculate extra ticks
                if (absDifference <= NetworkRunner.Runner.MaxTickRecalculation)
                {
                    // Calculate the extra ticks
                    CalculateExtraTicks?.Invoke(absDifference);
                }
                else
                {
                    // Too big of a difference, so just set the tick
                    SetTick?.Invoke(targetSimulationTick);
                }
            }
            else if (difference < 0)
            {
                // Calculate less ticks, if difference is too big
                if (absDifference <= NetworkRunner.Runner.MaxTickRecalculation)
                {
                    // Skip the ticks
                    uint ticksToSkip = (uint) Mathf.Min(0, absDifference - buffer);
                    if (ticksToSkip != 0)
                        CalculateLessTicks?.Invoke(ticksToSkip);
                }
                else
                {
                    // Too big of a difference, so just set the tick
                    SetTick?.Invoke(targetSimulationTick);
                }
            }
            #endif
        }
    }
}