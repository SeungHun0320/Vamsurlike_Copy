using UnityEngine;

namespace Vamsurlike.Network
{
    // Resources/Configs/NetworkConfig.asset 으로 배치.
    // IP/포트 기본값이 GameNetworkManager/NetworkBootstrapper/MainMenuUI 등 여러 파일에 각각
    // 하드코딩되어 있으면 한쪽만 바꿨을 때 클라/서버 기본값이 서로 어긋날 수 있다 — 단일 소스로 통합.
    [CreateAssetMenu(fileName = "NetworkConfig", menuName = "Vamsurlike/Network Config")]
    public class NetworkConfigSO : ScriptableObject
    {
        private const string ResourcesPath = "Configs/NetworkConfig";

        private static NetworkConfigSO cachedInstance;
        public static NetworkConfigSO Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<NetworkConfigSO>(ResourcesPath);
                return cachedInstance;
            }
        }

        public string defaultClientIp = "127.0.0.1";
        public string defaultServerIp = "0.0.0.0";
        public ushort defaultPort     = 7777;
    }
}
