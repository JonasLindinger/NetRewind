using NetRewind.Utils.CustomDataStructures;

namespace NetRewind
{
    public interface IInputDataSource
    {
        #if Client
        public IData OnInputData();
        #endif
    }
}