using SceneManagement;
using UnityEngine;

namespace _Demo.Scene
{
    public class AutomaticSceneSwitcher : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private uint sceneGroupIndexToLoad = 0;

        private async void Start()
        {
            await SceneLoader.GetInstance().LoadSceneGroup((int) sceneGroupIndexToLoad);
        }
    }
}