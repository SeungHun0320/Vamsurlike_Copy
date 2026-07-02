using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace Vamsurlike.Player
{
    // 한 스테이지 누적 전투 통계. 서버 전용 필드 — 종료 시 StageResultBroadcaster가 RPC로 전송.
    // PlayerNetworkStats(실시간 HP/상태)와 역할 분리.
    public class PlayerMatchStats : NetworkBehaviour
    {
        // 서버가 쓰고 모든 클라이언트가 읽는 닉네임 (접속 시 ConnectionData에서 추출)
        public readonly NetworkVariable<FixedString64Bytes> PlayerName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int   KillCount    { get; private set; }
        public float TotalDamage  { get; private set; }
        public float SurvivalTime { get; private set; }
        public int   Level        { get; private set; }
        // Phase 7.5 — 다운(1단계 진입) / 사망(2단계 진입) 횟수
        public int   DownCount    { get; private set; }
        public int   DeathCount   { get; private set; }

        // 스킬별 데미지 누적 (서버 전용, 결과 전송 시 직렬화)
        public readonly Dictionary<string, float> DamagePerSkill = new();

        public void SetPlayerName(string name)
        {
            if (!IsServer) return;
            PlayerName.Value = new FixedString64Bytes(name);
        }

        public void AddDamage(float amount, string skillTag = null)
        {
            if (!IsServer) return;
            TotalDamage += amount;
            if (!string.IsNullOrEmpty(skillTag))
            {
                DamagePerSkill.TryGetValue(skillTag, out float prev);
                DamagePerSkill[skillTag] = prev + amount;
            }
        }

        public void AddKill()
        {
            if (!IsServer) return;
            KillCount++;
        }

        public void AddDown()
        {
            if (!IsServer) return;
            DownCount++;
        }

        public void AddDeath()
        {
            if (!IsServer) return;
            DeathCount++;
        }

        public void SetSurvivalTime(float time)   { if (IsServer) SurvivalTime = time; }
        public void SetLevel(int level)            { if (IsServer) Level        = level; }
    }
}
