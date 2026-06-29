using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    // Stage 씬의 NetworkObject로 배치.
    // 상자 픽업 → 전원 스킬 카드 선택 UI → 적용 → Gameplay 복귀.
    // 유효한 스킬 카드가 없으면 UI 없이 XP만 지급.
    public class ChestRewardManager : NetworkBehaviour
    {
        public static ChestRewardManager Instance { get; private set; }

        private readonly Dictionary<ulong, ChestChoiceData[]> playerOptions = new();
        private readonly HashSet<ulong>                      pendingChoices = new();

        public static event Action<ChestChoiceData[]> OnOptionsReceived;
        public static event Action        OnChestRewardCompleted;

        [SerializeField] private int fallbackXP = 30;

        private readonly System.Random rng = new();
        private ChestChoiceBuilder choiceBuilder;
        private ChestRewardApplier rewardApplier;

        private void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
            choiceBuilder = new ChestChoiceBuilder(rng);
            rewardApplier = new ChestRewardApplier();
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

        // 서버 전용: NetworkedItemPickup(Chest)에서 호출.
        // 큐에 적재하고 항상 true 반환 — 상자는 현재 Flow와 무관하게 즉시 소멸.
        public bool BeginChestReward()
        {
            if (!IsServer) return false;
            GameFlowCoordinator.Instance?.RequestTransition(GameFlowState.ChestOpening, StartChestFlow);
            return true;
        }

        private void StartChestFlow()
        {
            var catalog = UpgradeCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogError($"[{nameof(ChestRewardManager)}] UpgradeCatalog 없음 — 상자 건너뜀");
                GameFlowCoordinator.Instance?.ReturnToGameplay();
                return;
            }

            playerOptions.Clear();
            pendingChoices.Clear();

            bool anyHasCards = false;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (!CanClientChooseReward(clientId))
                    continue;

                var skillManager = GetSkillManager(clientId);
                ChestChoiceData[] choices = choiceBuilder.Build(catalog, skillManager);

                if (choices.Length == 0)
                {
                    SharedLevelSystem.Instance?.AddXP(fallbackXP);
                    continue;
                }

                playerOptions[clientId] = choices;
                pendingChoices.Add(clientId);
                anyHasCards = true;

                ShowOptionsClientRpc(choices, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            if (!anyHasCards)
                GameFlowCoordinator.Instance?.ReturnToGameplay();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitChoiceServerRpc(int choiceIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!pendingChoices.Contains(clientId))
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: 대기 목록에 없음");
                return;
            }

            if (!CanClientChooseReward(clientId))
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: dead/downed player cannot choose reward.");
                pendingChoices.Remove(clientId);
                playerOptions.Remove(clientId);
                CheckAllDone();
                return;
            }

            if (!playerOptions.TryGetValue(clientId, out ChestChoiceData[] options) ||
                choiceIndex < 0 || choiceIndex >= options.Length)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: 유효하지 않은 인덱스 {choiceIndex}");
                return;
            }

            Debug.Log($"[SubmitChoiceServerRpc] OK — clientId={clientId}, choice={choiceIndex}");
            pendingChoices.Remove(clientId);
            ApplyChoice(clientId, options[choiceIndex]);
            CheckAllDone();
        }

        private void ApplyChoice(ulong clientId, ChestChoiceData choice)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject == null) return;

            rewardApplier.Apply(client.PlayerObject.gameObject, choice);
        }

        private void CheckAllDone()
        {
            if (pendingChoices.Count > 0) return;

            playerOptions.Clear();
            // NotifyCompleted를 ReturnToGameplay보다 먼저 전송해야 한다.
            // ReturnToGameplay가 큐를 즉시 소비하면 StartChestFlow → ShowOptionsClientRpc가
            // 같은 프레임에 전송되어, 클라이언트가 수신 시 ShowOptions → NotifyCompleted 순서로
            // 도착해 두 번째 상자 UI를 즉시 닫아버리는 버그가 발생한다.
            NotifyCompletedClientRpc();
            GameFlowCoordinator.Instance?.ReturnToGameplay();

            SharedLevelSystem.Instance?.CheckLevelUp();
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (!pendingChoices.Contains(clientId)) return;
            pendingChoices.Remove(clientId);
            CheckAllDone();
        }

        private Skills.SkillManager GetSkillManager(ulong clientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            if (client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<Skills.SkillManager>();
        }

        private bool CanClientChooseReward(ulong clientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return false;
            if (client.PlayerObject == null) return false;

            var stats = client.PlayerObject.GetComponent<PlayerNetworkStats>();
            if (stats == null)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: PlayerNetworkStats missing.");
                return false;
            }

            return stats.CanAct;
        }

        [ClientRpc]
        private void ShowOptionsClientRpc(ChestChoiceData[] choices, ClientRpcParams rpcParams = default)
        {
            OnOptionsReceived?.Invoke(choices); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Reward.PublishChestOptions(new ChestOptionsPayload(choices));
        }

        [ClientRpc]
        private void NotifyCompletedClientRpc()
        {
            OnChestRewardCompleted?.Invoke(); // 임시 경유지 (Phase 8 마이그레이션 완료 후 제거)
            UIEventHub.Instance?.Reward.PublishChestRewardCompleted();
        }
    }
}
