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
        public static uint LastReceivedSnapshotTick { get; private set; }
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
            Simulation.InitReconciliation(snapshot, false);
            #endif
        }
        
        #endregion

        
        #region Sending states
        #if Server
        public static void SendStates(uint tick, NetObject[] netObjects)
        {
            if (_layers == null)
                throw new Exception("Failed to send states. No local instance of SnapshotTransportLayer found.");

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
                    ObjectState objectState = networkedObject.GetSnapshotState(tick);
                    snapshot.States.Add(networkId, objectState.State);
                    snapshot.NetObjectStates.Add(networkId, objectState.NetObjectState);
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
            if (snapshot.Tick <= LastReceivedSnapshotTick)
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
            LastReceivedSnapshotTick = snapshot.Tick;

            HandleServerSnapshot(snapshot);
            #endif
        }
        
        #if Client
        private void HandleServerSnapshot(Snapshot snapshot)
        {
            Snapshot fullClientSnapshot = new Snapshot(0);
            bool isAboutToReconcile = false;
            bool reconciliationIsExpected = true;
            
            foreach (var kvp in snapshot.States)
            {
                ulong networkId = kvp.Key;
                IState serverState = kvp.Value;
                NetObjectState netObjectServerState = (NetObjectState) snapshot.NetObjectStates[networkId];

                try
                {
                    NetObject netObject = NetObject.NetworkObjects[networkId];

                    IState clientState = netObject.GetStateAtTick(snapshot.Tick);

                    if (netObject.IsPredicted && !isAboutToReconcile)
                    {
                        // Check if we predicted correctly. If we didn't, reconcile
                        bool isReconciling = CompareStates(netObject, clientState, serverState);

                        // Check if we should reconcile
                        if (isReconciling)
                        {
                            if (netObject.IsOwner || !netObject.IsPredicted)
                                reconciliationIsExpected = false;
                            
                            // Merge snapshots (info that we know, but is incomplete (server snapshot) + complete info, but not 100% correct (predicted snapshot)) 
                            // -> good enough snapshot for reconciliation
                            fullClientSnapshot = GetCorrectSnapshot(
                                snapshot,
                                SnapshotContainer.GetSnapshot(snapshot.Tick)
                            );
                            
                            // From now on, only Update non-predicted objects and skip the predicted ones, since we are going to reconcile anyway.
                            isAboutToReconcile = true;
                        }
                    }
                    else
                    {
                        // If the object isn't predicted, we can just update the object
                        
                        // Update state
                        netObject.TryUpdateState(serverState, netObjectServerState);
                    }
                }
                catch (KeyNotFoundException e)
                {
                    // It's probably fine to just Skip
                }
            }
            
            // Do this here, since we want to apply the state of every non-predicted object first.
            if (isAboutToReconcile && fullClientSnapshot.Tick != 0)
                Simulation.InitReconciliation(fullClientSnapshot, reconciliationIsExpected);
        }

        private Snapshot GetCorrectSnapshot(Snapshot incompleteSnapshot, Snapshot wrongCompleteSnapshot)
        {
            Snapshot correctSnapshot = wrongCompleteSnapshot;
            
            foreach (var kvp in incompleteSnapshot.States)
            {
                ulong networkId = kvp.Key;
                IState serverState = kvp.Value;
                IState netObjectServerState = incompleteSnapshot.NetObjectStates[networkId];
                
                correctSnapshot.States.Remove(networkId);
                correctSnapshot.States.Add(networkId, serverState);
                correctSnapshot.NetObjectStates.Remove(networkId);
                correctSnapshot.NetObjectStates.Add(networkId, netObjectServerState);
            }
            
            return correctSnapshot;
        }
        #endif
        
        #if Client
        private bool CompareStates(NetObject netObject, IState localState, IState serverState)
        {
            uint result = localState.Compare(localState, serverState);

            switch (result)
            {
                case (uint) CompareResult.Equal:
                    // Everything is fine.
                    break;
                case (uint) CompareResult.WorldCorrection:
                    // Apply the entire server state and recalculate some ticks to be ahead of the server again.
                    return true; // Reconcile
                    break;
                default:
                    // Apply only a part of the server state.
                    // Don't use try catch here, because if we receive states, we should sync them!
                    netObject.TryApplyPartialState(serverState, result);
                    break;
            }

            return false;
        }
        #endif
        
        #endregion
    }
}