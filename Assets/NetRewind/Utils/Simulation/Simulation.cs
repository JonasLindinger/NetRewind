using System.Threading.Tasks;
using NetRewind.Utils.Input;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public static class Simulation
    {
        public static uint CurrentTick { get; private set; }
        public static uint TickRate { get; private set; }
        public static float TimeBetweenTicks { get; private set; }
        private static bool _isRunning;

        private static int _ms;
        #if Client
        private static bool _isSkipping;
        #endif
        
        public static void StartTickSystem(uint tickRate, uint startingTick)
        {
            CurrentTick = startingTick;
            TickRate = tickRate;
            TimeBetweenTicks = 1f / tickRate;

            _ms = Mathf.RoundToInt(TimeBetweenTicks * 1000);
            InitiateTick(); // Initiate first tick
            
            _isRunning = true;
            #if Client
            _isSkipping = false;
            #endif
        }

        public static void StopTickSystem()
        {
            _isRunning = false;
            #if Client
            _isSkipping = false;
            #endif
        }
        
        private static async void InitiateTick()
        {
            if (!_isRunning) return;
            CurrentTick++;
            OnTick(CurrentTick, false);
            await Task.Delay(_ms);
            InitiateTick();
        }

        private static void OnTick(uint tick, bool isReconciliation)
        {
            #if Client
            InputContainer.Collect(tick);
            #endif
        }

        #if Client
        public static void SetTick(uint tick)
        {
            CurrentTick = tick;
        }

        public static void CalculateExtraTicks(uint amount, bool isReconciliation)
        {
            if (!_isRunning) return;
            for (int i = 0; i < amount; i++)
            {
                CurrentTick++;
                OnTick(CurrentTick, isReconciliation);
            }
        }
        
        public static async void SkipTicks(uint amount, bool isReconciliation)
        {
            if (!_isRunning) return;
            if (_isSkipping) return;
            
            uint tempTickRate = TickRate - amount;
            float tempTimeBetweenTicks = 1f / tempTickRate;
            _ms = Mathf.RoundToInt(tempTimeBetweenTicks * 1000);
            _isSkipping = true;
            
            await Task.Delay(_ms);
            
            _ms = Mathf.RoundToInt(TimeBetweenTicks * 1000);
            _isSkipping = false;
        }
        #endif
    }
}