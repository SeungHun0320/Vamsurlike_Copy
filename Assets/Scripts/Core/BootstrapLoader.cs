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
            string sceneName = SceneConfigSO.Instance != null ? SceneConfigSO.Instance.mainMenuSceneName : mainMenuSceneName;
            SceneManager.LoadScene(sceneName);
        }
    }
}
