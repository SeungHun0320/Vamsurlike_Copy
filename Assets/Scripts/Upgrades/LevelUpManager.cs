using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.Stage;

namespace Vamsurlike.Upgrades
{
    // SharedLevelSystem과 같은 NetworkObject에 배치.
    // 레벨업 선택 흐름 전체를 담당 (옵션 생성 → RPC 전송 → 선택 수집 → 업그레이드 적용).
    public class LevelUpManager : NetworkBehaviour
    {
        public static LevelUpManager Instance { get; private set; }

        // 서버: clientId → 전송한 옵션 인덱스 배열
        private readonly Dictionary<ulong, int[]> playerOptions  = new();
        // 서버: 아직 선택하지 않은 플레이어 집합
        private readonly HashSet<ulong>           pendingChoices = new();

        // 클라이언트 이벤트: 이 클라이언트에 옵션이 도착했을 때
        public static event Action<int[]> OnOptionsReceived;
        // 클라이언트 이벤트: 레벨업이 완전히 완료(모두 선택)됐을 때
        public static event Action OnLevelUpCompleted;

        // 중복 방지용 시드 기반 랜덤
        private readonly System.Random rng = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        // SharedLevelSystem이 XP 차감 전에 호출해 사전 검증 — null 엔트리도 걸러냄
        public bool HasValidCatalog()
        {
            var catalog = UpgradeCatalog.Instance;
            if (catalog == null) 
                return false;
            foreach (var opt in catalog.options)
                if (opt != null) 
                    return true;
            return false;
        }

        // 서버 전용: SharedLevelSystem에서 레벨업 조건 달성 시 호출
        public void BeginLevelUp(int newLevel)
        {
            if (!IsServer) return;

            if (!HasValidCatalog())
            {
                Debug.LogError($"[{nameof(LevelUpManager)}] 유효한 UpgradeCatalog 옵션 없음 — 레벨업 건너뜀");
                return;
            }
            var catalog = UpgradeCatalog.Instance;

            playerOptions.Clear();
            pendingChoices.Clear();

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                int[] indices = GenerateRandomOptions(catalog, 3);
                playerOptions[clientId] = indices;
                pendingChoices.Add(clientId);

                ShowLevelUpOptionsClientRpc(indices, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            if (pendingChoices.Count == 0)
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] 연결된 클라이언트 없음 — 레벨업 건너뜀");
                return;
            }

            StageRuntime.Instance?.SetGameState(GameState.LevelingUp);
        }

        // 클라이언트 → 서버: 플레이어가 카드를 선택했을 때
        [ServerRpc(RequireOwnership = false)]
        public void SubmitChoiceServerRpc(int choiceIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!pendingChoices.Contains(clientId))
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] clientId {clientId}: 대기 목록에 없음");
                return;
            }

            if (!playerOptions.TryGetValue(clientId, out int[] options) ||
                choiceIndex < 0 || choiceIndex >= options.Length)
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] clientId {clientId}: 유효하지 않은 선택 인덱스 {choiceIndex}");
                return;
            }

            pendingChoices.Remove(clientId);
            ApplyUpgrade(clientId, options[choiceIndex]);
            CheckAllDone();
        }

        private void ApplyUpgrade(ulong clientId, int catalogIndex)
        {
            var catalog = UpgradeCatalog.Instance;
            if (catalog == null || !catalog.IsValidIndex(catalogIndex)) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject == null) return;

            var handler = client.PlayerObject.GetComponent<PassiveStatHandler>();
            if (handler != null)
                handler.ApplyUpgrade(catalog.options[catalogIndex]);
            else
                Debug.LogWarning($"[{nameof(LevelUpManager)}] clientId {clientId}: PassiveStatHandler 없음");
        }

        private void CheckAllDone()
        {
            if (pendingChoices.Count > 0) return;
            FinalizeLevelUp();
        }

        private void FinalizeLevelUp()
        {
            playerOptions.Clear();
            StageRuntime.Instance?.SetGameState(GameState.Playing);
            NotifyLevelUpCompletedClientRpc();

            // Fix: Playing 복귀 직후 누적 XP 재검사 — in-flight pickup이나 다중 레벨 도달 대비
            if (SharedLevelSystem.Instance != null)
                SharedLevelSystem.Instance.CheckLevelUp();
        }

        // 레벨업 선택 중 클라이언트 연결 끊김 처리
        private void HandleClientDisconnect(ulong clientId)
        {
            if (!pendingChoices.Contains(clientId)) return;
            pendingChoices.Remove(clientId);
            Debug.Log($"[{nameof(LevelUpManager)}] clientId {clientId} 이탈 — pendingChoices에서 제거");
            CheckAllDone();
        }

        private int[] GenerateRandomOptions(UpgradeCatalog catalog, int count)
        {
            // null 엔트리를 제외한 유효 인덱스만 풀에 넣는다
            var pool = new List<int>(catalog.options.Length);
            for (int i = 0; i < catalog.options.Length; i++)
                if (catalog.options[i] != null) pool.Add(i);

            count = Mathf.Min(count, pool.Count);

            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(pool.Count);
                result[i] = pool[pick];
                pool.RemoveAt(pick);
            }
            return result;
        }

        // 서버 → 특정 클라이언트: 해당 플레이어의 업그레이드 옵션 인덱스 전달
        [ClientRpc]
        private void ShowLevelUpOptionsClientRpc(int[] optionIndices, ClientRpcParams rpcParams = default)
        {
            OnOptionsReceived?.Invoke(optionIndices);
        }

        // 서버 → 전체 클라이언트: 레벨업 완료 통보
        [ClientRpc]
        private void NotifyLevelUpCompletedClientRpc()
        {
            OnLevelUpCompleted?.Invoke();
        }
    }
}
