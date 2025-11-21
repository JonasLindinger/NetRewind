using NetRewind.Utils.Input;

namespace NetRewind.Utils.Simulation
{
    public class SimulationTransportLayer : TickSystem
    {
        private uint _currentTick;

        private void Start()
        {
            StartTickSystem(60);
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