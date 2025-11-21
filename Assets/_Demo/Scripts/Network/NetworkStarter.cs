using NetRewind;
using NetRewind.Utils;
using UnityEngine;

namespace _Demo.Scripts.Network
{
    public class NetworkStarter : MonoBehaviour
    {
        public void Start()
        {
            #if Client && Server
            NetRunner.GetInstance().Run(RunType.Host);
            #elif Client && !Server
            NetRunner.GetInstance().Run(RunType.Client);
            #elif !Client && Server
            NetRunner.GetInstance().Run(RunType.Server);
            #endif
        }
    }
}