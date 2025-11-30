using NetRewind.Utils;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.PatrollingPlatform
{
    public class PatrollingPlatformSpawner : MonoBehaviourSingleton<PatrollingPlatformSpawner>
    {
        public static Transform PointA => GetInstance().pointA;
        public static Transform PointB => GetInstance().pointB;
        
        [Header("References")]
        [SerializeField] private PatrollingPlatformController platformPrefab;
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        
        private void Start()
        {
            NetworkManager.Singleton.OnServerStarted += Spawn;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnServerStarted -= Spawn;
        }

        private void Spawn()
        {
            PatrollingPlatformController platform = Instantiate(platformPrefab, pointA.position, Quaternion.identity);
            platform.NetworkObject.Spawn();
        }
    }
}