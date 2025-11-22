using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NetRewind.Utils.Input
{
    public class InputSender : MonoBehaviourSingleton<InputSender>
    {
        [Header("Inputs")]
        [SerializeField] private List<InputActionEntry> actions = new List<InputActionEntry>();

        private int _byteArraySize;
        private byte[] _data;
        
        private void OnEnable()
        {
            // Enable all actions
            foreach (var entry in actions)
                entry.Action?.Enable();
        }

        private void OnDisable()
        {
            // Disable all actions
            foreach (var entry in actions)
                entry.Action?.Disable();
        }
        
        private void Start()
        {
            // Calculate how many bits we need
            int id = 0;
            foreach (var entry in actions)
            {
                if (entry.IsButton)
                {
                    _byteArraySize++;
                    entry.id = id++;
                }
                else if (entry.IsVector2)
                {
                    _byteArraySize += 4;
                    entry.id = id;
                    id += 4;
                }
                
                entry.Subscribe(OnVector2, OnButton);
            }
            
            // Convert bits to bytes
            _byteArraySize = (_byteArraySize + 7) / 8; // ceil division
            
            _data = new byte[_byteArraySize];
        }

        public byte[] CollectInput() => _data;

        private void OnVector2(Vector2 vector, InputActionEntry entry)
        {
            // Vector2 (-1,0,1 per axis)
            int vx = vector.x < 0 ? 1 : (vector.x > 0 ? 2 : 0); // 2 bits
            int vy = vector.y < 0 ? 1 : (vector.y > 0 ? 2 : 0); // 2 bits
            int packed = (vx << 2) | vy; // 4 bits total
            SetBits(entry.id, packed, 4);
        }
        
        private void OnButton(bool button, InputActionEntry entry)
        {
            SetBit(entry.id, button);
        }
        
        public bool GetButton(int id, byte[] data)
        {
            return GetBits(id, 1, data) != 0;
        }

        public Vector2 GetVector2(int id, byte[] data)
        {
            // Vector2
            int packed = GetBits(id, 4, data);
            int vx = (packed >> 2) & 0x03;
            int vy = packed & 0x03;
            float x = vx == 1 ? -1f : vx == 2 ? 1f : 0f;
            float y = vy == 1 ? -1f : vy == 2 ? 1f : 0f;
            return new Vector2(x, y);
        }
        
        // Set a single bit
        void SetBit(int bitIndex, bool value)
        {
            int byteIndex = bitIndex / 8;
            int bitInByte = bitIndex % 8;

            if (value)
                _data[byteIndex] |= (byte)(1 << bitInByte);
            else
                _data[byteIndex] &= (byte)~(1 << bitInByte);
        }

        // Set N bits (value) at bitIndex
        void SetBits(int bitIndex, int value, int bitCount)
        {
            for (int i = 0; i < bitCount; i++)
            {
                bool bit = ((value >> i) & 1) != 0;
                SetBit(bitIndex + i, bit);
            }
        }

        // Get N bits starting at bitIndex
        int GetBits(int bitIndex, int bitCount, byte[] data)
        {
            int result = 0;
            for (int i = 0; i < bitCount; i++)
            {
                int byteIndex = (bitIndex + i) / 8;
                int bitInByte = (bitIndex + i) % 8;
                if ((data[byteIndex] & (1 << bitInByte)) != 0)
                    result |= (1 << i);
            }
            return result;
        }
    }
}