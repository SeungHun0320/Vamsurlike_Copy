using UnityEngine;

namespace Vamsurlike.Data
{
    public enum SkillCastType
    {
        Projectile,
        AreaAura,
        Orbital,
        Ultimate,
        Grenade,        // 포물선 투척 + 착지 스플래시
        ScatterShot,    // 부채꼴 랜덤 발사, duration + cooldown
        Melee,          // 전방 근접 스플래시
        ClusterGrenade, // 포물선 투척 + 착지 시 서브 그레네이드 분열
        OrbitalGrenade, // Orbital + ProjectileCount: 위성이 플레이어 주위를 공전하며 스플래시+넉백
        BlackHole,      // DamageAura + Orbital: 범위 끌어당김 + 위성 집중 타격
        PierceShotgun,  // ScatterShot + PierceProjectile: 원뿔 즉발 전체 타격
        Earthshatter,   // Melee + AttackPower: 광역 지진파 + 기절
        GaleSpread,     // SpreadProjectile + SkillHaste: 스킬가속 연동 탄속 극대화
        Shotgun,        // SpreadProjectile 원뿔 즉발 판정 (최근접 1명)
        PiercingBoomerang, // PierceProjectile + MoveSpeed: 전진 관통 스택 누적 후 귀환 시 증폭 데미지
        LifeDrainBolt,  // BasicProjectile + HealthRegen: 투사체 + 주위를 도는 위성(구 OrbitalGrenade 방식) + 전체 흡혈
    }

    [System.Serializable]
    public class SkillLevelData
    {
        [Header("Common")]
        [Min(0.01f)] public float cooldown = 1f;
        [Min(0f)]    public float damage   = 10f;
        [Min(0.1f)]  public float range    = 10f;

        [Header("Projectile")]
        [Min(0.1f)] public float projectileSpeed    = 12f;
        [Min(0.1f)] public float projectileLifetime = 2f;
        [Min(0.05f)] public float projectileHitRadius = 0.5f;
        [Min(1)]    public int   projectileCount    = 1;
        [Min(0f)]   public float spreadAngle        = 0f;
        [Min(0)]    public int   pierceCount        = 0;

        [Header("Persistent (Aura / Orbital)")]
        [Min(0f)]    public float duration     = 0f;  // 0 = 항상 활성
        [Min(0.01f)] public float tickInterval = 1f;

        [Header("Area")]
        [Min(0f)] public float areaRadius = 0f;

        [Header("Orbital")]
        [Min(1)]   public int   orbitalCount         = 1;
        [Min(0.1f)] public float orbitalRadius       = 2f;
        [Min(0f)]   public float orbitalRotationSpeed = 180f;
        [Min(0.05f)] public float orbitalHitRadius   = 0.65f;
        [Min(0f)] public float orbitalDamageMultiplier = 1f;
        // OrbitalGrenade 전용 — 0이면 기존 Orbital처럼 직접 타격만 적용된다.
        [Min(0f)] public float orbitalSplashRadius     = 0f;
        [Min(0f)] public float orbitalKnockbackForce   = 0f;
        [Min(0.01f)] public float orbitalHitCooldown = 0.2f;
        [Min(0.01f)] public float orbitalProjectileScale = 0.75f;
        [Min(0.1f)] public float detachedOrbitalLifetimeMultiplier = 0.8f;
        [Min(0f)] public float detachedOrbitalHomingDelay = 0.2f;
        [Min(0.1f)] public float detachedOrbitalHomingRange = 18f;
        [Min(1f)] public float detachedOrbitalHomingTurnSpeed = 540f;

        [Header("Ultimate")]
        [Min(1)] public int   waveCount      = 1;
        [Min(0f)] public float waveDelay     = 0.15f;
        [Min(0f)] public float rotationPerWave = 30f;

        [Header("Grenade")]
        [Min(1)]    public int   grenadeCount     = 1;    // 한 번 시전 시 던지는 메인 수류탄 개수(투사체 개수 증가 패시브가 여기에 가산됨)
        [Min(0.5f)] public float grenadeRange     = 6f;   // 착탄 가능 반경
        [Min(0.1f)] public float grenadeArcHeight = 3f;   // 포물선 최고 높이
        [Min(0.1f)] public float splashRadius     = 2.5f; // 착지 스플래시 반경

