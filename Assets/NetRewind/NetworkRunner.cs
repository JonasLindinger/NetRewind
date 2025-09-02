using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NetRewind.DONOTUSE;
using NetRewind.Utils;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

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
        
        /// Getters
        public DebugMode DebugMode => debugMode;
        public uint CurrentTick => simulationTickSystem.Tick;
        #if Client
        public ulong GetCurrentRtt(ulong connectionId) => networkTransport.GetCurrentRtt(connectionId);
        public ulong ServerClientId => networkTransport.ServerClientId;
        public ulong GetRTTToServer() => GetCurrentRtt(ServerClientId);
        public uint SimulationTickRate => simulationTickRate;
        public uint InputsPerSecond => inputsPerSecond;
        public uint ClientServerInputBuffer => clientServerInputBuffer;
        public uint MaxTickRecalculation => maxTickRecalculation;
        public uint InputBufferOnClient => inputBufferOnClient;
        public uint StateBufferOnClient => stateBufferOnClient;
        #endif
        #if Server
        public uint InputBufferOnServer => inputBufferOnServer;
        public uint StateBufferOnServer => stateBufferOnServer;
        #endif
        
        /// <summary>
        /// Maximum allowed tick rate
        /// </summary>
        private const float MAX_TICK_RATE = 256;

        /// <summary>
        /// Timeouts in seconds
        /// </summary>
        private const float START_SERVER_TIMEOUT = 5f;
        private const float START_CLIENT_TIMEOUT = 5f;
        private const float START_HOST_TIMEOUT = 5f;
        
        /// <summary>
        /// Unity Editor Settings
        /// </summary>
        [Header("Tick Rates")] 
        [SerializeField] private uint simulationTickRate = 64;
        [SerializeField] private uint inputTickRate = 32;
        [SerializeField] private uint stateTickRate = 32;
        [Space(10)] 
        [Header("Buffers")] 
        [SerializeField] private uint clientServerInputBuffer = 3;
        [SerializeField] private uint maxTickRecalculation = 10;
        [SerializeField] private uint inputBufferOnClient = 128;
        [SerializeField] private uint inputBufferOnServer = 256;
        [SerializeField] private uint stateBufferOnClient = 256;
        [SerializeField] private uint stateBufferOnServer = 256;
        [Space(10)] 
        [Header("Input")] 
        [SerializeField] private string networkInputMapName = "Networked";
        [Space(10)] 
        [Header("Settings")] 
        [SerializeField] private bool autoStartServer = false;
        [SerializeField] private DebugMode debugMode = DebugMode.All;
        [Space(10)] 
        [Header("References")]
        [SerializeField] private NetworkClientConnection networkClientConnectionPrefab;
        [SerializeField] private NetworkTransport networkTransport;
        [SerializeField] private PlayerInput playerInput;
        
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
        /// <summary>
        /// The amount of inputs one input package (rpc) should contain. ((sendInterval / tickInterval) x (k + margin))
        /// </summary>
        private uint inputsPerSecond;
        #endif
        #if Server
        private TickSystem stateTickSystem;
        private TickSystem syncTickSystem;
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
            
            #if Client
            float sendInterval = 1f / inputTickRate;
            float tickInterval = 1f / simulationTickRate;
            float k = 3; // The amount of input packages that can be lost in a streak. Todo: expose this in a setting. and update this inputsPerSecond
            float safetyMargin = ClientServerInputBuffer;
            inputsPerSecond = (uint) Mathf.CeilToInt(((sendInterval / tickInterval) * k) + safetyMargin);
            #endif
            
            SetUpNetworkManager();
            SetUpNetworkInput();
            
            #if Server
            // Subscribe to events
            networkManager.OnClientConnectedCallback += OnPlayerJoined;
            networkManager.OnClientDisconnectCallback += OnPlayerLeft;
            #endif
            #if Client
            NetworkClientConnection.OnStartTickSystem += StartClientTickSystem;
            SyncTickSystem.CalculateExtraTicks += OnCalculateExtraTicks;
            SyncTickSystem.CalculateLessTicks += OnCalculateLessTicks;
            SyncTickSystem.SetTick += OnSetTick;
            #endif
            
            #if Server && !Client
            // Limit fps
            Application.targetFrameRate = (int) simulationTickRate;
            
            // Auto Start Server
            if (autoStartServer)
                StartServer();
            #endif
        }

        private void OnDestroy()
        {
            #if Server
            // Subscribe to events
            networkManager.OnClientConnectedCallback -= OnPlayerJoined;
            networkManager.OnClientDisconnectCallback -= OnPlayerLeft;
            #endif
            #if Client
            NetworkClientConnection.OnStartTickSystem -= StartClientTickSystem;
            SyncTickSystem.CalculateExtraTicks -= OnCalculateExtraTicks;
            SyncTickSystem.CalculateLessTicks -= OnCalculateLessTicks;
            SyncTickSystem.SetTick -= OnSetTick;
            #endif
        }

        private void Update()
        {
            simulationTickSystem?.Update(Time.deltaTime);
            #if Server
            stateTickSystem?.Update(Time.deltaTime);
            syncTickSystem?.Update(Time.deltaTime);
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
            if (networkManager.StartClient())
            {
                #region Timeout Check
                var sw = Stopwatch.StartNew();
                var timeout = TimeSpan.FromSeconds(START_CLIENT_TIMEOUT);

                while (!networkManager.IsConnectedClient)
                {
                    if (sw.Elapsed >= timeout)
                    {
                        // Timeout
                        if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                            Debug.LogError("[NetRewind] Timed out trying to start client!");
                        return;
                    }
                    
                    await Task.Delay(10);
                }
                    
                #endregion
                
                if (debugMode == DebugMode.All)
                    Debug.Log("[NetRewind] Started client!");
            }
            else
            {
                if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                    Debug.LogError("[NetRewind] Failed to start client!");
            }
        }
        #endif

        #if Client && Server
        public async Task StartHost()
        {
            if (networkManager.StartHost())
            {
                #region Timeout Check
                var sw = Stopwatch.StartNew();
                var timeout = TimeSpan.FromSeconds(START_HOST_TIMEOUT);
                
                while (!(networkManager.IsConnectedClient && networkManager.IsServer && NetworkManager.Singleton.IsListening))
                {
                    if (sw.Elapsed >= timeout)
                    {
                        // Timeout
                        if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                            Debug.LogError("[NetRewind] Timed out trying to start host!");
                            
                        return;
                    }
                    
                    await Task.Delay(10);
                }
                    
                #endregion

                StartClientTickSystem(0); // Start Simulation and Input Tick System
                StartServerTickSystem(); // Start State and Sync Tick System
                
                if (debugMode == DebugMode.All)
                    Debug.Log("[NetRewind] Started host!");
            }
            else
            {
                if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                    Debug.LogError("[NetRewind] Failed to start host!");
            }
        }
        #endif

        #if Server
        public async Task StartServer()
        {
            if (networkManager.StartServer())
            {
                #region Timeout Check
                var sw = Stopwatch.StartNew();
                var timeout = TimeSpan.FromSeconds(START_SERVER_TIMEOUT);
                
                while (!(networkManager.IsServer && NetworkManager.Singleton.IsListening))
                {
                    if (sw.Elapsed >= timeout)
                    {
                        // Timeout
                        if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                            Debug.LogError("[NetRewind] Timed out trying to start server!");
                            
                        return;
                    }
                    
                    await Task.Delay(1);
                }
                    
                #endregion

                StartSimulationTickSystem(0); // Start Simulation Tick System
                StartServerTickSystem(); // Start State and Sync Tick System
                
                if (debugMode == DebugMode.All)
                    Debug.Log("[NetRewind] Started server!");
            }
            else
            {
                if (debugMode != DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                    Debug.LogError("[NetRewind] Failed to start server!");
            }
        }
        #endif

        #endregion

        #region Events

        #if Server
        private void OnPlayerJoined(ulong clientId)
        {
            // Instantiate player transport prefab
            if (networkManager.IsServer)
                InstantiateTransportPrefab(clientId);
        }
        
        private void OnPlayerLeft(ulong clientId)
        {
            
        }
        #endif
        
        #if Client
        private void OnSetTick(uint tick)
        {
            simulationTickSystem.SetTick(tick);
            if (debugMode == DebugMode.ErrorsOnly || debugMode == DebugMode.All)
                Debug.LogWarning("[NetRewind] Set Simulation Tick");
        }

        private void OnCalculateExtraTicks(uint ticks)
        {
            simulationTickSystem.CalculateExtraTicks(ticks);
            if (debugMode == DebugMode.All)
                Debug.Log("[NetRewind] Calculating extra ticks: " + ticks);
        }
        
        private void OnCalculateLessTicks(uint ticks)
        {
            simulationTickSystem.SkipTicks(ticks);
            if (debugMode == DebugMode.All)
                Debug.Log("[NetRewind] Skipping ticks: " + ticks);
        }
        
        #endif
        #endregion
        
        #region Private Methods

        private void InstantiateTransportPrefab(ulong clientId)
        {
            var connection = Instantiate(networkClientConnectionPrefab, Vector3.zero, Quaternion.identity);
            var networkObject = connection.NetworkObject;
            networkObject.AlwaysReplicateAsRoot = false;
            networkObject.SynchronizeTransform = false;
            networkObject.ActiveSceneSynchronization = false;
            networkObject.SceneMigrationSynchronization = false;
            networkObject.SpawnWithObservers = false;
            networkObject.DontDestroyWithOwner = true;
            networkObject.AutoObjectParentSync = false;
            networkObject.SyncOwnerTransformWhenParented = false;
            networkObject.AllowOwnerToParent = false;
            networkObject.SpawnWithOwnership(clientId);
            networkObject.NetworkShow(clientId);
        }

        private void StartSimulationTickSystem(uint simulationTickOffset)
        {
            simulationTickSystem = new TickSystem(simulationTickRate, simulationTickOffset);
            simulationTickSystem.OnTick += GameStateSync.SaveGameState; // Save the state
            #if Client
            simulationTickSystem.OnTick += InputCollector.Collect; // Collect input (first)
            #endif
            simulationTickSystem.OnTick += NetworkEntity.TriggerSimulationTick; // Do the Tick
        }
        
        #if Client
        private void StartClientTickSystem(uint simulationTickOffset)
        {
            StartSimulationTickSystem(simulationTickOffset); // Start Simulation Tick System
            inputTickSystem = new TickSystem(inputTickRate); // Start Input Tick System
            inputTickSystem.OnTick += InputSender.SendInputs;
        }
        #endif
        
        #if Server
        private void StartServerTickSystem()
        {
            stateTickSystem = new TickSystem(stateTickRate);
            stateTickSystem.OnTick += GameStateSync.SendGameState;
            uint syncTickRate = 1; // Sync every second
            syncTickSystem = new TickSystem(syncTickRate);
            syncTickSystem.OnTick += SyncTickSystem.UpdateSystem;
        }
        #endif
        
        private void SetUpNetworkInput()
        {
            InputCollector.SetUp(networkInputMapName, playerInput);
        }
        
        #endregion
    }
}