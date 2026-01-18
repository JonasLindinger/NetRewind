using System;
using TMPro;
using UnityEngine;

namespace _Demo.Scripts.Application
{
    public class FPSDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text fpsText;
        
        private float _deltaTime;

        void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        }

        private void FixedUpdate()
        {
            float fps = 1.0f / _deltaTime;
            string text = "FPS: " + Mathf.Ceil(fps);
            
            fpsText.text = text;
        }
    }
}