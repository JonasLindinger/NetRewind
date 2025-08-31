using Unity.Netcode;

namespace NetRewind.Utils
{
    public interface IState : INetworkSerializable
    {
        int GetStateType();
    }
}