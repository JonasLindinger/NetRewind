using NetRewind.Utils.CustomDataStructures;
using NetRewind.Utils.Simulation;

namespace NetRewind.Utils.Input
{
    public static class InputContainer
    {
        #if Client
        private const uint InputBufferSize = 1024; // Todo: Configurable
        private static CircularBuffer<InputState> _inputBuffer = new CircularBuffer<InputState>(InputBufferSize);
        public static bool CollectedInputs { get; private set;} = false;

        private static byte[] _playerInputSource;
        
        public static void Init()
        {
            _playerInputSource = InputSender.GetInstance().CollectInput();
        }
        
        public static void Collect(uint tick)
        {
            if (InputSender.GetInstance() == null) return;
            
            // Collect the current input state
            InputState inputState = new InputState(
                tick, 
                (byte[]) _playerInputSource.Clone(),
                NetObject.GetPlayerInputData()
            );
            
            // Store the input state
            _inputBuffer.Store(inputState.Tick, inputState);

            CollectedInputs = true;
        }

        public static InputState[] GetInputsToSend(uint amount)
        {
            return _inputBuffer.GetLatestItems(amount);
        }

        public static InputState GetInput(uint tick) => _inputBuffer.Get(tick);
        #endif
    }
}