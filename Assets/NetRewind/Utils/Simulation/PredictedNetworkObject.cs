using System;
using System.Collections.Generic;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public abstract class PredictedNetworkObject : NetObject
    {
        protected override void OnStateReceived(IState localState, IState serverState)
        {
            (CompareResult result, uint part) result = localState.Compare(localState, serverState);
            
            switch (result.result)
            {
                case CompareResult.Equal:
                    // Everything is fine.
                    break;
                case CompareResult.FullObjectCorrection:
                    // Apply the entire server state.
                    // Don't use try catch here, because if we receive states, we should sync them!
                    ApplyState(serverState);
                    break;
                case CompareResult.PartialObjectCorrection:
                    // Apply only a part of the server state.
                    // Don't use try catch here, because if we receive states, we should sync them!
                    ApplyPartialState(serverState, result.part);
                    break;
                case CompareResult.GroupCorrection:
                    // Todo: Apply a group of objects.
                    throw new System.NotImplementedException();
                    break;
                case CompareResult.WorldCorrection:
                    // Request a full Snapshot and do reconciliation.
                    RequestSnapshotRPC();
                    break;
                
                default:
                    throw new System.NotImplementedException();
            }
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable, RequireOwnership = false)]
        private void RequestSnapshotRPC()
        {
            #if Server
            try 
            {
                Snapshot snapshot = SnapshotContainer.GetLatestSnapshot();

                ulong sender = NetworkManager.Singleton.LocalClientId;
                
                SendSnapshotRPC(snapshot, RpcTarget.Single(sender, RpcTargetUse.Temp));
            }
            catch (KeyNotFoundException e) 
            {
                Debug.LogWarning("Latest Snapshot not found!");
            }
            #endif
        }
        
        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void SendSnapshotRPC(Snapshot snapshot, RpcParams rpcParams = default)
        {
            #if Client
            Simulation.InitReconciliation(snapshot);
            #endif
        }
    }
}