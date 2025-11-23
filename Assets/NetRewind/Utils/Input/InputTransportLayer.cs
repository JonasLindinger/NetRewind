using System;
using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Simulation;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Input
{
    [RequireComponent(typeof(SimulationTransportLayer))]
    public class InputTransportLayer : TickSystem
    {
        private const uint MaxInputPackageSize = 20; // Todo: Configurable
        
        #if Client
        public InputSendingMode SendingMode => sendingMode;
        public uint InputPackageLoss => inputPackageLoss;
        #endif
        
        [Header("Input sending")]
        [SerializeField] private InputSendingMode sendingMode = InputSendingMode.Full;
        [SerializeField] private uint inputPackageLoss = 4;
        
        #if Server
        private const uint LocalInputBufferSize = 1024;
        
        private const uint InputCheckAmount = 5; // Every [InputCheckAmount]th input package is checked for validity. // Todo: Configurable
        private uint _inputsReceived;
        
        private CircularBuffer<InputState> _inputBuffer;
        #endif
        
        #if Client
        private SimulationTransportLayer _transportLayer;
        private uint _amountOfInputsToSend;
        #endif
        
        [HideInInspector] public NetworkVariable<bool> isAllowedToSendInputs;

        private void Start()
        {
            #if Server
            if (IsServer)
                isAllowedToSendInputs.Value = true;
            
            _inputBuffer = new CircularBuffer<InputState>(LocalInputBufferSize);
            #endif
        }

        public void StartInputSending(uint simulationTickRate)
        {
            #if Client
            if (!IsOwner) return;
            _transportLayer = GetComponent<SimulationTransportLayer>();
            int tickRate = Mathf.CeilToInt(simulationTickRate / (byte) sendingMode) ;
            StartTickSystem(tickRate);
            _amountOfInputsToSend = (byte)sendingMode * inputPackageLoss;
            #endif
        }

        protected override void OnTick()
        {
            #if Client
            InitiateSendingInputPackage();
            #endif
        }
        
        #if Client
        private void InitiateSendingInputPackage()
        {
            if (IsHost) return; // We don't want to send input packages to us.
            if (!isAllowedToSendInputs.Value) return; // We aren't allowed to send input packages.
            if (!InputContainer.CollectedInputs) return;

            // Get input states to send.
            InputState[] statesToSend = InputContainer.GetInputsToSend(_amountOfInputsToSend);
            
            // Send input package to the server
            SendInputPackageRPC(statesToSend);
        }
        #endif
        
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendInputPackageRPC(InputState[] inputs)
        {
            #if Server
            // Validate the input package every [InputCheckAmount]th time.
            if (_inputsReceived % InputCheckAmount != 0 && !IsValidInputPackage(inputs))
            {
                // This input package is invalid.
                // Todo: Punish the player.
            }
            
            _inputsReceived++;

            // Save the inputs.
            foreach (InputState input in inputs)
                _inputBuffer.Store(input.Tick, input);
            
            #endif
        }
        
        private bool IsValidInputPackage(InputState[] inputs)
        {
            if (inputs.Length == 0) return false;   
            if (inputs.Length > MaxInputPackageSize) return false;
            return true;
        }
    }
}