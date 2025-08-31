using Unity.Netcode;

namespace NetRewind.Utils
{
    public interface IData : INetworkSerializable
    {
        int GetDataType();
    }
}