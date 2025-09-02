using Unity.Netcode;

namespace NetRewind.DONOTUSE
{
    public class GameStateSync : NetworkBehaviour
    {
        public static uint latestReceavedServerGameStateTick; // Todo: set this
        
        #if Server
        public static void SendGameState(uint _) 
        {
            
        }
        #endif
    }
}