using UnityEngine.SceneManagement;

namespace Vamsurlike.Core
{
    public static class SceneLoader
    {
        public static void Load(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public static void LoadAsync(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName);
        }

        public static string CurrentScene() => SceneManager.GetActiveScene().name;
    }
}
