using UnityEngine;
using UnityEngine.SceneManagement;
using Vamsurlike.Network;

namespace Vamsurlike.Core
{
    public class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Start()
        {
            StartupValidator.ValidateBootstrap(PoolManager.Instance);
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
