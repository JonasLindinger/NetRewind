using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace NetRewind.Utils.Sync
{
    public abstract class TickSystem : NetworkBehaviour
    {
        private bool _isRunning;

        protected void StartTickSystem(int tickRate)
        {
            _isRunning = true;
            
            InitiateTick(Mathf.RoundToInt((1f / tickRate) * 1000));
        }

        public void StopTickSystem()
        {
            _isRunning = false;
        }
        
        private async void InitiateTick(int ms)
        {
            if (!_isRunning) return;
            
            OnTick();
            await Task.Delay(ms);
            InitiateTick(ms);
        }
        
        protected abstract void OnTick();
    }
}