using NetRewind.Utils;
using Unity.Netcode;
using UnityEngine;

namespace _Demo.Scripts.Car
{
    public class CarSpawner : MonoBehaviourSingleton<CarSpawner>
    {
        [Header("References")]
        [SerializeField] private CarController carPrefab;

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
            CarController platform = Instantiate(carPrefab, transform.position, Quaternion.identity);
            platform.NetworkObject.Spawn();
        }
    }
}