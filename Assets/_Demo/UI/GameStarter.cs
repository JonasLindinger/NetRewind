using System;
using NetRewind;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.UI
{
    public class GameStarter : MonoBehaviour
    {
        [Header("References")] 
        [SerializeField] private GameObject ui;
        
        private void Start()
        {
            NetworkManager.Singleton.OnServerStarted += () =>
            {
                ui.SetActive(false);
            };
            NetworkManager.Singleton.OnClientStopped += (_) =>
            {
                ui.SetActive(false);
            };
        }

        public void StartClient()
        {
            NetworkRunner.Runner.StartClient();
        }

        public void StartHost()
        {
            NetworkRunner.Runner.StartHost();
        }
        
        public void StartServer()
        {
            NetworkRunner.Runner.StartServer();
        }
    }
}