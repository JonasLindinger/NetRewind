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
        public float Time;
        public bool Direction;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Time);
            serializer.SerializeValue(ref Direction);
        }
        
        public uint Compare(IState localState, IState serverState)
        {
            PatrollingPlatformState local = (PatrollingPlatformState) localState;
            PatrollingPlatformState server = (PatrollingPlatformState) serverState;
            
            // Position tolerance: be MUCH looser than 0.25f if it’s a moving platform
            if (Vector3.Distance(local.Position, server.Position) > 0.5f)
            {
                Debug.Log("PatrollingPlatform Position");
                return 1;
            }

            if (Vector3.Distance(local.Rotation, server.Rotation) > 2f)
            {
                Debug.Log("PatrollingPlatform Rotation");
                return 1;
            }
            
            if (local.Direction != server.Direction)
            {
                Debug.Log("PatrollingPlatform Direction");
                return 1;
            }
            
            if (!Mathf.Approximately(local.Time, server.Time))
            {
                Debug.Log("PatrollingPlatform Time");
                return 1;
            }

            return 0;
        }
    }
}