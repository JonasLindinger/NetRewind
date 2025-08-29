using System;
using System.Threading.Tasks;
using NetRewind.DONOTUSE;
using NetRewind.Utils;
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
        /// Getters
        /// </summary>
        public DebugMode DebugMode => debugMode;
        
        /// <summary>
        /// Maximum allowed tick rate
        /// </summary>
        private const float MAX_TICK_RATE = 256;

        /// <summary>
        /// Timeouts in seconds
        /// </summary>
        private const float START_SERVER_TIMEOUT = 5f;
        
        /// <summary>
        /// Unity Editor Settings
        /// </summary>
        [Header("Tick Rates")] 
        [SerializeField] private uint simulationTickRate = 64;
        [SerializeField] private uint inputTickRate = 32;
        [SerializeField] private uint stateTickRate = 32;
        [Space(10)] 
        [Header("Settings")] 
        [SerializeField] private bool autoStartServer = true;
        [SerializeField] private DebugMode debugMode = DebugMode.All;
        
        /// <summary>
        /// References
        /// </summary>
        private NetworkManager networkManager;

        /// <summary>
        /// Runntime Variables
        /// </summary>
        private uint networkTickRate;
        private TickSystem simulationTickSystem;
        #if Client
        private TickSystem inputTickSystem;
        #endif
        #if Server
        private TickSystem stateTickSystem;
        #endif
        
        #endregion
        
        #region Unity Methods
        
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
            
            #if Server
            // Auto Start Server
            if (autoStartServer)
                StartServer();
            #endif
        }

        private void Update()
        {
            simulationTickSystem?.Update(Time.deltaTime);
            #if Server
            stateTickSystem?.Update(Time.deltaTime);
            #endif
            #if Client
            inputTickSystem?.Update(Time.deltaTime);
            #endif
        }

        #endregion
        
        #region Setup Methods
        
        private void SetUpNetworkManager()
        {
            networkManager.NetworkConfig.TickRate = networkTickRate;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.RecycleNetworkIds = false;
        }
        
        #endregion

        #region Public Methods

        #if Client
        public async Task StartClient()
        {
                        
        }
        #endif

        #if Client 
        public async Task HostGame()
        {
            
        }
        #endif

        #if Server
        public async Task StartServer()
        {
            if (networkManager.StartServer())
            {
                #region Timeout Check
                int timer = 0;
                int timeout = Mathf.RoundToInt(START_SERVER_TIMEOUT * 1000);
                while (!(networkManager.IsServer && NetworkManager.Singleton.IsListening))
                {
                    timer += 1;

                    if (timer >= timeout)
                    {
                        // Timeout
                        if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                        {
                            Debug.LogError("[NetRewind] Timed out trying to start server!");
                        }
                        return;
                    }
                    await Task.Delay(1);
                }
                    
                #endregion
                
                simulationTickSystem = new TickSystem(simulationTickRate);
                simulationTickSystem.OnTick += TickSystemHandler.OnSimulationTick;
                stateTickSystem = new TickSystem(stateTickRate);
                stateTickSystem.OnTick += TickSystemHandler.OnStateTick;
            }
            else
            {
                if (debugMode == DebugMode.All)
                    Debug.Log("[NetRewind] Failed to start server!");
            }
        }
        #endif 

        #endregion
    }
}