using System;
using NetRewind.Utils;
using NetRewind.Utils.Input;
using NetRewind.Utils.Simulation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace NetRewind
{
    public class NetRunner : MonoBehaviourSingleton<NetRunner>
    {
        public static uint EventPackageLossToAccountFor;
        
        [Header("Simulation")]
        [SerializeField] private uint simulationTickRate = 60;
        [SerializeField] private PhysicsMode physicsMode = PhysicsMode.Sync;
        [FormerlySerializedAs("sendingMode")]
        [Space(5)]
        [Header("Input sending")]
        [SerializeField] private SendingMode inputSendingMode = SendingMode.Full;
        [SerializeField] private uint inputPackageLoss = 4;
        [Space(5)] 
        [Header("State sending")]
        [SerializeField] private uint eventPackageLossToAccountFor = 5;
        [Space(5)]
        [Header("Transport Layer")]
        [SerializeField] private GameObject transportLayerPrefab;
        [Space(5)]
        [Header("Network connection")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private NetworkTransport transport;

        #region Getters

        public PhysicsMode PhysicsMode => physicsMode;
        public SendingMode InputSendingMode => inputSendingMode;
        public uint InputPackageLoss => inputPackageLoss;
        public ulong GetRTT(ulong clientId) => transport.GetCurrentRtt(clientId);
        public ulong ServerClientId => transport.ServerClientId;
        public ulong ServerRTT => GetRTT(ServerClientId);
        public uint TicksPassedBetweenServerAndClientRPC(uint tickRate)  {
            ulong ms = GetInstance().ServerRTT / 2;
            float msPerTick = 1000f / tickRate;
            uint passedTicks = (uint) (ms / msPerTick);
            return passedTicks;
        }
        
        #endregion

        private void Start()
        {
            EventPackageLossToAccountFor = eventPackageLossToAccountFor;
            networkManager.NetworkConfig.EnableSceneManagement = false;
        }

        private void Update()
        {
            Simulation.Update(Time.deltaTime);   
        }

        #if Client
        private void LateUpdate()
        {
            Simulation.HandleReconciliation();
        }
        #endif

        private void OnDestroy()
        {
            #if Server
            if (networkManager.IsServer)
                networkManager.OnClientConnectedCallback -= CreateTransportLayer;

            Simulation.StopTickSystem();
            #endif
        }

        #region Starters (Runners)
        
        public void Run(RunType runType)
        {
            switch (runType)
            {
                case RunType.Server:
                    #if Server
                    RunAsServer();
                    #else
                    Debug.LogWarning("This isn't a server build!");
                    #endif
                    break;
                
                case RunType.Client:
                    #if Client
                    RunAsClient();
                    #else
                    Debug.LogWarning("This isn't a client build!");
                    #endif
                    break;
                
                case RunType.Host:
                    #if Client && Server
                    RunAsHost();
                    #else
                    Debug.LogWarning("This build version doesn't support hosting!");
                    #endif
                    break;
            }
        }

        #if Server
        private void RunAsServer()
        {
            networkManager.NetworkConfig.TickRate = simulationTickRate;
            networkManager.OnClientConnectedCallback += CreateTransportLayer;
            networkManager.StartServer();
            Simulation.StartTickSystem(simulationTickRate, 0);
            Application.targetFrameRate = (int) simulationTickRate;
        }
        #endif

        #if Client
        private void RunAsClient()
        {
            networkManager.NetworkConfig.TickRate = simulationTickRate;
            networkManager.StartClient();
        }
        #endif        
    
        #if Client && Server
        private void RunAsHost()
        {
            networkManager.NetworkConfig.TickRate = simulationTickRate;
            networkManager.OnClientConnectedCallback += CreateTransportLayer;
            networkManager.StartHost();
            Simulation.StartTickSystem(simulationTickRate, 0);
        }
        #endif
        
        #endregion

        #region Events

        #if Server
        private void CreateTransportLayer(ulong clientId)
        {
            Debug.Log("Creating transport layer for client: " + clientId);
            // Create GameObject
            GameObject obj = Instantiate(transportLayerPrefab, Vector3.zero, Quaternion.identity);
            DontDestroyOnLoad(obj);
            
            // Add components
            InputTransportLayer layer = obj.GetComponent<InputTransportLayer>();
            
            // Disable syncing, hide for everyone except Server and client who owns it, and Spawn
            layer.NetworkObject.SpawnWithObservers = false;            
            layer.NetworkObject.SynchronizeTransform = false;
            layer.NetworkObject.AutoObjectParentSync = false;
            layer.NetworkObject.SyncOwnerTransformWhenParented = false;
            layer.NetworkObject.SceneMigrationSynchronization = false;
            layer.NetworkObject.SpawnWithOwnership(clientId);
            layer.NetworkObject.NetworkShow(clientId);
        }
        #endif

        #endregion
    }
}