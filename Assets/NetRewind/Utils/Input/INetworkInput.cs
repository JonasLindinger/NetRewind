using Unity.Netcode;

namespace NetRewind.Utils.Input
{
    public interface INetworkInput
    {
        /// <summary> Encode this input into a byte array. </summary>
        public byte[] Encode();

        /// <summary> Decode this input from a byte array. </summary>
        public void Decode(byte[] data);
    }
}