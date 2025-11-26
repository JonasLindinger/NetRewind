using System;
using NetRewind.Utils.Input;
using NetRewind.Utils.Input.Data;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public abstract class NetInput : PredictedNetObject
    {
        private byte[] _input;
        private IData _data;
        
        protected override void OnTickTriggered(uint tick)
        {
            #if Client
            if (IsOwner)
            {
                // -> Local client, get local input
                InputState inputState = InputContainer.GetInput(tick);
                InitInputTick(
                    tick, 
                    inputState.Input,
                    inputState.Data
                );
            }
            #endif
            #if Server 
            if (!IsOwner && InputTransportLayer.SentInput(OwnerClientId))
            {
                // Not local client -> get input from InputTransportLayer
                try
                {
                    InputState inputState = InputTransportLayer.GetInput(OwnerClientId, tick);
                    InitInputTick(
                        tick,
                        inputState.Input,
                        inputState.Data
                    );
                }
                catch (Exception e)
                {
                    Debug.Log("No input found!");
                }
            }
            #endif
        }
        
        private void InitInputTick(uint tick, byte[] input, IData data)
        {
            _input = input;
            _data = data;
            OnTick(tick);
        }
        
        protected override bool IsPredicted() => IsOwner;
        protected abstract void OnTick(uint tick);
        protected bool GetButton(int id) => InputSender.GetInstance().GetButton(id, _input);
        protected Vector2 GetVector2(int id) => InputSender.GetInstance().GetVector2(id, _input);
        protected T GetData<T>() where T : IData => (T) _data;
    }
}