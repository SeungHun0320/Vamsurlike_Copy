using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vamsurlike.Network
{
    /// <summary>
    /// 데디케이티드 서버 전용 콘솔 로거.
    /// UNITY_SERVER 빌드 또는 -server 인자로 실행 시 자동 활성화.
    /// Unity 로그를 그대로 stdout으로 중계하고, 서버 이벤트(접속/해제/상태 변화)를 구조화된 형식으로 출력한다.
    /// </summary>
    public class ServerConsoleLogger : MonoBehaviour
    {
        private static readonly string Prefix = "[SERVER]";
        private const int MaxBufferedEntries = 200;

        private static readonly List<string> BufferedEntries = new();

        public static event Action<string> OnEntryAdded;
        public static IReadOnlyList<string> Entries => BufferedEntries;

        private void Start()
        {
            if (!NetworkBootstrapper.IsServerBuild)
            {
                enabled = false;
                return;
            }

            Application.logMessageReceived += ForwardUnityLog;

            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    += OnClientConnected;
                GameNetworkManager.Instance.OnClientDisconnected += OnClientDisconnected;
            }

            Log($"ServerConsoleLogger 활성화 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= ForwardUnityLog;

            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    -= OnClientConnected;
                GameNetworkManager.Instance.OnClientDisconnected -= OnClientDisconnected;
            }
        }

        private static void OnClientConnected(ulong clientId)
        {
            int total = GameNetworkManager.Instance != null
                ? GameNetworkManager.Instance.ConnectedPlayerCount : 0;
            Log($"Client {clientId} 접속. 현재 {total}명");
        }

        private static void OnClientDisconnected(ulong clientId)
        {
            int total = GameNetworkManager.Instance != null
                ? GameNetworkManager.Instance.ConnectedPlayerCount : 0;
            Log($"Client {clientId} 해제. 현재 {total}명");
        }

        // Unity 로그를 stdout으로 중계 (logType별 색상 없이 순수 텍스트)
        private static void ForwardUnityLog(string message, string stackTrace, LogType type)
        {
            string tag = type switch {
                LogType.Error     => "[ERROR]",
                LogType.Exception => "[EXCEPTION]",
                LogType.Warning   => "[WARN]",
                _                 => "[LOG]"
            };
            string entry = $"{DateTime.Now:HH:mm:ss} {tag} {message}";
            Console.WriteLine($"{Prefix} {entry}");
            Publish(entry);

            if (type is LogType.Exception or LogType.Error && !string.IsNullOrEmpty(stackTrace))
                Console.WriteLine(stackTrace);
        }

        public static void Log(string message)
        {
            string entry = $"{DateTime.Now:HH:mm:ss} [SERVER] {message}";
            Console.WriteLine($"{Prefix} {entry}");
            Publish(entry);
        }

        // 같은 key 로그를 intervalSeconds 이내에 중복 출력하지 않는다.
        // 매 프레임 수십 번 호출될 수 있는 게임플레이 검증 로그에 사용.
        private static readonly Dictionary<string, float> throttleTable = new();

        public static void LogThrottled(string key, string message, float intervalSeconds = 5f)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (throttleTable.TryGetValue(key, out float last) && now - last < intervalSeconds)
                return;
            throttleTable[key] = now;
            Log(message);
        }

        private static void Publish(string entry)
        {
            BufferedEntries.Add(entry);
            if (BufferedEntries.Count > MaxBufferedEntries)
                BufferedEntries.RemoveAt(0);

            OnEntryAdded?.Invoke(entry);
        }
    }
}
