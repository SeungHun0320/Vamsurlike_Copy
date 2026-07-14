using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

namespace Vamsurlike.Network
{
    // 서버 프로세스 전용 — 플레이어별 저장 파일을 UGS PlayerId 기준으로 디스크에 보관한다.
    // 클라이언트는 이 파일에 접근하지 않는다(직접 수정으로 인한 치팅 방지가 이 설계의 목적).
    internal static class ServerSaveFileStore
    {
        private const int CurrentVersion = 1;

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public static PlayerSaveData LoadOrCreate(string playerId)
        {
            string path = PathFor(playerId);
            if (!File.Exists(path))
                return CreateDefault();

            try
            {
                var data = JsonConvert.DeserializeObject<PlayerSaveData>(File.ReadAllText(path));
                if (data == null || data.Version != CurrentVersion)
                {
                    Debug.LogWarning($"[{nameof(ServerSaveFileStore)}] '{playerId}' 세이브 버전 불일치/손상 — 기본값으로 시작.");
                    return CreateDefault();
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(ServerSaveFileStore)}] '{playerId}' 로드 실패({e.Message}) — 기본값으로 시작.");
                return CreateDefault();
            }
        }

        public static void Save(string playerId, PlayerSaveData data)
        {
            if (string.IsNullOrEmpty(playerId)) return;

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                data.Version = CurrentVersion;
                File.WriteAllText(PathFor(playerId), JsonConvert.SerializeObject(data));
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(ServerSaveFileStore)}] '{playerId}' 저장 실패: {e.Message}");
            }
        }

        private static PlayerSaveData CreateDefault() =>
            new() { Version = CurrentVersion, Gold = 0, UpgradeLevels = new int[13] };

        private static string PathFor(string playerId)
        {
            string safe = Regex.Replace(string.IsNullOrWhiteSpace(playerId) ? "unknown" : playerId, "[^a-zA-Z0-9_-]", "_");
            return Path.Combine(SaveDirectory, safe + ".json");
        }
    }
}
