using System.Collections.Generic;
using NetRewind.Utils;
using Unity.Netcode;
using UnityEngine;

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

        private static GameState[] gameStates;
        private static uint latestSavedGameStateTick;

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
        public static void SaveGameState(uint tick)
        {
            #region Create GameState List
            
            if (gameStates == null)
            {
                int size = 128;
                #if Server && Client
                size = (int) Mathf.Max(NetworkRunner.Runner.StateBufferOnClient, NetworkRunner.Runner.StateBufferOnServer);
                #elif Server
                size = (int) NetworkRunner.Runner.StateBufferOnServer;
                #elif Client
                size = (int) NetworkRunner.Runner.StateBufferOnClient;
                #endif

                gameStates = new GameState[size];
            }
            
            #endregion
            
            GameState gameState = new GameState();
            gameState.Tick = tick;
            
            var entities = NetworkRegister.GetRegisteredEntities();

            foreach (var kvp in entities)
            {
                ulong id = kvp.Key;
                var entity = kvp.Value;

                IState state = entity.GetCurrentState();
                
                gameState.States.Add(id, state);
            }
            
            // Save in circular buffer
            gameStates[gameState.Tick % gameStates.Length] = gameState;
            latestSavedGameStateTick = gameState.Tick;
        }
        
        private static GameState GetGameStateToSend()
        {
            if (gameStates == null) return null;
            
            // Duplicate the game state from the array, because otherwise we would change the array.
            GameState listGameState = gameStates[latestSavedGameStateTick % gameStates.Length];
            var rawGameState = new GameState();
            listGameState.Clone(rawGameState);
            
            foreach (var kvp in rawGameState.States)
            {
                ulong id = kvp.Key;
                var state = kvp.Value;

                var entity = NetworkRegister.GetNetworkEntityFromId(id);
                
                bool shouldUpdate = latestReceavedServerGameStateTick % (byte) entity.SyncType == 0;
                if (!shouldUpdate)
                    rawGameState.States.Remove(id);
            }

            return rawGameState;
        }
        
        public static void SendGameState(uint _)
        {
            GameState gameState = GetGameStateToSend();
            
            if (gameState == null) return;
            
            foreach (var client in clients)
            {
                #if Client && Server
                if (client.IsServer) continue; // Check if this is the host.
                #endif
                client.OnGameStateRPC(gameState);
            }
        }
        #endif

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Unreliable)] // Todo: think about making this reliable or maybe an option...?
        private void OnGameStateRPC(GameState gameState)
        {
            #if Client
            // Todo:
            #endif
        }
    }
}