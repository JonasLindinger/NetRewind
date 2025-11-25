using NetRewind.Utils.Simulation.State;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.State
{
    public class PlayerState : IState
    {
        public Vector3 Position;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
        }

        public (CompareResult, uint) Compare(IState localState, IState serverState)
        {
            PlayerState local = (PlayerState) localState;
            PlayerState server = (PlayerState) serverState;

            if (Vector3.Distance(local.Position, server.Position) > 0.1f)
            {
                // Predicted position is wrong
                Debug.Log("Player state is wrong");
                return (CompareResult.WorldCorrection, 0);
            }

            Debug.Log("Player state is correct");
            return (CompareResult.Equal, 0);
        }
        
        static PlayerState() => StateFactory.Register((int)StateTypes.Player, () => new PlayerState());
        
        public int GetStateType() => (int) StateTypes.Player;
    }
}