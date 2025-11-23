using System;
using NetRewind.Utils.Input;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public class SimulationTransportLayer : TickSystem
    {
        [Header("Input")]
        [SerializeField] private InputTransportLayer inputTransportLayer;
        
        #if Client
        public uint TickRate { get; private set; }
        #endif

        public override void OnNetworkSpawn()
        {
            #if Server
            if (!IsOwner)
            {
                StartClientTickSystemRPC(Simulation.TickRate, Simulation.CurrentTick);
                int syncTickTickRate = 1;
                StartTickSystem(syncTickTickRate); // How often per second to sync the tick (send the tick to the client)
            }
            #endif
        }

        protected override void OnTick()
        {
            #if Server
            SendTickToClientRPC(Simulation.CurrentTick);
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void StartClientTickSystemRPC(uint tickRate, uint serverTick)
        {
            #if Client
            uint theServerTickNow = serverTick + NetRunner.GetInstance().TicksPassedBetweenServerAndClientRPC(tickRate);
            
            Simulation.StartTickSystem(tickRate, theServerTickNow);
            TickRate = tickRate;
            if (inputTransportLayer != null)
                inputTransportLayer.StartInputSending(tickRate);
            else Debug.LogWarning("No InputTransportLayer found! -> no input will be sent.");
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void SendTickToClientRPC(uint serverTick)
        {
            #if Client
            if (TickRate == 0) return; // Not enough info. & Tick System hasn't started yet.
            uint currentTick = Simulation.CurrentTick;
            uint theServerTickNow = serverTick + NetRunner.GetInstance().TicksPassedBetweenServerAndClientRPC(TickRate);

            int difference = (int)currentTick - (int)theServerTickNow;
            uint absDifference = (uint) Mathf.Abs(difference);
            uint maxTicksTheClientIsAllowedToBeAhead = (uint) Mathf.Max((int) (NetRunner.GetInstance().ServerRTT / 2), 3); // Must be at least 3 ticks ahead of the server.
            
            if (difference < 0)
            {
                // Skip to the server tick if we have to calculate too many ticks to get to the server tick
                if (absDifference > 6) // Todo: Make the 6 configurable
                {
                    Debug.LogWarning("Setting tick, because we are too far behind the server");
                    Simulation.SetTick(theServerTickNow + (uint) maxTicksTheClientIsAllowedToBeAhead);
                }
                // Calculate extra ticks if the difference to the server tick isn't that big
                else
                {
                    Debug.LogWarning("Calculating extra ticks, because we are a bit behind the server");
                    Simulation.CalculateExtraTicks(absDifference + maxTicksTheClientIsAllowedToBeAhead, false);
                }
            }
            else if (difference > maxTicksTheClientIsAllowedToBeAhead) // Check if we are too far ahead.
            {
                // Skip to the server tick if we have to calculate too many ticks to get to the server tick
                if (absDifference > 6) // Todo: Make the 6 configurable
                {
                    Debug.LogWarning("Setting tick, because we are too far in front of the server");
                    Simulation.SetTick(theServerTickNow + (uint) maxTicksTheClientIsAllowedToBeAhead);
                }
                // Calculate extra ticks if the difference to the server tick isn't that big
                else
                {
                    Debug.LogWarning("Skipping ticks, because we are a bit in front of the server");
                    Simulation.SkipTicks(absDifference + 1, false);
                }
            }
            else
            {
                // Do nothing, because we are in the sweet spot of tick offset.
                // Debug.Log("We are in the sweet spot");
            }
            
            #endif
        }
    }
}