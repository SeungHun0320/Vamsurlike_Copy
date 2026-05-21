using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vamsurlike.Core
{
    public class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string targetScene = "MainMenu";
        [SerializeField] private float  delay       = 0f;

        private void Start()
        {
            if (delay <= 0f)
            {
                LoadTargetScene();
                return;
            }
            Invoke(nameof(LoadTargetScene), delay);
        }

        private void LoadTargetScene()
        {
            SceneManager.LoadScene(targetScene);
        }
    }
}
