using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation.State
{
    public class SnapshotTransportLayer : NetworkBehaviour
    {
        #if Server
        private static List<SnapshotTransportLayer> _layers = new List<SnapshotTransportLayer>();
        #endif
        
        #if Client
        private static SnapshotTransportLayer _localInstance;
        private uint _lastReceivedSnapshotTick;
        #endif

        public override void OnNetworkSpawn()
        {
            #if Client
            if (IsOwner)
                _localInstance = this;
            #endif
            
            #if Server
            if (IsServer)
                _layers.Add(this);
            #endif
        }

        public override void OnNetworkDespawn()
        {
            #if Client
            if (IsOwner)
                _localInstance = this;
            #endif
            
            #if Server
            if (IsServer)
                _layers.Remove(this);
            #endif
        }

        #region Requesting a snapshot
        
        #if Client
        public static void RequestSnapshot()
        {
            if (_localInstance == null)
            {
                Debug.LogWarning("Failed to request snapshot. No local instance of SnapshotTransportLayer found.");
                return;
            }

            _localInstance.RequestSnapshotRPC();
        }
        #endif

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void RequestSnapshotRPC()
        {
            #if Server
            try 
            {
                Snapshot snapshot = SnapshotContainer.GetLatestSnapshot();

                SendSnapshotRPC(snapshot);
            }
            catch (KeyNotFoundException e) 
            {
                Debug.LogWarning("Latest Snapshot not found!");
            }
            #endif
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void SendSnapshotRPC(Snapshot snapshot)
        {
            #if Client
            Simulation.InitReconciliation(snapshot);
            #endif
        }
        
        #endregion

        
        #region Sending states
        #if Server
        public static void SendStates(uint tick, NetObject[] netObjects)
        {
            if (_layers == null)
            {
                Debug.LogWarning("Failed to send states. No local instance of SnapshotTransportLayer found.");
                return;
            }

            Snapshot snapshot = GetSnapshotByObjects(tick, netObjects);
            
            foreach (var layer in _layers)
                layer.SendRegularSnapshotToClientRPC(snapshot);
        }
        #endif
        
        #if Server
        private static Snapshot GetSnapshotByObjects(uint tick, NetObject[] netObjects)
        {
            Snapshot snapshot = new Snapshot(tick);

            foreach (var netObject in netObjects)
            {
                ulong networkId = netObject.NetworkObjectId;
                NetObject networkedObject = netObject;

                try
                {
                    IState state = networkedObject.GetSnapshotState(tick);
                    snapshot.States.Add(networkId, state);
                }
                catch (NotImplementedException e)
                {
                    // We found a stateless object. Ignore it.
                }
            }
            
            return snapshot;
        }
        #endif

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Unreliable)]
        private void SendRegularSnapshotToClientRPC(Snapshot snapshot)
        {
            #if Client
            // Only accept new states!
            if (snapshot.Tick <= _lastReceivedSnapshotTick)
                return;
            
            if (snapshot.Tick > Simulation.CurrentTick)
            {
                // We received a snapshot for a tick that didn't happen yet. We are probably too far behind and should speed up. But this shouldn't be done here! 
                
                // Debug.LogWarning("Received state for tick " + serverObjectState.TickOfTheInput + " but we are at tick " + Simulation.CurrentTick + "! IGNORING / Waiting for tick adjustments!");
                return;
            }
            
            // Check if our buffer is even capable of containing the snapshot at that tick.
            if (snapshot.Tick <= Simulation.CurrentTick - SnapshotContainer.SnapshotBufferSize) 
                return;
            
            // Only accept if we don't already do correction.
            if (Simulation.IsCorrectingGameState) return;
            
            // --- Accept the state ---
            _lastReceivedSnapshotTick = snapshot.Tick;

            HandleServerSnapshot(snapshot);
            #endif
        }
        
        #if Client
        private void HandleServerSnapshot(Snapshot snapshot)
        {
            foreach (var kvp in snapshot.States)
            {
                ulong networkId = kvp.Key;
                IState serverState = kvp.Value;

                try
                {
                    NetObject netObject = NetObject.NetworkObjects[networkId];

                    IState clientState = netObject.GetStateAtTick(snapshot.Tick);
                    
                    bool isReconciling = CompareStates(snapshot.Tick, netObject, clientState, serverState);

                    if (isReconciling)
                    {
                        Snapshot fullClientSnapshot = GetCorrectSnapshot(
                            snapshot, 
                            SnapshotContainer.GetSnapshot(snapshot.Tick)
                            );
                        
                        Simulation.InitReconciliation(fullClientSnapshot);
                    }
                    else
                        break;
                }
                catch (KeyNotFoundException e)
                {
                    // It's probably fine to just Skip
                }
            }
        }

        private Snapshot GetCorrectSnapshot(Snapshot incompleteSnapshot, Snapshot wrongCompleteSnapshot)
        {
            Snapshot correctSnapshot = wrongCompleteSnapshot;
            
            foreach (var kvp in incompleteSnapshot.States)
            {
                ulong networkId = kvp.Key;
                IState serverState = kvp.Value;
                
                correctSnapshot.States.Remove(networkId);
                correctSnapshot.States.Add(networkId, serverState);
            }
            
            return correctSnapshot;
        }
        #endif
        
        #if Client
        private bool CompareStates(uint tick, NetObject netObject, IState localState, IState serverState)
        {
            uint result = localState.Compare(localState, serverState);
            bool correctionIsNormal = netObject.IsPredicted && !netObject.IsOwner; // If the object isn't owned but predicted, we can expect a correction.

            switch (result)
            {
                case (uint) CompareResult.Equal:
                    // Everything is fine.
                    break;
                case (uint) CompareResult.WorldCorrection:
                    // Apply the entire server state and recalculate some ticks to be ahead of the server again.
                    return true;
                    break;
                default:
                    // Apply only a part of the server state.
                    // Don't use try catch here, because if we receive states, we should sync them!
                    netObject.ApplyPartialState(serverState, result);
                    break;
            }

            return false;
        }
        #endif
        
        #endregion
    }
}