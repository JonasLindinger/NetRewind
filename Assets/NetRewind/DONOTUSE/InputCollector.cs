using System.Collections.Generic;
using System.Linq;
using NetRewind.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NetRewind.DONOTUSE
{
    public static class InputCollector
    {
        public static int NetworkInputFlagCount => inputFlagNames.Count;
        public static int NetworkDirectionalInputCount => directionalInputNames.Count;
        
        // All Input names
        private static List<string> directionalInputNames = new List<string>();
        private static List<string> inputFlagNames = new List<string>();
        
        public static string GetNetworkInputFlagName(int index) => inputFlagNames[index];
        public static string GetNetworkDirectionalInputName(int index) => directionalInputNames[index];
        
        // Inputs by there name and there value
        private static Dictionary<string, Vector2> directionalInputs = new Dictionary<string, Vector2>();
        private static Dictionary<string, bool> inputFlags = new Dictionary<string, bool>();
        
        #if Client
        private static Dictionary<string, InputAction> inputs = new Dictionary<string, InputAction>();
        
        private static Queue<ClientInputState> lastInputStates = new Queue<ClientInputState>();
        private static ClientInputState[] localInputBuffer;
        
        private static bool enabled; // For example, when in UI, this is false and returns always false or Vector2.zero.
        private static bool setUp;
        private static uint inputBufferOnClient;
        #endif
        
        private static ClientInputState emptyInputState;
        
        #if Client
        public static void Enable()
        {
            enabled = true;
        }

        public static void Disable()
        {
            enabled = false;
        }
        #endif
        
        public static void SetUp(List<InputAction> inputActions)
        {
            #if Client
            if (setUp) return;
            
            inputBufferOnClient = NetworkRunner.Runner.InputBufferOnClient;
            localInputBuffer = new ClientInputState[inputBufferOnClient];
            
            setUp = true;

            foreach (var input in inputActions)
                inputs.Add(input.name, input);
            #endif
            
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

            emptyInputState = new ClientInputState()
            {
                Tick = 0,
                LatestReceivedServerGameStateTick = 0,
                InputFlags = inputFlags,
                DirectionalInputs = directionalInputs,
                Data = null,
            };

            #if Client
            Enable();
            #endif
        }
        
        #if Client
        public static void Collect(uint tick)
        {
            // Check if SetUp
            if (!setUp)
                return;
            
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

            // Get the Player IData (if a local player exists)
            IData playerData = PlayerNetworkEntity.Local == null ? null : PlayerNetworkEntity.Local.GetPlayerData();
            
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
        }

        public static ClientInputState[] GetInputStatesToSend(uint amount)
        {
            // Remove old inputs so that the queue has the max size of: AMOUNT_OF_INPUTS_TO_KEEP
            int i;
            if (lastInputStates.Count > inputBufferOnClient)
            {
                for (i = 0; i < lastInputStates.Count - inputBufferOnClient; i++)
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

            // Remove inputs, that the server doesn't need
            for (i = inputsToReturn.Count - 1; i >= 0; i--)
            {
                // Check if we 100% know, that the server doesn't need our input and then remove it.
                bool isUseless = inputsToReturn[i].Tick <= GameStateSync.latestReceavedServerGameStateTick;
                if (isUseless)
                    inputsToReturn.RemoveAt(i);
            }
            
            // Return the list as an array
            return inputsToReturn.ToArray();
        }
        
        public static ClientInputState GetInput(uint tick)
        {
            ClientInputState input = localInputBuffer[tick % localInputBuffer.Length];

            if (input != null)
            {
                // Check if input is correct
                if (input.Tick == tick)
                    return input;
            }

            return null;
        }
        #endif
    }
}