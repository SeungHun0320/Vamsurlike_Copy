using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vamsurlike.Core
{
    public class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private void Start()
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
