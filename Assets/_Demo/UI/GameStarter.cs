using NetRewind;
using UnityEngine;

namespace _Demo.UI
{
    public class GameStarter : MonoBehaviour
    {
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