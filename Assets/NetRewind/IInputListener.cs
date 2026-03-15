using NetRewind.Utils.CustomDataStructures;

namespace NetRewind
{
    public interface IInputListener
    {
        public byte[] InputData { get; set; }
        public IData Data { get; set; }
        public uint TickOfTheInput { get; set; }
        public void NetInputOwnerUpdate();
    }
}