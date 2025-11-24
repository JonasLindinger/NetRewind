using System;
using NetRewind.Utils;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    public class AutomaticPlayerSpawner : MonoBehaviourSingleton<AutomaticPlayerSpawner>
    {
        [Header("Prefab")]
        [SerializeField] private NetworkBehaviour playerPrefab;

        private void Start()
        {
            #if Server
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;
            #endif
        }

        private void OnDestroy()
        {
            #if Server
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
            #endif
        }

        #if Server
        private void SpawnPlayer(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            NetworkBehaviour player = Instantiate(playerPrefab, transform.position, Quaternion.identity);
            player.NetworkObject.SpawnWithOwnership(clientId);
        }
        #endif
    }
}