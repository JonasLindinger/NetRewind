using UnityEngine;

namespace NetRewind.Demo.Application
{
    public static class FrameLimiter
    {
        private static uint targetFrameRate = 240;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            UnityEngine.Application.targetFrameRate = (int) targetFrameRate;
        }
    }
}