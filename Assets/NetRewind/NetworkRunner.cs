using System;
using UnityEngine;
using Unity.Netcode;

namespace NetRewind
{
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkRunner : MonoBehaviour
    {
        #region Variables
        
        /// <summary>
        /// Singleton instance
        /// </summary>
        public static NetworkRunner Runner { get; private set; }

        /// <summary>
        /// Maximum allowed tick rate
        /// </summary>
        private const float MAX_TICK_RATE = 256;
        
        /// <summary>
        /// Unity Editor Settings
        /// </summary>
        [Header("Settings")] 
        [SerializeField] private uint simulationTickRate = 64;
        [SerializeField] private uint inputTickRate = 32;
        [SerializeField] private uint stateTickRate = 32;
        
        /// <summary>
        /// References
        /// </summary>
        private NetworkManager networkManager;

        /// <summary>
        /// Runntime Variables
        /// </summary>
        private uint networkTickRate;
        
        #endregion
        
        #region Unity Methods
        
        private void Awake()
        {
            // Ensure only one instance of NetworkRunner exists
            if (Runner == null)
            {
                Runner = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogError("There can only be one NetworkRunner!");
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Referencing
            networkManager = GetComponent<NetworkManager>();

            // SetUp Values
            networkTickRate = (uint) Mathf.Max(inputTickRate, stateTickRate);
            
            SetUpNetworkManager();
        }

        private void OnValidate()
        {
            // ----- Clamp Tick Rates -----
            
            // Clamp the simulation tick rate to a maximum value.
            simulationTickRate = (uint) Mathf.Min(simulationTickRate, MAX_TICK_RATE);
            
            // Clamp the input tick rate to be no greater than the simulation tick rate.
            inputTickRate = (uint) Mathf.Min(inputTickRate, simulationTickRate);
            
            // Clamp the state tick rate to be no greater than the simulation tick rate.
            stateTickRate = (uint) Mathf.Min(stateTickRate, simulationTickRate);
        }

        #endregion
        
        private void SetUpNetworkManager()
        {
            networkManager.NetworkConfig.TickRate = networkTickRate;
        }
    }
}