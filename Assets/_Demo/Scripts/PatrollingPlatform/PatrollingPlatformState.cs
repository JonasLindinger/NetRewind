using _Demo.Scripts.State;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.PatrollingPlatform
{
    [StateType]
    public struct PatrollingPlatformState : IState
    {
        public Vector3 Position;
        public Vector3 Rotation;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }
        
        public (CompareResult, uint) Compare(IState localState, IState serverState)
        {
            PatrollingPlatformState local = (PatrollingPlatformState) localState;
            PatrollingPlatformState server = (PatrollingPlatformState) serverState;
            
            if (Vector3.Distance(local.Position, server.Position) > 0.25f)
            {
                return (CompareResult.FullObjectCorrection, 0);
            }
            if (Vector3.Distance(local.Rotation, server.Rotation) > 0.25f)
            {
                return (CompareResult.FullObjectCorrection, 0);
            }

            return (CompareResult.Equal, 0);
        }
    }
}