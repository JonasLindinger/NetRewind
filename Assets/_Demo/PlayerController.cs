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

        public override IData GetPlayerData()
        {
            return null;
        } 

        protected override IState GetCurrentState()
        {
            return null; // Todo: replace
        }

        protected override void SetState(uint tick, IState state)
        {
            // Todo: replace
        }

        protected override void StateUpdate(uint tick, IState state)
        {
            // Todo: replace
        }

        protected override bool DoWeNeedToReconcile(uint tick, IState predictedState, IState serverState)
        {
            return false;  // Todo: replace
        }
    }
}