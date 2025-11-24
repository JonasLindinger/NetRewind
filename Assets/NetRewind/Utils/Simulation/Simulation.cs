using System;
using System.Threading.Tasks;
using NetRewind.Utils.Input;
using NetRewind.Utils.Simulation.State;
using UnityEngine;

namespace NetRewind.Utils.Simulation
{
    public static class Simulation
    {
        private const uint MaxTicksToCalculateInOneSecond = 140;
        
        public static uint CurrentTick { get; private set; }
        public static uint TickRate { get; private set; }
        public static float TimeBetweenTicks { get; private set; }
        private static bool _isRunning;

        private static float _timeBetweenTicks;
        private static float _timer;
        #if Client
        private static bool _isAdjusting;
        #endif
        
        public static void StartTickSystem(uint tickRate, uint startingTick)
        {
            CurrentTick = startingTick;
            TickRate = tickRate;
            TimeBetweenTicks = 1f / tickRate;
            _timeBetweenTicks = TimeBetweenTicks;
            
            _isRunning = true;
            #if Client
            _isAdjusting = false;
            #endif
        }

        public static void Update(float deltaTime)
        {
            if (!_isRunning) return;
            _timer += deltaTime;

            #if Client
            if (_isAdjusting) return;
            #endif
            while (_timer >= _timeBetweenTicks)
            {
                _timer -= _timeBetweenTicks;
                
                CurrentTick++;
                OnTick(CurrentTick);
            }
        }

        public static void StopTickSystem()
        {
            _isRunning = false;
            #if Client
            _isAdjusting = false;
            #endif
        }

        private static void OnTick(uint tick)
        {
            // 1. Simulation physics
            if (NetRunner.GetInstance().ControlPhysics)
                Physics.Simulate(TimeBetweenTicks);
            
            // 2. Save Game State
            SnapshotContainer.TakeSnapshot(tick);
            
            #if Client
            // 2.5 Collect local client input
            InputContainer.Collect(tick);
            #endif
            
            // 3. Run updates
            RegisteredNetworkObject.RunTick(tick);
        }

        #if Client
        public static void SetTick(uint tick)
        {
            CurrentTick = tick;
        }

        public static async void CalculateExtraTicks(uint amount)
        {
            if (!_isRunning) return;
            if (_isAdjusting) return;
            
            // Calculate ticks instantly to not fall back too hard!
            for (uint i = 0; i < amount; i++) 
            {
                CurrentTick++;
                OnTick(CurrentTick);
            }
        }
        
        public static async void SkipTicks(uint amount)
        {
            if (!_isRunning) return;
            if (_isAdjusting) return;
            
            // Slowly adjust the tick rate to get back on track. (Is set to skip amount ticks in 500ms).
            uint tempTickRate = TickRate - (amount * 2);
            
            if (tempTickRate < 1)
                tempTickRate = 1;
            
            _timeBetweenTicks = 1f / tempTickRate;
            _isAdjusting = true;
            
            await Task.Delay(500);
            
            _timeBetweenTicks = TimeBetweenTicks;
            _isAdjusting = false;
        }
        #endif
    }
}