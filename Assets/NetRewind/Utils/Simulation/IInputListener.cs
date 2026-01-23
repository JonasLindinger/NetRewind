using NetRewind.Utils.Input.Data;

namespace NetRewind.Utils.Simulation
{
    public interface IInputListener
    {
        public byte[] InputData { get; set; }
        public IData Data { get; set; }
        public uint TickOfTheInput { get; set; }
        public void NetInputOwnerUpdate();
    }
}