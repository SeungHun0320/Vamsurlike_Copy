using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Player;
using Vamsurlike.UI.Events;

namespace Vamsurlike.Stage
{
    // GameFlowCoordinator와 같은 GameObject에 배치.
    // Clear/GameOver 감지 시 전 플레이어 통계를 수집해 전 클라이언트로 전송.
    public class StageResultBroadcaster : NetworkBehaviour
    {
        private bool hasBroadcast;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.CurrentFlow.OnValueChanged += OnFlowChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && GameFlowCoordinator.Instance != null)
                GameFlowCoordinator.Instance.CurrentFlow.OnValueChanged -= OnFlowChanged;
        }

        private void OnFlowChanged(GameFlowState prev, GameFlowState next)
        {
            if (hasBroadcast) return;
            if (next != GameFlowState.Clear && next != GameFlowState.GameOver) return;

            hasBroadcast = true;
            SendMatchResultClientRpc(BuildEntries());
        }

        private MatchResultEntry[] BuildEntries()
        {
            var results = new List<MatchResultEntry>();
            int playerIndex = 0;
            float stageElapsed = StageRuntime.Instance != null ? StageRuntime.Instance.ElapsedTime.Value : 0f;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                if (!client.PlayerObject.TryGetComponent<PlayerMatchStats>(out var statsComp)) continue;

                // 한 번도 쓰러지지 않은 플레이어는 스테이지 전체 경과 시간을 생존 시간으로 기록
                PlayerNetworkStats networkStats = client.PlayerObject.GetComponent<PlayerNetworkStats>();
                if (networkStats != null && networkStats.CanAct)
                    statsComp.SetSurvivalTime(stageElapsed);
                else if (statsComp.SurvivalTime <= 0f)
                    statsComp.SetSurvivalTime(stageElapsed);

                var sortedSkills = statsComp.DamagePerSkill
                    .OrderByDescending(kv => kv.Value)
                    .Take(4)
                    .ToArray();

                results.Add(new MatchResultEntry
                {
                    ClientId        = client.ClientId,
                    DisplayName     = ResolveDisplayName(statsComp, ++playerIndex),
                    Level           = statsComp.Level,
                    KillCount       = statsComp.KillCount,
                    TotalDamage     = statsComp.TotalDamage,
                    SurvivalTime    = statsComp.SurvivalTime,
                    DownCount       = statsComp.DownCount,
                    DeathCount      = statsComp.DeathCount,
                    SkillEntryCount = sortedSkills.Length,
                    Skill0          = MakeSkillEntry(sortedSkills, 0),
                    Skill1          = MakeSkillEntry(sortedSkills, 1),
                    Skill2          = MakeSkillEntry(sortedSkills, 2),
                    Skill3          = MakeSkillEntry(sortedSkills, 3),
                });
            }

            return results.ToArray();
        }

        private static FixedString64Bytes ResolveDisplayName(PlayerMatchStats statsComp, int fallbackIndex)
        {
            string playerName = statsComp.PlayerName.Value.ToString();
            return new FixedString64Bytes(string.IsNullOrWhiteSpace(playerName)
                ? $"Player {fallbackIndex}"
                : playerName.Trim());
        }

        private static SkillDamageEntry MakeSkillEntry(KeyValuePair<string, float>[] sorted, int idx)
        {
            if (idx >= sorted.Length) return default;
            return new SkillDamageEntry
            {
                SkillTag = new FixedString64Bytes(sorted[idx].Key),
                Damage   = sorted[idx].Value,
            };
        }

        [ClientRpc]
        private void SendMatchResultClientRpc(MatchResultEntry[] entries)
        {
            UIEventHub.Instance?.Flow.PublishMatchResult(new MatchResultPayload(entries));
        }
    }
}
