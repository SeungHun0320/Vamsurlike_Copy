using Unity.Collections;
using Unity.Netcode;

namespace Vamsurlike.Stage
{
    // 스테이지 종료 시 서버 → 전 클라이언트 전송용 직렬화 구조체.
    // DamagePerSkill은 상위 4개만 직렬화 (Dictionary는 INetworkSerializable 불가).
    public struct MatchResultEntry : INetworkSerializable
    {
        public ulong ClientId;
        public FixedString64Bytes DisplayName;
        public int   Level;
        public int   KillCount;
        public float TotalDamage;
        public float SurvivalTime;
        // Phase 7.5 — 다운(1단계 진입) / 사망(2단계 진입) 횟수
        public int   DownCount;
        public int   DeathCount;

        public int SkillEntryCount;
        public SkillDamageEntry Skill0, Skill1, Skill2, Skill3;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId);
            s.SerializeValue(ref DisplayName);
            s.SerializeValue(ref Level);
            s.SerializeValue(ref KillCount);
            s.SerializeValue(ref TotalDamage);
            s.SerializeValue(ref SurvivalTime);
            s.SerializeValue(ref DownCount);
            s.SerializeValue(ref DeathCount);
            s.SerializeValue(ref SkillEntryCount);
            s.SerializeValue(ref Skill0);
            s.SerializeValue(ref Skill1);
            s.SerializeValue(ref Skill2);
            s.SerializeValue(ref Skill3);
        }
    }

    public struct SkillDamageEntry : INetworkSerializable
    {
        public FixedString64Bytes SkillTag;
        public float Damage;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref SkillTag);
            s.SerializeValue(ref Damage);
        }
    }
}
