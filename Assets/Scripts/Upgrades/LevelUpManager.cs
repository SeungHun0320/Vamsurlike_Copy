using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.Skills;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

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

        // 클라이언트 이벤트: 이 클라이언트에 옵션이 도착했을 때 (optionIndices, currentLevels)
        public static event Action<int[], int[]> OnOptionsReceived;
        // 클라이언트 이벤트: 레벨업이 완전히 완료(모두 선택)됐을 때
        public static event Action OnLevelUpCompleted;

        // RULES.md: 시드 기반 System.Random 사용
        private readonly System.Random rng = new();
        private LevelUpOptionPicker optionPicker;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            optionPicker = new LevelUpOptionPicker(rng);
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
            return optionPicker.HasValidCatalog(UpgradeCatalog.Instance);
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

            GameFlowCoordinator.Instance?.RequestTransition(
                GameFlowState.LevelingUp,
                () => StartLevelUpFlow(newLevel));
        }

        private void StartLevelUpFlow(int newLevel)
        {
            var catalog = UpgradeCatalog.Instance;

            playerOptions.Clear();
            pendingChoices.Clear();

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (!CanClientChooseUpgrade(clientId))
                    continue;

                SkillManager skillManager = GetPlayerSkillManager(clientId);
                int[] indices = optionPicker.GenerateOptions(
                    catalog,
                    3,
                    skillManager,
                    clientId,
                    message => Debug.LogWarning($"[{nameof(LevelUpManager)}] {message}"));
                int[] levels = optionPicker.BuildCurrentLevels(indices, catalog, skillManager);
                playerOptions[clientId] = indices;
                pendingChoices.Add(clientId);

                ShowLevelUpOptionsClientRpc(indices, levels, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            if (pendingChoices.Count == 0)
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] 연결된 클라이언트 없음 — 레벨업 건너뜀");
                GameFlowCoordinator.Instance?.ReturnToGameplay();
            }
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

            if (!CanClientChooseUpgrade(clientId))
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] clientId {clientId}: dead/downed player cannot choose upgrade.");
                pendingChoices.Remove(clientId);
                playerOptions.Remove(clientId);
                CheckAllDone();
                return;
            }

            if (!playerOptions.TryGetValue(clientId, out int[] options) ||
                choiceIndex < 0 || choiceIndex >= options.Length)
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] clientId {clientId}: 유효하지 않은 선택 인덱스 {choiceIndex}");
                return;
            }

            Debug.Log($"[SubmitChoiceServerRpc] OK — clientId={clientId}, choice={choiceIndex}");
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
            NotifyLevelUpCompletedClientRpc();
            GameFlowCoordinator.Instance?.ReturnToGameplay();

            // ReturnToGameplay 이후 누적 XP 재검사 — 큐 처리 완료 후 다중 레벨 도달 대비
            if (SharedLevelSystem.Instance != null)
                SharedLevelSystem.Instance.CheckLevelUp();
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (!pendingChoices.Contains(clientId)) return;
            pendingChoices.Remove(clientId);
            CheckAllDone();
        }

        private SkillManager GetPlayerSkillManager(ulong clientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            if (client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<SkillManager>();
        }

        private bool CanClientChooseUpgrade(ulong clientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return false;
            if (client.PlayerObject == null) return false;

            var stats = client.PlayerObject.GetComponent<PlayerNetworkStats>();
            if (stats == null)
            {
                Debug.LogWarning($"[{nameof(LevelUpManager)}] clientId {clientId}: PlayerNetworkStats missing.");
                return false;
            }

            return stats.CanAct;
        }

        // 서버 → 특정 클라이언트: 해당 플레이어의 업그레이드 옵션 인덱스 + 현재 스킬 레벨 전달
        [ClientRpc]
        private void ShowLevelUpOptionsClientRpc(int[] optionIndices, int[] currentLevels, ClientRpcParams rpcParams = default)
        {
            OnOptionsReceived?.Invoke(optionIndices, currentLevels); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Reward.PublishLevelUpOptions(new LevelUpOptionsPayload(optionIndices, currentLevels));
        }

        // 서버 → 전체 클라이언트: 레벨업 완료 통보
        [ClientRpc]
        private void NotifyLevelUpCompletedClientRpc()
        {
            OnLevelUpCompleted?.Invoke(); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Reward.PublishLevelUpCompleted();
        }
    }
}
