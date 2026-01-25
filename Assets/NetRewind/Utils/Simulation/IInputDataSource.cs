using NetRewind.Utils.Input.Data;

namespace NetRewind.Utils.Simulation
{
    public interface IInputDataSource
    {
        #if Client
        public IData OnInputData();
        #endif
    }
}