using System;
using System.Collections.Generic;
using NetRewind.Utils;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.DONOTUSE
{
    public class GameStateSync : NetworkBehaviour
    {
        #if Client
        public static uint latestReceavedServerGameStateTick;
        private static bool isReconciling;
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
            if (gameState.States == null) return;
            if (gameState.States.Count == 0) return;
            
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
            // Savety check and setting the latestReceavedServerGameStateTick
            if (gameState == null) return;
            if (gameState.States == null) return;
            if (gameState.Tick <= latestReceavedServerGameStateTick) return;
            latestReceavedServerGameStateTick = gameState.Tick;

            ProcessGameState(gameState);
            #endif
        }
        
        #if Client
        private void SaveServerGameState(GameState gameState)
        {
            GameState listGameState = gameStates[gameState.Tick % gameStates.Length];

            foreach (var kvp in gameState.States)
            {
                ulong id = kvp.Key;
                var state = kvp.Value;

                // Override our gamestate with the gameState we got from the Server
                listGameState.States[id] = state;
            }
        }
        
        private void ProcessGameState(GameState gameState)
        {
            if (NetworkRunner.Runner.PredictionType == PredictionType.All)
            {
                // Apply every gameState and recalculate to the current tick.
                Reconcile(gameState);
            }
            else
            {
                // Update entity states that aren't predicted entity's and check if predicted entity's need a reconciliation.
                // If they do, request the full GameState and reconcile everyting until we are back at the current tick.
                if (isReconciling) return;
            
                // Safety checks
                GameState localGameState = gameStates[gameState.Tick % gameStates.Length];
                if (localGameState == null) return;
                if (localGameState.States == null) return;
                if (localGameState.States.Count == 0) return;
                
                Dictionary<NetworkEntity, IState> nonPredictiveStates = new Dictionary<NetworkEntity, IState>();
                Dictionary<PredictedNetworkEntity, IState> predictedClientStates = new Dictionary<PredictedNetworkEntity, IState>();
                Dictionary<PredictedNetworkEntity, IState> serverStates = new Dictionary<PredictedNetworkEntity, IState>();
    
                // Sort all server game state states into predictive and non-predictive states
                foreach (var kvp in gameState.States)
                {
                    ulong objectId = kvp.Key;
                    var serverState = kvp.Value;
                    
                    // Check if this object exists in our hierarchy. It may already be destroyed
                    if (!NetworkRegister.IsRegistered(objectId)) continue;
                    
                    NetworkEntity entity = NetworkRegister.GetNetworkEntityFromId(objectId);
                    bool isPredicted = false;
                    if (entity is PredictedNetworkEntity)
                    {
                        // use predictedNetworkEntity
                        PredictedNetworkEntity predictedEntity = entity as PredictedNetworkEntity;
                        if (predictedEntity.IsPredicted)
                            isPredicted = true;
                    }
    
                    if (isPredicted)
                    {
                        PredictedNetworkEntity predictedEntity = entity as PredictedNetworkEntity;
                        IState predictedState = localGameState.States[objectId];
                        predictedClientStates.Add(predictedEntity, predictedState);
                        serverStates.Add(predictedEntity, serverState);
                    }
                    else
                    {
                        // Add the non-predicted entity along the state of the server to the dictionary
                        nonPredictiveStates.Add(entity, serverState);
                    }
                }
            
                // Apply All non-predictive states
                UpdateEntityWithState(gameState.Tick, nonPredictiveStates, false);
            
                // Check if we need to reconcile
                if (ReconciliationNeeded(gameState.Tick, predictedClientStates, serverStates))
                {
                    // Initialize reconciliation
                    isReconciling = true;
                    OnGameStateRequest(gameState.Tick);
                }
            }
        }

        private void UpdateEntityWithState(uint tick, Dictionary<NetworkEntity, IState> states, bool force)
        {
            GameState listGameState = gameStates[tick % gameStates.Length];
            
            foreach (var kvp in states)
            {
                var entity = kvp.Key;
                var state = kvp.Value;
                
                if (force)
                    entity.SetState(tick, state);
                else
                    entity.StateUpdate(tick, state);

                // Update the game state we saved
                listGameState.States[entity.UniqueDeterministicId] = state;
            }
        }

        private bool ReconciliationNeeded(uint tick, Dictionary<PredictedNetworkEntity, IState> predictedStates,
            Dictionary<PredictedNetworkEntity, IState> serverStates)
        {
            foreach (var kvp in predictedStates)
            {
                var entity = kvp.Key;
                var predictedState = kvp.Value;
                var serverState = serverStates[entity];

                // Check if the predicted state matches / is in tolerance to the serverState
                if (entity.DoWeNeedToReconcile(tick, predictedState, serverState))
                    return true;
            }
            
            return false;
        }

        private void Reconcile(GameState serverState)
        {
            // Apply the server State and for every object, that isn't
            
            // Recalculate every tick between the serverState.Tick to the NetworkRunner.Runner.CurrentTick
        }
        #endif
        
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void OnGameStateRequest(uint tick)
        {
            #if Server
            OnGameStateResponse(gameStates[tick % gameStates.Length]);
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void OnGameStateResponse(GameState gameState)
        {
            isReconciling = false;
            
            // Save the game state
            gameStates[gameState.Tick % gameStates.Length] = gameState;
            
            Reconcile(gameState);
        }
    }
}