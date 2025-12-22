using System.Diagnostics;
using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace _Demo.Scripts.Player
{
    [StateType]
    public struct PlayerState : IState
    {
        public Vector3 Position;
        public float YRotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public ulong Car;
        public bool CanMove;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref YRotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AngularVelocity);
            serializer.SerializeValue(ref CanMove);
            serializer.SerializeValue(ref Car);
        }

        public uint Compare(IState localState, IState serverState)
        {
            PlayerState local = (PlayerState) localState;
            PlayerState server = (PlayerState) serverState;
            
            if (Vector3.Distance(local.Position, server.Position) > 0.25f)
            {
                Debug.Log("Player Position: " + local.Position + " vs " + server.Position);
                return 1;
            }

            if (CanMove != server.CanMove)
            {
                Debug.Log("Player CanMove");
                return 1;
            }

            if (Car != server.Car)
            {
                Debug.Log("Player Car");
                return 1;
            }
            
            return 0;
        }
    }
}