using UnityEngine;

namespace Vamsurlike.Core
{
    // Resources/Configs/SceneConfig.asset 으로 배치.
    // 씬 이름 문자열이 AudioManager/BootstrapLoader/NetworkPlayerSpawner/GameNetworkManager 등에
    // 각각 하드코딩되어 있으면 씬 이름 변경 시 일부만 갱신되는 사고가 나기 쉽다 — 단일 소스로 통합.
    [CreateAssetMenu(fileName = "SceneConfig", menuName = "Vamsurlike/Scene Config")]
    public class SceneConfigSO : ScriptableObject
    {
        private const string ResourcesPath = "Configs/SceneConfig";

        private static SceneConfigSO cachedInstance;
        public static SceneConfigSO Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<SceneConfigSO>(ResourcesPath);
                return cachedInstance;
            }
        }

        public string mainMenuSceneName = "MainMenu";
        public string stageSceneName    = "Stage_01";
    }
}
