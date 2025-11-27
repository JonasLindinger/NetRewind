using NetRewind.Utils.Simulation.State;

namespace NetRewind.Utils.Simulation
{
    public interface IStateHolder
    {
        public IState GetCurrentState();
        public void UpdateState(IState state);
        public void ApplyState(IState state);
        public void ApplyPartialState(IState state, uint part);
    }
}