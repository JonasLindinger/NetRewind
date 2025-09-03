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
        /// <summary>
        /// the uint is included and is the first tick to run!
        /// </summary>
        public static event Action<uint> OnRecalculateTicks = delegate { };
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
        public static void SaveGameState(uint tick, bool isReconciliation)
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
        
        public static void SendGameState(uint _, bool isReconciliation)
        {
            if (isReconciliation) return;
            
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
                SaveGameStateSoft(gameState);
                
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
                UpdateEntityWithStateAndSave(gameState.Tick, nonPredictiveStates, false);
            
                // Check if we need to reconcile
                if (ReconciliationNeeded(gameState.Tick, predictedClientStates, serverStates))
                {
                    // Initialize reconciliation
                    isReconciling = true;
                    OnGameStateRequest(gameState.Tick);
                }
            }
        }

        private void UpdateEntityWithStateAndSave(uint tick, Dictionary<NetworkEntity, IState> states, bool force)
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

        private void Reconcile(GameState gameState)
        {
            // Get the local GameState on that tick and check if we can use it.
            GameState listGameState = gameStates[gameState.Tick % gameStates.Length];
            bool canUsePredictedGameState = listGameState == null;
            if (canUsePredictedGameState)
            {
                if (listGameState.Tick != gameState.Tick) 
                    canUsePredictedGameState = false;
                if (listGameState.States == null)
                    canUsePredictedGameState = false;
                if (listGameState.States.Count == 0)
                    canUsePredictedGameState = false;
            }
            
            // Apply the server State and for every object, that isn't
            var entities = NetworkRegister.GetRegisteredEntities();
            foreach (var kvp in entities)
            {
                ulong id = kvp.Key;
                var entity = kvp.Value;

                if (gameState.States.ContainsKey(id))
                {
                    // Apply the server state
                    entity.SetState(gameState.Tick, gameState.States[id]);
                }
                else if (canUsePredictedGameState && listGameState.States.ContainsKey(id))
                {
                    // Apply the local predicted state
                    entity.SetState(gameState.Tick, listGameState.States[id]);
                }
                else
                {
                    // No valid state found for that entity!
                    if (NetworkRunner.Runner.DebugMode == DebugMode.All || NetworkRunner.Runner.DebugMode == DebugMode.ErrorsOnly)
                        Debug.LogWarning("Entity state for entity: " + entity.name + " is missing. So reconciliation might fail!");
                }
            }
            
            // Override the local GameState with the Server Game State
            listGameState = gameState;
            
            // Recalculate every tick between the serverState.Tick to the NetworkRunner.Runner.CurrentTick
            OnRecalculateTicks?.Invoke(gameState.Tick + 1);
        }

        /// <summary>
        /// All states in the given GameState will be saved in the local GameState array. But all States that are in the local array, but not in the GameState we got, will still be in the GameState and not cleared.
        /// </summary>
        /// <param name="gameState"></param>
        private void SaveGameStateSoft(GameState gameState)
        {
            GameState listGameState = gameStates[gameState.Tick % gameStates.Length];
            
            // validate
            if (listGameState == null || listGameState.Tick != gameState.Tick)
            {
                listGameState = gameState;
                return;
            }
            
            // Apply the state additive
            foreach (var kvp in gameState.States)
            {
                ulong id = kvp.Key;
                var state = kvp.Value;
                
                // Add the state to the list
                listGameState.States[id] = state;
            }
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
            SaveGameStateSoft(gameState);
            
            // Do the actual reconciliation
            Reconcile(gameState);
        }
    }
}