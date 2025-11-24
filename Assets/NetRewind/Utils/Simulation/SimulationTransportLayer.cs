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
        private uint _inputSendingTickOffset = 0;
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
            if (inputTransportLayer != null)
            {
                _inputSendingTickOffset = (byte) inputTransportLayer.SendingMode;
                _inputSendingTickOffset *= inputTransportLayer.InputPackageLoss;
            }
            else Debug.LogWarning("No InputTransportLayer found! -> no input will be sent.");

            uint maxTicksTheClientIsAllowedToBeAhead =
                (uint)Mathf.Max((int)(NetRunner.GetInstance().TicksPassedBetweenServerAndClientRPC(Simulation.TickRate) / 2), 3);
            uint headRoom = maxTicksTheClientIsAllowedToBeAhead / 2;
            uint localTargetTick =
                serverTick +
                NetRunner.GetInstance().TicksPassedBetweenServerAndClientRPC(TickRate) +
                _inputSendingTickOffset;
            
            localTargetTick += headRoom;

            TickRate = tickRate;
            Simulation.StartTickSystem(tickRate, localTargetTick);
            
            if (inputTransportLayer != null)
                inputTransportLayer.StartInputSending(tickRate);
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void SendTickToClientRPC(uint serverTick)
        {
            #if Client
            if (TickRate == 0) return;

            uint localTargetTick =
                serverTick +
                NetRunner.GetInstance().TicksPassedBetweenServerAndClientRPC(TickRate) +
                _inputSendingTickOffset;

            int difference = (int) (localTargetTick - Simulation.CurrentTick); // Positive means we are behind, negative means we are ahead.
            uint absDifference = (uint) Mathf.Abs(difference);

            uint maxTicksTheClientIsAllowedToBeAhead =
                (uint)Mathf.Max((int)(NetRunner.GetInstance().TicksPassedBetweenServerAndClientRPC(Simulation.TickRate) / 2), 3);

            uint headRoom = maxTicksTheClientIsAllowedToBeAhead / 2;
            
            if (difference > 0)
            {
                // Skip to the server tick if we have to calculate too many ticks to get to the server tick
                if (absDifference > 20) // Todo: Make the 20 configurable
                {
                    Debug.LogWarning("Setting tick, because we are too far behind the server");
                    Simulation.SetTick(localTargetTick + headRoom);
                }
                // Calculate extra ticks if the difference to the server tick isn't that big
                else
                {
                    Debug.LogWarning("Calculating extra ticks, because we are a bit behind the server");
                    Simulation.CalculateExtraTicks(absDifference + headRoom);
                }
            }
            else if (difference < -maxTicksTheClientIsAllowedToBeAhead) // Check if we are too far ahead.
            {
                // Skip to the server tick if we have to calculate too many ticks to get to the server tick
                if (absDifference > 20) // Todo: Make the 20 configurable
                {
                    Debug.LogWarning("Setting tick, because we are too far in front of the server");
                    Simulation.SetTick(localTargetTick + headRoom);
                }
                // Calculate extra ticks if the difference to the server tick isn't that big
                else
                {
                    Debug.LogWarning("Skipping ticks, because we are a bit in front of the server");
                    Simulation.SkipTicks(absDifference - headRoom);
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