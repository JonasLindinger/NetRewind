using System.Collections.Generic;
using Unity.Netcode;

namespace NetRewind.DONOTUSE
{
    public class GameStateSync : NetworkBehaviour
    {
        #if Client
        public static uint latestReceavedServerGameStateTick; // Todo: set this
        #endif
        
        #if Server
        private static List<GameStateSync> clients = new List<GameStateSync>();
        #endif

        public override void OnNetworkSpawn()
        {
            #if Server
            clients.Add(this);
            #endif
        }

        public override void OnNetworkDespawn()
        {
            #if Server
            clients.Remove(this);
            #endif
        }

        #if Server
        public static void SendGameState(uint _) 
        {
            foreach (var client in clients)
            {
                
            }
        }
        #endif
    }
}