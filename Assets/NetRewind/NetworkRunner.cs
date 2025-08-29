using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace NetRewind
{
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(UnityTransport))]
    public class NetworkRunner : MonoBehaviour
    {
        public static NetworkRunner Runner { get; private set; }

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
    }
}