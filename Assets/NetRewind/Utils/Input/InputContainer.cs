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

            // Collect the current input state
            InputState inputState = new InputState()
            {
                Tick = tick,
                Input = InputSender.GetInstance().CollectInput(),
            };
            
            // Store the input state
            InputBuffer.Store(inputState.Tick, inputState);

            CollectedInputs = true;
        }

        public static InputState[] GetInputsToSend()
        {
            // Todo: Account for latency and packet loss.
            return InputBuffer.GetLatestItems(10);
        }
        #endif
    }
}