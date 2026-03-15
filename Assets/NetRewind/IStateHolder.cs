using NetRewind.Utils.CustomDataStructures;

namespace NetRewind
{
    public interface IStateHolder
    {
        public IState GetCurrentState();
        public void UpdateState(IState state);
        public void ApplyState(IState state);
        public void ApplyPartialState(IState state, uint part);
    }
}