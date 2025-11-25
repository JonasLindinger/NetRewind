using _Demo.Scripts.State;
using NetRewind.Utils.Simulation;
using NetRewind.Utils.Simulation.State;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    public class PlayerController : InputPredictedNetworkObject
    {
        protected override void OnTick(uint tick)
        {
            Vector2 move = GetVector2(0);
            bool sprint = GetButton(4);

            Vector3 newPosition = transform.position;
            newPosition.x += move.x * (sprint ? 10 : 5);
            newPosition.z += move.y * (sprint ? 10 : 5);
            
            transform.position = newPosition;
        }

        protected override void UpdateState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            transform.position = playerState.Position;
        }

        protected override void ApplyState(IState state)
        {
            PlayerState playerState = (PlayerState) state;
            transform.position = playerState.Position;
        }
        
        protected override IState GetCurrentState()
        {
            return new PlayerState()
            {
                Position = transform.position,
            };
        }

        protected override void ApplyPartialState(IState state, uint part)
        {
            throw new System.NotImplementedException();
        }
    }
}