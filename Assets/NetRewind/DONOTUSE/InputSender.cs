using System.Collections.Generic;
using NetRewind.Utils;
using Unity.Netcode;

namespace NetRewind.DONOTUSE
{
    public class InputSender : NetworkBehaviour
    {
        #if Client
        private static InputSender local;
        
        private static Dictionary<uint, ClientInputState> inputs = new Dictionary<uint, ClientInputState>();

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
                local = this;
        }

        public override void OnNetworkDespawn()
        {
            if (local == this)
                local = null;
        }

        public static void SendInputs(uint _)
        {
            local.SendInputs();
        }
        
        private void SendInputs()
        {
            if (IsHost)
            {
                // Skip
            }
            else
            {
                // Get the current and last inputs
                ClientInputState[] inputsToSent = InputCollector.GetInputStatesToSend(10);
            
                // Check if the list has content to save bandwidth
                if (inputsToSent.Length == 0) return;

                // Send the inputs to the server
                OnClientInputsRPC(inputsToSent);
            }
        }
        #endif

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void OnClientInputsRPC(ClientInputState[] clientInputs)
        {
            #if Server
            foreach (var clientInput in clientInputs)
            {
                // Check if this input is in the future and if we don't already have this input
                if (clientInput.Tick < NetworkRunner.Runner.CurrentTick) continue;
                if (inputs.ContainsKey(clientInput.Tick)) continue;
                
                // Add the input to list
                inputs.Add(clientInput.Tick, clientInput);
            }
            #endif
        }
    }
}