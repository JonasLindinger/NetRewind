using NetRewind;
using NetRewind.Utils;
using UnityEngine;

namespace _Demo
{
    public class PlayerController : PlayerNetworkEntity
    {
        [Header("Settings")]
        [SerializeField] private float moveSpeed = 7f;
        
        protected override void OnTick(uint tick, ClientInputState input)
        {
            Vector2 moveInput = input.DirectionalInputs["Move"].normalized;
            moveInput *= moveSpeed * NetworkRunner.Runner.TimeBetweenTicks;
            
            transform.position += new Vector3(moveInput.x, 0, moveInput.y);
        }

        public override IData GetPlayerData()
        {
            return null;
        } 

        public override IState GetCurrentState()
        {
            PlayerState state = new PlayerState();

            state.Position = transform.position;
            state.Rotation = transform.eulerAngles;
            
            return state;
        }

        public override void SetState(uint tick, IState state)
        {
            if (state is not PlayerState playerState) return;

            transform.position = playerState.Position;
            transform.eulerAngles = playerState.Rotation;
        }

        public override void StateUpdate(uint tick, IState state)
        {
            if (state is not PlayerState playerState) return;

            // Todo: lerp / use doTween
            transform.position = playerState.Position;
            transform.eulerAngles = playerState.Rotation;
        }

        public override bool DoWeNeedToReconcile(uint tick, IState predictedState, IState serverState)
        {
            if (predictedState is not PlayerState predictedPlayerState) return true;
            if (serverState is not PlayerState serverPlayerState) return true;

            if (Vector3.Distance(predictedPlayerState.Position, serverPlayerState.Position) > 0.1f)
            {
                Debug.Log("Position error"); // Todo: remove this
                return true;
            }
            else if (Vector3.Distance(predictedPlayerState.Rotation, serverPlayerState.Rotation) > 0.5f)
            {
                Debug.Log("Rotation error"); // Todo: remove this
                return true;
            }

            return false;
        }
    }
}