using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.Car
{
    [StateType]
    public struct CarState : IState
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public ulong Seat1;
        public ulong Seat2;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AngularVelocity);
            serializer.SerializeValue(ref Seat1);
            serializer.SerializeValue(ref Seat2);
        }

        public uint Compare(IState localState, IState serverState)
        {
            CarState local = (CarState) localState;
            CarState server = (CarState) serverState;
            
            if (Vector3.Distance(local.Position, server.Position) > 0.25f)
            {
                Debug.Log("Car Position");
                return 1;
            }
            
            if (Vector3.Distance(local.Rotation, server.Rotation) > 0.25f)
            {
                Debug.Log("Car Rotation");
                return 1;
            }
            
            if (local.Seat1 != server.Seat1)
            {
                Debug.Log("Car Seat1");
                return 1;
            }
            
            if (local.Seat2 != server.Seat2)
            {
                Debug.Log("Car Seat2");
                return 1;
            }
            
            return 0;
        }
    }
}