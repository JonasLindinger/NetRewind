using Unity.Netcode;
using UnityEngine;

namespace _Demo
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("References")] 
        [SerializeField] private PlayerController playerPrefab;
        
        #if Server
        private void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            // Check if we are the server / host
            if (!NetworkManager.Singleton.IsServer) return;

            var player = Instantiate(playerPrefab);
            player.NetworkObject.SpawnWithOwnership(clientId);
        }
        #endif
    }
}