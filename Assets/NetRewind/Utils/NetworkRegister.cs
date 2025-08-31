using System.Collections.Generic;

namespace NetRewind.Utils
{
    public static class NetworkRegister
    {
        private static Dictionary<ulong, NetworkEntity> presentNetworkEntities = new Dictionary<ulong, NetworkEntity>(); // Unique Network Id to Entity
        private static Dictionary<ulong, ulong> allNetworkEntities = new Dictionary<ulong, ulong>(); // Unique to Object Network Id

        public static void Register(ulong uniqueDeterministicNetworkId, ulong networkObjectId,
            NetworkEntity networkEntity)
        {
            presentNetworkEntities.Add(uniqueDeterministicNetworkId, networkEntity);
            allNetworkEntities.Add(uniqueDeterministicNetworkId, networkObjectId);
        }

        public static void Unregister(ulong uniqueDeterministicNetworkId)
        {
            presentNetworkEntities.Remove(uniqueDeterministicNetworkId);
        }
    }
}