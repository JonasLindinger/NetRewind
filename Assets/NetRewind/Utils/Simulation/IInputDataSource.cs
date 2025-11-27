using NetRewind.Utils.Input.Data;

namespace NetRewind.Utils.Simulation
{
    public interface IInputDataSource
    {
        public IData OnInputData();
    }
}