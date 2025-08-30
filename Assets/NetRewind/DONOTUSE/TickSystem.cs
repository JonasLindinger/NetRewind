using System;
using System.Threading.Tasks;
using UnityEngine;

namespace NetRewind.DONOTUSE
{
    public class TickSystem
    {
        public event Action<uint> OnTick = delegate { };
        public uint Tick { get; private set; }
        public float TimeBetweenTicks { get; private set; }
        public uint TickRate { get; private set; }
        public bool SetUp { get; private set; }

        private float currentTimeBetweenTicks;
        
        private float timer;
        private int ticksToSkip;
        
        public TickSystem(uint tickRate, uint tickOffset = 0)
        {
            SetUp = true;

            Tick = tickOffset;
            TickRate = tickRate;
            TimeBetweenTicks = 1f / TickRate;
            currentTimeBetweenTicks = TimeBetweenTicks;
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
            if (timer < currentTimeBetweenTicks) return;
            timer -= currentTimeBetweenTicks;
            
            // Check if we should skip ticks
            
            DoTick();
        }

        private void DoTick()
        {
            Tick++;
                
            OnTick?.Invoke(Tick);
        }

        /// <summary>
        /// Calculates less ticks for the next second.
        /// </summary>
        /// <param name="amount"></param>
        public async void SkipTicks(uint amount)
        {
            currentTimeBetweenTicks = 1f / (TickRate - amount);

            await Task.Delay(1000);

            currentTimeBetweenTicks = 1f / TickRate;
        }

        /// <summary>
        /// Calculates more ticks for the next second.
        /// </summary>
        /// <param name="amount"></param>
        public async void CalculateExtraTicks(uint amount)
        {
            currentTimeBetweenTicks = 1f / (TickRate + amount);

            await Task.Delay(1000);

            currentTimeBetweenTicks = 1f / TickRate;
        }
        
        public void SetTick(uint tick) => Tick = tick;
    }
}