using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace Vamsurlike.Network
{
    /// <summary>
    /// Bootstrap 씬에서 UGS 초기화 및 서버 빌드 자동 시작 담당.
    /// UNITY_SERVER 빌드 또는 커맨드라인 인자 -server 로 서버 모드를 활성화한다.
    /// 지원 인자: -server  -ip 0.0.0.0  -port 7777
    /// </summary>
    public class NetworkBootstrapper : MonoBehaviour
    {
        private static NetworkBootstrapper instance;

        public static bool IsUgsReady    { get; private set; }
        public static bool IsServerBuild { get; private set; }

        public static event Action OnUgsReady;

        private void Awake()
        {
            if (instance != null) { Destroy(this); return; }
            instance = this;
            IsUgsReady    = false;
            IsServerBuild = DetectServerBuild();
            OnUgsReady    = null;

#if UNITY_EDITOR
            string playerTags = string.Join(", ", Unity.Multiplayer.PlayMode.CurrentPlayer.Tags);
            Debug.Log($"[{nameof(NetworkBootstrapper)}] MPM 태그=[{playerTags}], 서버 모드={IsServerBuild}");
#else
            Debug.Log($"[{nameof(NetworkBootstrapper)}] 서버 모드={IsServerBuild}");
#endif
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private async void Start()
        {
            if (IsServerBuild)
            {
                StartDedicatedServer();
                return;
            }

            await InitializeUgsAsync();
        }

        private void StartDedicatedServer()
        {
            NetworkConfigSO config = NetworkConfigSO.Instance;
            string ip   = GetArgValue("-ip",   config != null ? config.defaultServerIp : "0.0.0.0");
            ushort port = GetArgPort("-port", config != null ? config.defaultPort : (ushort)7777);
            bool ok = GameNetworkManager.Instance?.StartAsServer(ip, port) ?? false;
            ServerConsoleLogger.Log($"서버 시작 — {ip}:{port} (ok={ok})");
        }

        // 서버 빌드 또는 에디터 MPM "Server" 태그 여부 — 스크립트 초기화 단계에서도 사용 가능
        public static bool IsServerMode()
        {
            if (IsServerBuild) return true;
#if UNITY_EDITOR
            foreach (string tag in Unity.Multiplayer.PlayMode.CurrentPlayer.Tags)
                if (string.Equals(tag, "Server", StringComparison.OrdinalIgnoreCase)) return true;
#endif
            return false;
        }

        private static bool DetectServerBuild()
        {
#if UNITY_SERVER
            return true;
#else
            foreach (string arg in System.Environment.GetCommandLineArgs())
            {
                if (arg.Equals("-server", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
#if UNITY_EDITOR
            // MPM 가상 플레이어에 "Server" 태그가 있으면 서버 모드로 시작
            foreach (string tag in Unity.Multiplayer.PlayMode.CurrentPlayer.Tags)
            {
                if (tag.Equals("Server", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
#endif
            return false;
#endif
        }

        // "-key value" 형식의 커맨드라인 인자에서 value 반환. 없으면 defaultValue.
        private static string GetArgValue(string key, string defaultValue)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
            return defaultValue;
        }

        private static ushort GetArgPort(string key, ushort defaultValue)
        {
            string val = GetArgValue(key, null);
            if (val == null) return defaultValue;
            if (ushort.TryParse(val, out ushort port) && port > 0) return port;

            Debug.LogWarning($"[{nameof(NetworkBootstrapper)}] 잘못된 포트 '{val}' — 기본값 {defaultValue} 사용");
            return defaultValue;
        }

        private async Task InitializeUgsAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[{nameof(NetworkBootstrapper)}] UGS 인증 완료. PlayerId: {AuthenticationService.Instance.PlayerId}");
                IsUgsReady = true;
                OnUgsReady?.Invoke();
            }
            catch (Exception e)
            {
                // 오프라인 환경에서도 로컬 플레이는 가능
                Debug.LogWarning($"[{nameof(NetworkBootstrapper)}] UGS 인증 실패 (로컬 전용 모드): {e.Message}");
            }
        }
    }
}
