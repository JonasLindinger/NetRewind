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
        private const uint MaxTicksToReconcile = 300;
        
        /// <summary>
        /// DON'T USE THIS IN A OnTick method. Depending on your logic, this might cause problems, since this is not changed during reconciliation, so its always the highest tick.
        /// </summary>
        public static uint CurrentTick { get; private set; }
        public static uint TickRate { get; private set; }
        public static float TimeBetweenTicks { get; private set; }
        private static bool _isRunning;

        private static float _timeBetweenTicks;
        private static float _timer;
        #if Client
        private static bool _isAdjusting;
        public static bool IsCorrectingGameState { get; private set; }
        private static Snapshot _reconciliationSnapshot = new Snapshot(0);
        #endif
        
        public static void StartTickSystem(uint tickRate, uint startingTick)
        {
            #if Client
            InputContainer.Init();
            #endif
            
            if (NetRunner.GetInstance().ControlPhysics)
                Physics.simulationMode = SimulationMode.Script;
            
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

                #if Client
                if (_reconciliationSnapshot.Tick != 0)
                {
                    Reconcile(_reconciliationSnapshot);
                    
                    // Reset
                    _reconciliationSnapshot = new Snapshot(0);
                }
                #endif
                
                CurrentTick++;
                OnTick(CurrentTick);
            }
        }

        public static void StopTickSystem()
        {
            if (NetRunner.GetInstance() != null && NetRunner.GetInstance().ControlPhysics)
                Physics.simulationMode = SimulationMode.FixedUpdate;
            
            _isRunning = false;
            #if Client
            _isAdjusting = false;
            #endif
        }

        private static void OnTick(uint tick, bool isReconciliation = false)
        {
            // 1. Simulation physics
            if (NetRunner.GetInstance().ControlPhysics)
                Physics.Simulate(TimeBetweenTicks);
            
            // 2. Save Game State
            SnapshotContainer.TakeSnapshot(tick);
            
            #if Client
            if (!isReconciliation)
            {
                // 2.5 Collect local client input
                InputContainer.Collect(tick);
            }
            #endif
            
            // 3. Run updates
            NetObject.RunTick(tick);
        }

        #if Client
        public static void SetTick(uint tick)
        {
            CurrentTick = tick;
        }

        public static void CalculateExtraTicks(uint amount)
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

        public static void InitReconciliation(Snapshot snapshot)
        {
            _reconciliationSnapshot = snapshot;
            IsCorrectingGameState = true;
        }

        private static void Reconcile(Snapshot snapshot)
        {
            Debug.LogWarning("Reconciling...");
            
            // --- Apply the snapshot. ---
            foreach (var kvp in snapshot.States)
                NetObject.ApplyState(kvp.Key, kvp.Value);
            
            // --- Save the snapshot. ---
            SnapshotContainer.StoreSnapshot(snapshot);
            
            // --- Recalculate the tick(s). ---
            
            // -> Line up the simulation rhythm
            // 3. Run updates
            NetObject.RunTick(snapshot.Tick);
            
            // Check if the amount of ticks that we have to recalculate is too big, so that it potentially crashes the game or is bad player experience.
            uint ticksToRecalculate = CurrentTick - snapshot.Tick + 1;
            if (ticksToRecalculate > MaxTicksToReconcile)
            {
                Debug.LogError("Too many ticks to reconcile (" + ticksToRecalculate + "). Max allowed is: " + MaxTicksToReconcile);
                
                // Todo: Kick the player / force rejoin...?
                return;
            }
            
            // -> Recalculate every tick
            for (uint tick = snapshot.Tick + 1; tick <= CurrentTick; tick++)
                OnTick(tick, true);

            IsCorrectingGameState = false;
        }
        #endif
    }
}