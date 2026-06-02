using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Stage;
using Vamsurlike.Upgrades;

namespace Vamsurlike.Items
{
    // Stage 씬의 NetworkObject로 배치. LevelUpManager와 별도 책임.
    // 상자 픽업 → 전원 스킬 선택 → 진화/업그레이드 적용 → GameState → Playing
    public class ChestRewardManager : NetworkBehaviour
    {
        public static ChestRewardManager Instance { get; private set; }

        private readonly Dictionary<ulong, ChestChoiceData[]> playerOptions  = new();
        private readonly HashSet<ulong>                       pendingChoices  = new();

        public static event Action<ChestChoiceData[]> OnOptionsReceived;
        public static event Action                    OnChestRewardCompleted;

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

        // 서버 전용: NetworkedItemPickup(Chest)에서 호출
        public void BeginChestReward()
        {
            if (!IsServer) return;

            var upgradeCatalog = UpgradeCatalog.Instance;
            var recipeCatalog  = CombineRecipeCatalog.Instance;

            if (upgradeCatalog == null)
            {
                Debug.LogError($"[{nameof(ChestRewardManager)}] UpgradeCatalog 없음 — 상자 건너뜀");
                return;
            }

            playerOptions.Clear();
            pendingChoices.Clear();

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                ChestChoiceData[] cards = GenerateCards(clientId, upgradeCatalog, recipeCatalog);
                playerOptions[clientId] = cards;
                pendingChoices.Add(clientId);

                ShowChestOptionsClientRpc(cards, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                });
            }

            if (pendingChoices.Count == 0)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] 연결된 클라이언트 없음 — 상자 건너뜀");
                return;
            }

            StageRuntime.Instance?.SetGameState(GameState.ChestOpening);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SubmitChoiceServerRpc(int cardIndex, ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!pendingChoices.Contains(clientId))
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: 대기 목록에 없음");
                return;
            }

            if (!playerOptions.TryGetValue(clientId, out var cards)
                || cardIndex < 0 || cardIndex >= cards.Length)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: 유효하지 않은 카드 인덱스 {cardIndex}");
                return;
            }

            pendingChoices.Remove(clientId);
            ApplyChoice(clientId, cards[cardIndex]);
            CheckAllDone();
        }

        private void ApplyChoice(ulong clientId, ChestChoiceData choice)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)) return;
            if (client.PlayerObject == null) return;

            var skillManager = client.PlayerObject.GetComponent<Skills.SkillManager>();
            if (skillManager == null)
            {
                Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: SkillManager 없음");
                return;
            }

            switch (choice.type)
            {
                case ChestChoiceType.Evolution:
                    if (!CombineSystem.TryEvolve(skillManager, choice.index))
                        Debug.LogWarning($"[{nameof(ChestRewardManager)}] clientId {clientId}: 진화 실패 (recipeIndex={choice.index})");
                    break;

                case ChestChoiceType.UpgradeOption:
                    var catalog = UpgradeCatalog.Instance;
                    if (catalog != null && catalog.IsValidIndex(choice.index))
                    {
                        var handler = client.PlayerObject.GetComponent<Upgrades.PassiveStatHandler>();
                        if (handler != null)
                            handler.ApplyUpgrade(catalog.options[choice.index]);
                    }
                    break;
            }
        }

        private void CheckAllDone()
        {
            if (pendingChoices.Count > 0) return;
            FinalizChestReward();
        }

        private void FinalizChestReward()
        {
            playerOptions.Clear();
            StageRuntime.Instance?.SetGameState(GameState.Playing);
            NotifyChestCompletedClientRpc();

            // 상자 선택 중 누적된 XP 재검사
            if (SharedLevelSystem.Instance != null)
                SharedLevelSystem.Instance.CheckLevelUp();
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            if (!pendingChoices.Contains(clientId)) return;
            pendingChoices.Remove(clientId);
            Debug.Log($"[{nameof(ChestRewardManager)}] clientId {clientId} 이탈 — pendingChoices에서 제거");
            CheckAllDone();
        }

        // 플레이어별 카드 생성: 진화 카드 선배치 → 빈 슬롯은 일반 업그레이드
        private ChestChoiceData[] GenerateCards(ulong clientId, UpgradeCatalog upgradeCatalog,
                                                CombineRecipeCatalog recipeCatalog)
        {
            var result = new List<ChestChoiceData>();

            // 진화 카드 선배치
            if (recipeCatalog != null
                && NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)
                && client.PlayerObject != null)
            {
                var skillManager = client.PlayerObject.GetComponent<Skills.SkillManager>();
                if (skillManager != null)
                    result.AddRange(CombineSystem.GetEvolutionCards(skillManager));
            }

            // 남은 슬롯(최대 3장) 일반 업그레이드로 채우기
            int remaining = Mathf.Max(0, 3 - result.Count);
            if (remaining > 0)
            {
                var upgradeIndices = GenerateRandomUpgradeIndices(upgradeCatalog, remaining);
                foreach (int idx in upgradeIndices)
                    result.Add(new ChestChoiceData(ChestChoiceType.UpgradeOption, idx));
            }

            return result.ToArray();
        }

        private List<int> GenerateRandomUpgradeIndices(UpgradeCatalog catalog, int count)
        {
            var pool = new List<int>(catalog.options.Length);
            for (int i = 0; i < catalog.options.Length; i++)
                if (catalog.options[i] != null) pool.Add(i);

            count = Mathf.Min(count, pool.Count);
            var result = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(pool.Count);
                result.Add(pool[pick]);
                pool.RemoveAt(pick);
            }
            return result;
        }

        [ClientRpc]
        private void ShowChestOptionsClientRpc(ChestChoiceData[] cards, ClientRpcParams rpcParams = default)
        {
            OnOptionsReceived?.Invoke(cards);
        }

        [ClientRpc]
        private void NotifyChestCompletedClientRpc()
        {
            OnChestRewardCompleted?.Invoke();
        }
    }
}
