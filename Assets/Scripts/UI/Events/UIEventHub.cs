using UnityEngine;

namespace Vamsurlike.UI.Events
{
    // Bootstrap DontDestroyOnLoad 오브젝트에 컴포넌트로 배치.
    // 씬 로드 전에 자동 생성되므로 어느 씬에서도 Instance가 보장된다.
    public sealed class UIEventHub : MonoBehaviour
    {
        public static UIEventHub Instance { get; private set; }

        public PlayerUIEventChannel Player { get; } = new();
        public StageUIEventChannel  Stage  { get; } = new();
        public SkillUIEventChannel  Skill  { get; } = new();
        public RewardUIEventChannel Reward { get; } = new();
        public FlowUIEventChannel   Flow   { get; } = new();

        // Bootstrap 씬에 컴포넌트가 없는 경우 자동 생성 (안전망)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[UIEventHub]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<UIEventHub>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
