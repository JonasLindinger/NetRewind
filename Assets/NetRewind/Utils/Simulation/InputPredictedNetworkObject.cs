using System;
using NetRewind.Utils.Input;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public abstract class InputPredictedNetworkObject : PredictedNetworkObject
    {
        // Todo: Separate visuals from the real input.
        private byte[] _input;
        
        protected override void OnTickTriggered(uint tick)
        {
            #if Client
            if (IsOwner)
            {
                // -> Local client, get local input
                InitInputTick(
                    tick, 
                    InputContainer.GetInput(tick).Input
                );
            }
            #endif
            #if Server 
            if (!IsOwner && InputTransportLayer.SentInput(OwnerClientId))
            {
                // Not local client -> get input from InputTransportLayer
                try
                {
                    InitInputTick(
                        tick,
                        InputTransportLayer.GetInput(OwnerClientId, tick).Input
                    );
                }
                catch (Exception e)
                {
                    Debug.Log("No input found!");
                }
            }
            #endif
        }
        
        private void InitInputTick(uint tick, byte[] input)
        {
            _input = input;
            OnTick(tick);
        }

        protected override bool IsPredicted() => IsOwner;

        protected abstract void OnTick(uint tick);

        protected bool GetButton(int id) => InputSender.GetInstance().GetButton(id, _input);
        protected Vector2 GetVector2(int id) => InputSender.GetInstance().GetVector2(id, _input);
    }
}