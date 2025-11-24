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
            Debug.Log("> " + tick + " -> " + GetButton(4));
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