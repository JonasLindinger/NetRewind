using NetRewind.Utils;
using UnityEngine;

namespace _Demo
{
    public class PlayerController : PlayerNetworkEntity
    {
        protected override void OnTick(uint tick, ClientInputState input)
        {
            Debug.Log(tick + " " + input.DirectionalInputs["Move"]);
        }

        protected override IState GetCurrentState()
        {
            return null; // Todo: replace
        }

        protected override void SetState(uint tick, IState state)
        {
            // Todo: replace
        }

        protected override void ApplyState(uint tick, IState state)
        {
            // Todo: replace
        }

        protected override bool DoWeNeedToReconcile(uint tick, IState predictedState, IState serverState)
        {
            return false;  // Todo: replace
        }
    }
}