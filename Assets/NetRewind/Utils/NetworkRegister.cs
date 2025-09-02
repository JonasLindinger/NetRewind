using System.Collections.Generic;

namespace NetRewind.Utils
{
    public static class NetworkRegister
    {
        private static Dictionary<ulong, NetworkEntity> presentNetworkEntities = new Dictionary<ulong, NetworkEntity>(); // Unique Network Id to Entity

        public static NetworkEntity GetNetworkEntityFromId(ulong networkObjectId)
        {
            return presentNetworkEntities[networkObjectId];
        }
        
        public static void Register(ulong uniqueDeterministicNetworkId, ulong networkObjectId,
            NetworkEntity networkEntity)
        {
            presentNetworkEntities.Add(uniqueDeterministicNetworkId, networkEntity);
        }

        public static void Unregister(ulong uniqueDeterministicNetworkId)
        {
            presentNetworkEntities.Remove(uniqueDeterministicNetworkId);
        }

        public static Dictionary<ulong, NetworkEntity> GetRegisteredEntities()
        {
            return presentNetworkEntities;
        }

        public static bool IsRegistered(ulong id)
        {
            return presentNetworkEntities.ContainsKey(id);
        }
    }
}