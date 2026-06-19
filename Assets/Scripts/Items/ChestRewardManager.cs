using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    // Stage 씬의 NetworkObject로 배치.
    // 상자 픽업 → 전원 스킬 카드 선택 UI → 적용 → Playing 복귀.
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

        // 서버 전용: NetworkedItemPickup(Chest)에서 호출
        public void BeginChestReward()
        {
            if (!IsServer) return;

            // 재진입 가드
            if (pendingChoices.Count > 0)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] 상자 보상 진행 중 — 새 상자 요청 무시");
                return;
            }

            var catalog = UpgradeCatalog.Instance;
            if (catalog == null)
            {
                Debug.LogError($"[{nameof(ChestRewardManager)}] UpgradeCatalog 없음 — 상자 건너뜀");
                return;
            }

            playerOptions.Clear();
            pendingChoices.Clear();

            bool anyHasCards = false;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                var skillManager = GetSkillManager(clientId);
                ChestChoiceData[] choices = choiceBuilder.Build(catalog, skillManager);

                if (choices.Length == 0)
                {
                    SharedLevelSystem.Instance?.AddXP(fallbackXP);
                    Debug.Log($"[{nameof(ChestRewardManager)}] clientId {clientId} 스킬 없음 → XP +{fallbackXP}");
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
            {
                Debug.Log($"[{nameof(ChestRewardManager)}] 전원 스킬 없음 — UI 생략");
                return;
            }

            StageRuntime.Instance?.SetGameState(GameState.ChestOpening);
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

            if (!playerOptions.TryGetValue(clientId, out ChestChoiceData[] options) ||
                choiceIndex < 0 || choiceIndex >= options.Length)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: 유효하지 않은 인덱스 {choiceIndex}");
                return;
            }

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
            StageRuntime.Instance?.SetGameState(GameState.Playing);
            NotifyCompletedClientRpc();

            SharedLevelSystem.Instance?.CheckLevelUp();
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (!pendingChoices.Contains(clientId)) return;
            pendingChoices.Remove(clientId);
            Debug.Log($"[{nameof(ChestRewardManager)}] clientId {clientId} 이탈 — 대기 제거");
            CheckAllDone();
        }

        private Skills.SkillManager GetSkillManager(ulong clientId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            if (client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<Skills.SkillManager>();
        }

        [ClientRpc]
        private void ShowOptionsClientRpc(ChestChoiceData[] choices, ClientRpcParams rpcParams = default)
        {
            OnOptionsReceived?.Invoke(choices);
        }

        [ClientRpc]
        private void NotifyCompletedClientRpc()
        {
            OnChestRewardCompleted?.Invoke();
        }
    }
}
