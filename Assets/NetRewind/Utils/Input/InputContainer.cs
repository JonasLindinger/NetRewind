using System;
using NetRewind.Utils.CustomDataStructures;

namespace NetRewind.Utils.Input
{
    public static class InputContainer
    {
        #if Client
        private const uint InputBufferSize = 1024; // Todo: Configurable
        private static CircularBuffer<InputState> InputBuffer = new CircularBuffer<InputState>(InputBufferSize);
        public static bool CollectedInputs { get; private set;} = false;

        public static void Collect(uint tick)
        {
            if (InputSender.GetInstance() == null) return;

            byte[] source = InputSender.GetInstance().CollectInput();
            
            // Collect the current input state
            InputState inputState = new InputState()
            {
                Tick = tick,
                Input = (byte[]) source.Clone(),
            };
            
            // Store the input state
            InputBuffer.Store(inputState.Tick, inputState);

            CollectedInputs = true;
        }

        public static InputState[] GetInputsToSend(uint amount)
        {
            // Todo: Account for latency and packet loss.
            return InputBuffer.GetLatestItems(amount);
        }

        public static InputState GetInput(uint tick) => InputBuffer.Get(tick);
        #endif
    }
}