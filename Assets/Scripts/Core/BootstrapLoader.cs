using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vamsurlike.Core
{
    public class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string m_strTargetScene = "MainMenu";
        [SerializeField] private float m_fDelay = 0f;

        private void Start()
        {
            if (m_fDelay <= 0f)
            {
                LoadTargetScene();
                return;
            }
            Invoke(nameof(LoadTargetScene), m_fDelay);
        }

        private void LoadTargetScene()
        {
            SceneManager.LoadScene(m_strTargetScene);
        }
    }
}
