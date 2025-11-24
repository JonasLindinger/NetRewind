using NetRewind.Utils.Simulation;
using UnityEngine;

namespace _Demo.Scripts.Player
{
    public class PlayerController : InputPredictedNetworkObject
    {
        protected override void OnTick(uint tick)
        {
            Debug.Log("> " + tick + " -> " + GetButton(4));
        }
    }
}