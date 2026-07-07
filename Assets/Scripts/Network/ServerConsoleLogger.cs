using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vamsurlike.Network
{
    /// <summary>
    /// 데디케이티드 서버 전용 콘솔 로거.
    /// UNITY_SERVER 빌드 또는 -server 인자로 실행 시 자동 활성화.
    /// Unity의 전체 Debug.Log를 그대로 중계하지 않는다 — 서버 검증/이벤트(접속·해제, 픽업 승인/거부 등
    /// 명시적으로 Log/LogThrottled를 호출한 것)만 구조화된 형식으로 stdout + 로그 UI에 출력한다.
    /// (다른 모든 Debug.Log는 각 프로세스의 일반 콘솔에는 여전히 찍히지만, 이 로거의 버퍼/UI에는 안 뜬다.)
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

            if (GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.OnClientConnected    += OnClientConnected;
                GameNetworkManager.Instance.OnClientDisconnected += OnClientDisconnected;
            }

            Log($"ServerConsoleLogger 활성화 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        private void OnDestroy()
        {
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
