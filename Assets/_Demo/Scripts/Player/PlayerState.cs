using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    [StateType]
    public struct PlayerState : IState
    {
        public Vector3 Position;
        public float YRotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref YRotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AngularVelocity);
        }

        public (CompareResult, uint) Compare(IState localState, IState serverState)
        {
            PlayerState local = (PlayerState) localState;
            PlayerState server = (PlayerState) serverState;
            
            if (Vector3.Distance(local.Position, server.Position) > 0.25f)
            {
                return (CompareResult.WorldCorrection, 0);
            }

            return (CompareResult.Equal, 0);
        }
    }
}