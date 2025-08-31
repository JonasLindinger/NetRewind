using System.Collections.Generic;
using NetRewind.Utils;
using Unity.Netcode;

namespace NetRewind.DONOTUSE
{
    public class InputSender : NetworkBehaviour
    {
        #if Server
        private static Dictionary<ulong, InputSender> clients = new Dictionary<ulong, InputSender>(); // OwnerClientId - InputSender
        
        private ClientInputState[] inputs;
        #endif
        
        #if Client
        private static InputSender local;
        #endif
        
        public override void OnNetworkSpawn()
        {
            #if Server
            clients.Add(OwnerClientId, this);
            #endif
            
            #if Client
            inputs = new ClientInputState[NetworkRunner.Runner.InputBufferOnServer];
            
            if (IsOwner)
                local = this;
            #endif
        }

        public override void OnNetworkDespawn()
        {
            #if Server
            clients.Remove(OwnerClientId);
            #endif
            
            #if Client
            if (local == this)
                local = null;
            #endif
        }
        
        #if Server
        public static ClientInputState GetInputFromClient(ulong ownerClientId, uint tick)
        {
            var inputs = clients[ownerClientId].inputs;
            ClientInputState input = inputs[tick % inputs.Length];
            
            if (IsValidInput(tick, input))
                return input; // Found a valid input
            else
            {
                // Check the last input and try to repeat it.
                uint oldTick = tick - 1;
                input = inputs[oldTick % inputs.Length];
                if (IsValidInput(oldTick, input))
                {
                    // Is a valid input to use
                        
                    // Repeat the old input and save it for the current tick
                    clients[ownerClientId].inputs[tick % inputs.Length] = input;
                        
                    // Return the (old repeated) input
                    return input;
                }
                else
                {
                    // No input found
                }
            }
            
            return null;
        }

        private static bool IsValidInput(uint tick, ClientInputState input)
        {
            // If input is null, input isn't valid
            if (input == null) return false;
            
            // if the input tick is not the tick we want, input isn't valid
            if (input.Tick != tick) return false;
            
            return true; // Input is valid
        }
        
        #endif
        
        #if Client
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

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void OnClientInputsRPC(ClientInputState[] clientInputs)
        {
            #if Server
            foreach (var clientInput in clientInputs)
            {
                // Check if this input is in the future and if we don't already have this input
                if (clientInput.Tick < NetworkRunner.Runner.CurrentTick) continue;
                
                // TryAdd (if it doesn't exist, add it)
                inputs[clientInput.Tick % inputs.Length] = clientInput;
            }
            #endif
        }
    }
}