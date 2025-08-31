using System.Collections.Generic;
using System.Linq;
using NetRewind.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetRewind.DONOTUSE
{
    public static class InputCollector
    {
        #if Client
        // Amount of Inputs to store in a queue
        private const uint AMOUNT_OF_INPUTS_TO_KEEP = 128;
        
        public static int NetworkInputFlagCount => inputFlagNames.Count;
        public static int NetworkDirectionalInputCount => directionalInputNames.Count;
        
        public static string GetNetworkInputFlagName(int index) => inputFlagNames[index];
        public static string GetNetworkDirectionalInputName(int index) => directionalInputNames[index];
        
        // Inputs by there name and there value
        private static Dictionary<string, Vector2> directionalInputs = new Dictionary<string, Vector2>();
        private static Dictionary<string, bool> inputFlags = new Dictionary<string, bool>();
        
        // All Input names
        private static List<string> directionalInputNames = new List<string>();
        private static List<string> inputFlagNames = new List<string>();

        private static Queue<ClientInputState> lastInputStates = new Queue<ClientInputState>();
        private static Dictionary<string, InputAction> inputs = new Dictionary<string, InputAction>();
        
        private static bool enabled; // For example, when in UI, this is false and returns always false or Vector2.zero.
        private static bool setUp;

        public static void Enable()
        {
            enabled = true;
        }

        public static void Disable()
        {
            enabled = false;
        }
        
        public static void SetUp(List<InputAction> inputActions)
        {
            if (setUp) return;
            
            setUp = true;
            foreach (var input in inputActions)
                inputs.Add(input.name, input);
            
            foreach (var action in inputActions)
            {
                switch (action.type)
                {
                    case InputActionType.Button:
                        inputFlags.Add(action.name, false);
                        inputFlagNames.Add(action.name);
                        break;
                    case InputActionType.Value:
                        if (action.expectedControlType == nameof(Vector2))
                        {
                            directionalInputs.Add(action.name, Vector2.zero);
                            directionalInputNames.Add(action.name);
                        }
                        else
                        {
                            if (NetworkRunner.Runner.DebugMode == DebugMode.All || NetworkRunner.Runner.DebugMode == DebugMode.ErrorsOnly)
                                Debug.LogWarning("Can't serialize this input: " + action.name);
                        }
                        break;
                    case InputActionType.PassThrough:
                        if (NetworkRunner.Runner.DebugMode == DebugMode.All || NetworkRunner.Runner.DebugMode == DebugMode.ErrorsOnly)
                            Debug.LogWarning("Can't serialize this input: " + action.name);
                        break;
                }
            }

            Enable();
        }
        
        public static ClientInputState Collect(uint tick)
        {
            // Check if SetUp
            if (!setUp)
                return null;
            
            // Update the input list(s)
            if (enabled)
            {
                // Update boolean's
                foreach (var inputFlag in inputFlags.Keys.ToArray())
                    inputFlags[inputFlag] = false;

                // Update Vector2's
                foreach (var inputFlag in directionalInputs.Keys.ToArray())
                    directionalInputs[inputFlag] = Vector2.zero.normalized;
            }
            else
            {
                // Update boolean's
                foreach (var inputFlag in inputFlags.Keys.ToArray())
                    inputFlags[inputFlag] = inputs[inputFlag].ReadValue<bool>();

                // Update Vector2's
                foreach (var inputFlag in directionalInputs.Keys.ToArray())
                    directionalInputs[inputFlag] = inputs[inputFlag].ReadValue<Vector2>();
            }

            IData playerData = PlayerNetworkEntity.Local.GetPlayerData();
            
            // Create the actual input
            ClientInputState input = new ClientInputState()
            {
                Tick = tick,
                LatestReceivedServerGameStateTick = GameStateSync.latestReceavedServerGameStateTick,
                InputFlags = inputFlags,
                DirectionalInputs = directionalInputs,
                Data = playerData
            };

            // Enqueue the input, so that it can be collected afterward.
            lastInputStates.Enqueue(input);
            
            return input;
        }

        public static ClientInputState[] GetLastInputStates(uint amount)
        {
            // Remove old inputs so that the queue has the max size of: AMOUNT_OF_INPUTS_TO_KEEP
            int i;
            if (lastInputStates.Count > AMOUNT_OF_INPUTS_TO_KEEP)
            {
                for (i = 0; i < lastInputStates.Count - AMOUNT_OF_INPUTS_TO_KEEP; i++)
                    lastInputStates.Dequeue();
            }
            
            // Add the latest inputs to a list
            List<ClientInputState> inputsToReturn = new List<ClientInputState>();
            ClientInputState[] lastInputs = lastInputStates.ToArray();
            i = lastInputs.Length - 1;
            while (inputsToReturn.Count < amount && i >= 0)
            {
                inputsToReturn.Add(lastInputs[i]);
                i--;
            }
            
            // Return the list as an array
            return inputsToReturn.ToArray();
        }
            
        #endif
    }
}