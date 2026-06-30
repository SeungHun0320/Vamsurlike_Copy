using System.Collections.Generic;
using Unity.Netcode;

namespace Vamsurlike.Player
{
    // 한 스테이지 누적 전투 통계. 서버 전용 필드 — 종료 시 StageResultBroadcaster가 RPC로 전송.
    // PlayerNetworkStats(실시간 HP/상태)와 역할 분리.
    public class PlayerMatchStats : NetworkBehaviour
    {
        public int   KillCount    { get; private set; }
        public float TotalDamage  { get; private set; }
        public float SurvivalTime { get; private set; }
        public int   Level        { get; private set; }

        // 스킬별 데미지 누적 (서버 전용, 결과 전송 시 직렬화)
        public readonly Dictionary<string, float> DamagePerSkill = new();

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

        public void SetSurvivalTime(float time)   { if (IsServer) SurvivalTime = time; }
        public void SetLevel(int level)            { if (IsServer) Level        = level; }
    }
}
