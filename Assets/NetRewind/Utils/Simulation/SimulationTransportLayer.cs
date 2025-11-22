using NetRewind.Utils.Input;

namespace NetRewind.Utils.Simulation
{
    public class SimulationTransportLayer : TickSystem
    {
        private uint _currentTick;
        public int TickRate { get; private set; } = 60;

        private void Start()
        {
            StartTickSystem(TickRate);
        }

        protected override void OnTick()
        {
            _currentTick++;
            
            #if Client
            InputContainer.Collect(_currentTick);
            #endif
        }
    }
}