using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils
{
    public abstract class TickSystem : NetworkBehaviour
    {
        private bool isRunning;
        
        public void StartTickSystem(int tickRate)
        {
            isRunning = true;
            
            InitiateTick(Mathf.RoundToInt((1f / tickRate) * 1000));
        }

        public void StopTickSystem()
        {
            isRunning = false;
        }
        
        private async void InitiateTick(int ms)
        {
            if (!isRunning) return;
            
            OnTick();
            await Task.Delay(ms);
            InitiateTick(ms);
        }
        
        protected abstract void OnTick();
    }
}