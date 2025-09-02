using NetRewind.Utils;
using UnityEngine;

namespace _Demo
{
    public class PlayerController : PlayerNetworkEntity
    {
        protected override void OnTick(uint tick, ClientInputState input)
        {
            foreach (var v in input.DirectionalInputs)
            {
                Debug.Log(OwnerClientId + " -> " + v.Key + " " + v.Value);
            }
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