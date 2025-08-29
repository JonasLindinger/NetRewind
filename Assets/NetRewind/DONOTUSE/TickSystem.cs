using System;
using UnityEngine;

namespace NetRewind.DONOTUSE
{
    public class TickSystem
    {
        public event Action<uint> OnTick = delegate { };
        public uint Tick { get; private set; }
        public uint TickRate { get; private set; }
        public float TimeBetweenTicks { get; private set; }
        public bool SetUp { get; private set; }

        private float timer;
        
        public TickSystem(uint tickRate, uint tickOffset = 0)
        {
            SetUp = true;

            Tick = tickOffset;
            TickRate = tickRate;
            TimeBetweenTicks = 1f / TickRate;
        }
        
        public void Update(float deltaTime)
        {
            #region Checks
            
            if (NetworkRunner.Runner == null)
            {
                Debug.LogError("NetworkRunner not found!");
                return;
            }
            
            if (!SetUp)
            {
                if (NetworkRunner.Runner.DebugMode == DebugMode.All || NetworkRunner.Runner.DebugMode == DebugMode.ErrorsOnly)
                    Debug.LogError("TickSystem not set up!");
            }
            
            #endregion
            
            timer += deltaTime;
            if (timer < TimeBetweenTicks) return;

            DoTick();
        }

        private void DoTick()
        {
            timer -= TimeBetweenTicks;
            Tick++;
                
            OnTick?.Invoke(Tick);
        }
    }
}