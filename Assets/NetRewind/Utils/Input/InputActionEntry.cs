using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetRewind.Utils.Input
{
    [Serializable]
    public class InputActionEntry
    {
        public string name;
        public InputActionReference actionReference;

        private Action<InputAction.CallbackContext> _vector2Callback;
        private Action<InputAction.CallbackContext> _buttonCallback;

        public InputAction Action => actionReference?.action;

        public bool IsVector2 => Action?.expectedControlType == "Vector2";
        public bool IsButton => Action?.expectedControlType == "Button";

        public Vector2 ReadVector2() => IsVector2 && Action != null ? Action.ReadValue<Vector2>() : Vector2.zero;
        public bool ReadButton() => IsButton && Action != null && Action.ReadValue<float>() > 0.4f;

        [HideInInspector] public int id;

        // Subscribe to events
        public void Subscribe(Action<Vector2, InputActionEntry> onVector2, Action<bool, InputActionEntry> onButton)
        {
            if (Action == null) return;

            if (IsVector2)
            {
                _vector2Callback = ctx => onVector2?.Invoke(ctx.ReadValue<Vector2>(), this);
                Action.performed += _vector2Callback;
                Action.canceled += ctx => onVector2?.Invoke(Vector2.zero, this); // optional
            }
            else if (IsButton)
            {
                _buttonCallback = ctx => onButton?.Invoke(true, this);
                Action.performed += _buttonCallback;
                Action.canceled += ctx => onButton?.Invoke(false, this);
            }

            Action.Enable();
        }

        // Proper unsubscribe
        public void UnsubscribeAll()
        {
            if (Action == null) return;

            if (_vector2Callback != null)
            {
                Action.performed -= _vector2Callback;
                _vector2Callback = null;
            }

            if (_buttonCallback != null)
            {
                Action.performed -= _buttonCallback;
                _buttonCallback = null;
            }
        }
    }
}