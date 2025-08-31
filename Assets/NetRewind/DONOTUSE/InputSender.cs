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
            if (input != null)
                return input;
            else
                return null; // Todo: try to get the last input (if possible)
            TODO!
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