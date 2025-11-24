using System.Collections.Generic;
using Unity.Netcode;

namespace NetRewind.Utils.Simulation
{
    public abstract class RegisteredNetworkObject : NetworkBehaviour
    {
        private static List<RegisteredNetworkObject> _networkObjects = new List<RegisteredNetworkObject>();
        
        public override void OnNetworkSpawn()
        {
            _networkObjects.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            _networkObjects.Remove(this);
        }

        public static void RunTick(uint tick)
        {
            foreach (RegisteredNetworkObject obj in _networkObjects)
                obj.OnTickTriggered(tick);
        }

        protected abstract void OnTickTriggered(uint tick);
    }
}