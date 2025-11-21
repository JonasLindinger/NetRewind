using SceneManagement;
using UnityEngine;

namespace _Demo.Scripts.Scene
{
    public class AutomaticSceneSwitcher : MonoBehaviour
    {
        private void Start()
        {
            SceneLoader.GetInstance().LoadSceneGroup(0);
        }
    }
}