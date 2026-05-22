using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Vamsurlike.Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // 서버에서만 호출. NetworkManager.SceneManager가 모든 클라이언트 씬을 동기화함.
        public void LoadSceneNetwork(string sceneName)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning($"[{nameof(SceneLoader)}] LoadSceneNetwork는 서버에서만 호출 가능합니다.");
                return;
            }
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        // 네트워크 없을 때 사용하는 로컬 씬 전환
        public void LoadSceneLocal(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