        [Header("ScatterShot")]
        [Min(1)]   public int   scatterBulletCount = 8;    // 한 번에 발사 수
        [Min(5f)]  public float scatterAngle       = 60f;  // 부채꼴 각도
        [Min(0.1f)] public float burstDuration     = 2f;   // 지속 발사 시간

        [Header("Melee")]
        [Min(0.5f)] public float meleeRange    = 2.5f; // 전방 판정 거리 (사각형 세로 길이)
        [Min(0.5f)] public float meleeWidth    = 3f;   // 좌우 판정 폭 (사각형 가로 길이) — 샷건(원뿔)과 판정 형태를 구분하기 위해 도입

        [Header("ClusterGrenade")]
        [Min(1)]    public int   clusterCount        = 4;    // 착지 후 분열 서브 그레네이드 수 (투사체 개수 증가 패시브 영향 없음 — grenadeCount만 영향받음)
        [Min(0.5f)] public float clusterSpread       = 3f;   // 서브 그레네이드 착탄 반경
        [Min(0.1f)] public float clusterSplashRadius = 1.5f; // 서브 그레네이드 개별 스플래시 반경
        [Range(0f, 1f)] public float clusterDamageRatio = 0.5f; // 서브 그레네이드 데미지 = 메인 * 비율

        [Header("BlackHole")]
        [Min(0.1f)] public float pullSpeed = 3f; // 틱당 적 끌어당김 속도 (m/s)

        [Header("Earthshatter")]
        [Min(0f)] public float stunDuration = 1.5f; // 강타 적중 시 기절 시간
        [Min(0)]   public int   aftershockCount        = 3;   // 메인 히트 후 스윙 범위 내 무작위 여진 지점 수
        [Min(0.1f)] public float aftershockRadius       = 2f;  // 여진 1개당 스플래시 반경
        [Range(0f, 2f)] public float aftershockDamageRatio = 0.5f; // 여진 데미지 = 메인 데미지 * 이 비율
        [Min(0f)]  public float aftershockDelay        = 0.4f; // 메인 히트 후 여진이 터지기까지 대기 시간(초)

        [Header("Lifesteal")]
        [Range(0f, 1f)] public float lifestealRatio = 0f; // 0 = 흡혈 없음. 적중 데미지 * 비율만큼 시전자 회복

        [Header("PiercingBoomerang")]
        [Min(0f)] public float boomerangDamageAmplifyPerStack = 0.15f; // 귀환 데미지 = 기본데미지 * (1 + 스택수 * 이 값)

        [Header("Shotgun")]
        [Min(0.1f)] public float shotgunSoloDamageMultiplier   = 1.5f; // 원뿔 안에 적이 1명뿐일 때 집중 데미지 배율
        [Min(0.1f)] public float shotgunSharedDamageMultiplier = 0.6f; // 2명 이상 동시에 맞을 때 분산 데미지 배율
    }

    [CreateAssetMenu(fileName = "SkillData", menuName = "Vamsurlike/Data/Skill")]
    public class SkillDataSO : ScriptableObject
    {
        public string       skillName = "Skill";
        public Sprite       icon;
        public SkillCastType castType = SkillCastType.Projectile;
        public bool         isManual;

        [Header("Projectile / Grenade / ScatterShot")]
        public GameObject projectilePrefab;

        [Header("Client Visual Model")]
        [Tooltip("클라이언트에서 재생할 비주얼 모델 프리팹 (Melee·Grenade 등 비-Projectile 스킬용)")]
        public GameObject vfxPrefab;

        [Header("Levels")]
        [Min(1)] public int maxLevel = 1;
        public SkillLevelData[] levels = { new() };

        public SkillLevelData GetLevelData(int level)
        {
            if (levels == null || levels.Length == 0) return null;
            return levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];
        }

#if UNITY_EDITOR
        // 커스텀 인스펙터(SkillDataSOEditor)를 거치지 않고 levels 배열을 직접 편집했을 때
        // maxLevel과 어긋난 상태로 저장되는 것을 잡기 위한 최후 방어선.
        private void OnValidate()
        {
            if (levels != null && levels.Length != maxLevel)
                Debug.LogWarning($"[{nameof(SkillDataSO)}] \"{name}\": maxLevel({maxLevel})과 levels 배열 길이({levels.Length})가 일치하지 않습니다.", this);
        }
#endif
    }
}
