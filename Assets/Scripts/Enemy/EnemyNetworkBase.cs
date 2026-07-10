using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Vamsurlike.Audio;
using Vamsurlike.Data;
using Vamsurlike.Network;
using Vamsurlike.Player;
using Vamsurlike.Skills;
using Vamsurlike.Stage;
using Vamsurlike.UI;
using Vamsurlike.Upgrades;
using Vamsurlike.VFX;

namespace Vamsurlike.Enemy
{
    public class EnemyNetworkBase : NetworkBehaviour
    {
        private static readonly Color BossTelegraphColor = new(1f, 0.1f, 0.05f, 0.9f);
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
        private static readonly int SaturationProperty = Shader.PropertyToID("_Saturation");

        // 치명타 데미지 배율 — 기획 확정 전 기본값 (Phase 7.5)
        private const float CritDamageMultiplier = 1.5f;
        private const float HitFeedbackMinInterval = 0.08f;

        // 랜덤은 시드 기반 System.Random 인스턴스 사용
        [SerializeField] private int critRandomSeed = 9137;
        private System.Random critRng;

        [SerializeField] private EnemyDataSO data;
        [SerializeField] private Color hitFlashColor = new(0.48f, 0.48f, 0.48f, 1f);
        [SerializeField] private float hitFlashDuration = 0.15f;
        [SerializeField] private GameObject hitSparkPrefab;
        [SerializeField] private float hitSparkLifetime = 0.25f;
        [SerializeField] private GameObject bossTelegraphPrefab;

        [Header("Camera Shake")]
        [SerializeField] private CameraShakeEventSO cameraShakeEvent;
        [SerializeField] private float critShakeIntensity = 0.15f;
        [SerializeField] private float critShakeDuration = 0.12f;
        [SerializeField] private float deathShakeIntensity = 0.2f;
        [SerializeField] private float deathShakeDuration = 0.15f;
        [SerializeField] private float bossDeathShakeMultiplier = 2.5f;
        [SerializeField] private float shakeRadius = 12f;

        [Header("Death VFX")]
        [SerializeField] private VFXSpawnEventSO vfxSpawnEvent;
        [SerializeField] private float deathVFXDuration = 0.3f;
        [SerializeField] private float bossDeathVFXDuration = 0.6f;
        [SerializeField] private float bossImpactVFXDuration = 0.4f;

        [Header("SFX")]
        [SerializeField] private SFXSpawnEventSO sfxSpawnEvent;

        public readonly NetworkVariable<float> HP = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> MaxHPValue = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 같은 프리팹을 재사용하는 색상 변형(팔레트 스왑) 지원 — EnemyDataSO.tintColor를 모든 클라에 동기화.
        public readonly NetworkVariable<Color> TintColor = new(
            Color.white,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // _Color는 곱하기 틴트라 원본 텍스처가 다색이면 무채색을 만들 수 없다 — 셰이더의 _Saturation으로
        // 별도 채도 조절(0=회색조, 1=원본)을 동기화한다.
        public readonly NetworkVariable<float> Saturation = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsAlive => HP.Value > 0f;
        public EnemyDataSO Data => data;
        public float MaxHP => MaxHPValue.Value;
        public float ScaledAttackPower { get; private set; }

        private const ulong NoAttacker = ulong.MaxValue;
        private ulong lastAttackerClientId = NoAttacker;
        private Coroutine hitFlashCoroutine;
        private Renderer[] hitFlashRenderers;
        private MaterialPropertyBlock[] hitFlashOriginalBlocks;
        private Renderer[] cachedHitFlashRenderers;
        private float nextHitFeedbackTime;

        public override void OnNetworkSpawn()
        {
            TintColor.OnValueChanged += OnTintColorChanged;
            Saturation.OnValueChanged += OnSaturationChanged;
            ApplyTint(TintColor.Value, Saturation.Value);

            nextHitFeedbackTime = 0f;

            if (!IsServer) return;
            MaxHPValue.Value = data != null ? data.hp : 100f;
            HP.Value = MaxHPValue.Value;
            // 풀에서 재사용되는 인스턴스이므로 스폰마다 재시딩 — NetworkObjectId로 인스턴스별 시퀀스 분리
            critRng = new System.Random(unchecked(critRandomSeed + (int)NetworkObjectId));
            EnemyRegistry.Register(this);
        }

        private void OnTintColorChanged(Color _, Color next) => ApplyTint(next, Saturation.Value);
        private void OnSaturationChanged(float _, float next) => ApplyTint(TintColor.Value, next);

        // 풀에서 재사용되는 프리팹에 팔레트 스왑을 적용 — MaterialPropertyBlock을 사용해 GPU 인스턴싱/배칭이
        // 깨지지 않도록 한다(renderer.material 직접 접근은 인스턴스마다 머티리얼을 복제해 배칭을 깬다).
        // _Color는 곱하기라 원본이 다색이면 무채색을 만들 수 없어, TintableDesaturate 셰이더의 _Saturation을
        // 함께 적용해 실제 회색조 변형도 지원한다.
        private void ApplyTint(Color color, float saturation)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorProperty, color);
                block.SetColor(ColorProperty, color);
                block.SetFloat(SaturationProperty, saturation);
                r.SetPropertyBlock(block);
            }
        }

        // EnemySpawnManager.SpawnEnemy에서 Spawn() 직후 호출
        public void Initialize(EnemyDataSO enemyData, float hpMultiplier = 1f, float damageMultiplier = 1f)
        {
            if (!IsServer) return;
            data = enemyData;
            MaxHPValue.Value = Mathf.Max(1f, data.hp * Mathf.Max(1f, hpMultiplier));
            HP.Value = MaxHPValue.Value;
            TintColor.Value = data.tintColor;
            Saturation.Value = data.saturation;
            // NetworkTransform이 SyncScale=true라 서버에서 설정하면 모든 클라이언트에 그대로 복제된다.
            transform.localScale = Vector3.one * Mathf.Max(0.05f, data.visualScale);
            ScaledAttackPower = data.attackPower * Mathf.Max(1f, damageMultiplier);
            // OnNetworkSpawn보다 뒤에 호출되므로 EnemyAI에 데이터를 직접 주입
            if (TryGetComponent<EnemyAI>(out var ai))
            {
                ai.ApplyData(data);

                if (data.isBoss)
                {
                    var patterns = GetComponent<BossPatternController>();
                    if (patterns == null) patterns = gameObject.AddComponent<BossPatternController>();
                    patterns.Configure(this, ai);
                }
                else if (TryGetComponent<BossPatternController>(out var patterns))
                {
                    patterns.StopPatterns();
                    patterns.enabled = false;
                }
            }
        }

        // attackerClientId: 플레이어 귀속 시 OwnerClientId, 환경/디버그 비귀속 시 ulong.MaxValue
        // skillTag: 스킬/아이템 식별자 (SkillDataSO.name). 비귀속 시 null
        public void TakeDamage(float amount, ulong attackerClientId = NoAttacker, string skillTag = null)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[{nameof(EnemyNetworkBase)}] TakeDamage ignored on client. enemy={name}, amount={amount}");
                return;
            }

            if (!IsAlive) return;

            if (amount <= 0f)
            {
                Debug.LogWarning($"[{nameof(EnemyNetworkBase)}] TakeDamage ignored because amount is invalid. enemy={name}, amount={amount}, hp={HP.Value}");
                return;
            }

            // GAME_PLAN §8 공식: FinalDamage = amount * PlayerDamageMultiplier * (1 - EnemyDefenseRate)
            // PlayerDamageMultiplier는 SkillCastContext.FinalDamage에서 이미 적용된 상태로 amount에 실려 들어온다.
            float defense = Mathf.Max(0f, data != null ? data.defense : 0f);
            float defenseRate = defense / (defense + 100f);
            float finalDamage = Mathf.Max(1f, amount * (1f - defenseRate));

            // 치명타 판정 — 플레이어 귀속 데미지에만 적용 (환경/디버그/아이템 비귀속 데미지는 크리티컬 없음)
            bool isCrit = false;
            if (attackerClientId != NoAttacker)
            {
                float critChance = GetPassiveStatHandler(attackerClientId)?.CritChance.Value ?? 0f;
                if (critChance > 0f && critRng.NextDouble() < critChance)
                {
                    isCrit = true;
                    finalDamage *= CritDamageMultiplier;
                }
            }

            HP.Value = Mathf.Max(0f, HP.Value - finalDamage);

            if (attackerClientId != NoAttacker)
            {
                lastAttackerClientId = attackerClientId;
                GetPlayerMatchStats(attackerClientId)?.AddDamage(finalDamage, skillTag);
            }
            bool died = HP.Value <= 0f;
            float offset = data != null ? data.floatingTextHeightOffset : 2f;
            if (ShouldSendHitFeedback(isCrit, died))
            {
                Vector3 basePosition = transform.position;
                PlayDamageFeedbackClientRpc(
                    finalDamage,
                    isCrit,
                    basePosition + Vector3.up * offset,
                    basePosition + Vector3.up * (offset * 0.5f),
                    isCrit);
            }

            if (died)
                HandleDeath();
        }

        private bool ShouldSendHitFeedback(bool isCrit, bool died)
        {
            if (isCrit || died)
            {
                nextHitFeedbackTime = Time.time + HitFeedbackMinInterval;
                return true;
            }

            if (Time.time < nextHitFeedbackTime) return false;

            nextHitFeedbackTime = Time.time + HitFeedbackMinInterval;
            return true;
        }

        private PlayerMatchStats GetPlayerMatchStats(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            return client.PlayerObject?.GetComponent<PlayerMatchStats>();
        }

        private PassiveStatHandler GetPassiveStatHandler(ulong clientId)
        {
            if (NetworkManager.Singleton == null) return null;
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client)) return null;
            if (client.PlayerObject == null) return null;
            return client.PlayerObject.GetComponent<PassiveStatHandler>();
        }

        // 대지분쇄자(Earthshatter) 등 CC 스킬 전용 — 서버 전용.
        public void ApplyStun(float duration)
        {
            if (!IsServer) return;
            if (TryGetComponent<EnemyAI>(out var ai)) ai.ApplyStun(duration);
        }

        // 궤도 수류탄(OrbitalGrenade) 등 넉백 스킬 전용 — sourcePosition 반대 방향으로 밀어낸다. 서버 전용.
        public void ApplyKnockback(Vector3 sourcePosition, float force)
        {
            if (!IsServer) return;
            if (!TryGetComponent<EnemyAI>(out var ai)) return;

            Vector3 direction = transform.position - sourcePosition;
            ai.ApplyKnockback(direction, force);
        }

        // 서버가 지정한 보스 광역기 범위를 Damage Aura와 같은 원형 VFX로 전 클라이언트에 표시.
        public void ShowSlamTelegraph(Vector3 center, float radius, float duration)
        {
            if (!IsServer) return;
            ShowSlamTelegraphClientRpc(center, radius, duration);
        }

        // BossPatternController가 Slam 착지/Charge 충돌 등 근접 패턴의 착탄 순간에 호출.
        public void PlayImpactVFX(Vector3 position)
        {
            if (!IsServer) return;
            PlayCameraShakeClientRpc(deathShakeIntensity * bossDeathShakeMultiplier, deathShakeDuration);
            PlayBossImpactVFXClientRpc(position);
        }

        [ClientRpc]
        private void PlayBossImpactVFXClientRpc(Vector3 position)
        {
            vfxSpawnEvent?.Raise(new VFXCue(VFXCueIds.BossImpact, position, Vector3.up, 1f, bossImpactVFXDuration, Color.white));
            sfxSpawnEvent?.Raise(new SFXCue(SFXCueIds.BossImpact, position));
        }

        // 모르타르 패턴: 여러 위치에 경고 원을 동시에 표시.
        public void ShowMortarTelegraph(Vector3[] positions, float radius, float duration)
        {
            if (!IsServer) return;
            ShowMortarTelegraphClientRpc(positions, radius, duration);
        }

        protected virtual void HandleDeath()
        {
            bool wasBoss = data != null && data.isBoss;

            if (lastAttackerClientId != NoAttacker)
            {
                PlayerMatchStats matchStats = GetPlayerMatchStats(lastAttackerClientId);
                if (matchStats != null) matchStats.AddKill();
            }

            TriggerDeathAnimClientRpc();
            PlayDeathVFXClientRpc(wasBoss);
            float deathIntensity = wasBoss ? deathShakeIntensity * bossDeathShakeMultiplier : deathShakeIntensity;
            PlayCameraShakeClientRpc(deathIntensity, deathShakeDuration);
            if (StageRuntime.Instance != null && StageRuntime.Instance.Drops != null)
                StageRuntime.Instance.Drops.OnEnemyDied(data, transform.position);

            if (TryGetComponent<BossPatternController>(out var patterns))
            {
                patterns.StopPatterns();
                patterns.enabled = false;
            }

            if (wasBoss && GameFlowCoordinator.Instance != null && GameFlowCoordinator.Instance.IsBossPhase)
                GameFlowCoordinator.Instance.ForceTransition(GameFlowState.Clear);

            NetworkObject.Despawn(false);
        }

        public override void OnNetworkDespawn()
        {
            TintColor.OnValueChanged -= OnTintColorChanged;
            Saturation.OnValueChanged -= OnSaturationChanged;

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }
            RestoreHitFlashBlocks();

            base.OnNetworkDespawn();
            if (!IsServer) return;
            EnemyRegistry.Unregister(this);
            if (data != null && data.prefab != null && PoolManager.Instance != null)
                PoolManager.Instance.ReturnNetworkObject(data.prefab, NetworkObject);
        }
        [ClientRpc]
        private void PlayDamageFeedbackClientRpc(
            float damage,
            bool isCrit,
            Vector3 damageWorldPosition,
            Vector3 sparkWorldPosition,
            bool playCritShake)
        {
            FloatingTextManager.Instance?.ShowDamage(damage, damageWorldPosition, isCrit);
            PlayHitSpark(sparkWorldPosition);
            PlayHitFlash(isCrit);

            if (playCritShake)
                cameraShakeEvent?.Raise(new CameraShakeCue(transform.position, critShakeIntensity, critShakeDuration, shakeRadius));
        }

        private void PlayHitSpark(Vector3 worldPosition)
        {
            sfxSpawnEvent?.Raise(new SFXCue(SFXCueIds.Hit, worldPosition));

            if (hitSparkPrefab == null) return;

            GameObject spark = PoolManager.GetOrInstantiateGO(
                hitSparkPrefab, worldPosition, Quaternion.identity, nameof(EnemyNetworkBase));

            if (spark == null) return;
            StartCoroutine(ReturnHitSparkAfterDelay(spark));
        }

        private IEnumerator ReturnHitSparkAfterDelay(GameObject spark)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, hitSparkLifetime));

            if (spark == null) yield break;
            PoolManager.ReturnOrDestroyGO(hitSparkPrefab, spark, nameof(EnemyNetworkBase));
        }
        private void PlayHitFlash(bool isCrit)
        {
            if (!gameObject.activeInHierarchy) return;

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                RestoreHitFlashBlocks();
            }

            hitFlashCoroutine = StartCoroutine(HitFlashCoroutine(isCrit));
        }

        private IEnumerator HitFlashCoroutine(bool isCrit)
        {
            hitFlashRenderers = GetHitFlashRenderers();
            if (hitFlashRenderers == null || hitFlashRenderers.Length == 0)
            {
                hitFlashCoroutine = null;
                yield break;
            }

            hitFlashOriginalBlocks = new MaterialPropertyBlock[hitFlashRenderers.Length];
            for (int i = 0; i < hitFlashRenderers.Length; i++)
            {
                Renderer r = hitFlashRenderers[i];
                if (r == null) continue;

                hitFlashOriginalBlocks[i] = new MaterialPropertyBlock();
                r.GetPropertyBlock(hitFlashOriginalBlocks[i]);

                Color baseColor = ResolveRendererColor(r, hitFlashOriginalBlocks[i]);
                Color flashColor = ApplyHitFlashTint(baseColor, isCrit);

                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorProperty, flashColor);
                block.SetColor(ColorProperty, flashColor);
                block.SetColor(EmissionColorProperty, flashColor);
                r.SetPropertyBlock(block);
            }

            yield return new WaitForSeconds(Mathf.Max(0.01f, hitFlashDuration));

            RestoreHitFlashBlocks();
            hitFlashCoroutine = null;
        }

        private Renderer[] GetHitFlashRenderers()
        {
            if (cachedHitFlashRenderers == null || cachedHitFlashRenderers.Length == 0)
                cachedHitFlashRenderers = GetComponentsInChildren<Renderer>(true);
            return cachedHitFlashRenderers;
        }

        private Color ApplyHitFlashTint(Color baseColor, bool isCrit)
        {
            Color flashColor = new(
                Mathf.Clamp01(baseColor.r * hitFlashColor.r),
                Mathf.Clamp01(baseColor.g * hitFlashColor.g),
                Mathf.Clamp01(baseColor.b * hitFlashColor.b),
                baseColor.a);

            return isCrit ? Color.Lerp(flashColor, Color.yellow, 0.25f) : flashColor;
        }

        private static Color ResolveRendererColor(Renderer renderer, MaterialPropertyBlock block)
        {
            Color color = Color.white;
            Material material = renderer != null ? renderer.sharedMaterial : null;

            if (material != null)
            {
                if (material.HasProperty(BaseColorProperty))
                    color = material.GetColor(BaseColorProperty);
                else if (material.HasProperty(ColorProperty))
                    color = material.GetColor(ColorProperty);
            }

            if (block == null) return color;

            Color blockColor = block.GetColor(BaseColorProperty);
            if (blockColor != default) return blockColor;

            blockColor = block.GetColor(ColorProperty);
            return blockColor != default ? blockColor : color;
        }

        private void RestoreHitFlashBlocks()
        {
            if (hitFlashRenderers == null || hitFlashOriginalBlocks == null) return;

            int count = Mathf.Min(hitFlashRenderers.Length, hitFlashOriginalBlocks.Length);
            for (int i = 0; i < count; i++)
            {
                if (hitFlashRenderers[i] != null && hitFlashOriginalBlocks[i] != null)
                    hitFlashRenderers[i].SetPropertyBlock(hitFlashOriginalBlocks[i]);
            }

            hitFlashRenderers = null;
            hitFlashOriginalBlocks = null;
        }
        [ClientRpc]
        private void PlayCameraShakeClientRpc(float intensity, float duration)
        {
            cameraShakeEvent?.Raise(new CameraShakeCue(transform.position, intensity, duration, shakeRadius));
        }

        [ClientRpc]
        private void ShowSlamTelegraphClientRpc(Vector3 center, float radius, float duration)
        {
            SpawnTelegraphCircle(center, radius, duration);
            sfxSpawnEvent?.Raise(new SFXCue(SFXCueIds.BossTelegraph, center));
        }

        [ClientRpc]
        private void ShowMortarTelegraphClientRpc(Vector3[] positions, float radius, float duration)
        {
            foreach (var pos in positions)
            {
                SpawnTelegraphCircle(pos, radius, duration);
                sfxSpawnEvent?.Raise(new SFXCue(SFXCueIds.BossTelegraph, pos));
            }
        }

        // Slam/Mortar 경고 원 — PoolManager를 거쳐 AreaCircleVFX를 재사용한다.
        // bossTelegraphPrefab 미배선 시 이전과 동일하게 raw Instantiate로 폴백.
        private void SpawnTelegraphCircle(Vector3 center, float radius, float duration)
        {
            Vector3 position = center + Vector3.up * 0.05f;

            if (bossTelegraphPrefab != null)
            {
                GameObject visual = PoolManager.GetOrInstantiateGO(
                    bossTelegraphPrefab, position, Quaternion.identity, nameof(EnemyNetworkBase));
                if (visual == null) return;

                if (visual.TryGetComponent(out AreaCircleVFX pooledCircle))
                {
                    pooledCircle.Initialize(radius, duration, BossTelegraphColor, null, bossTelegraphPrefab);
                    return;
                }

                Debug.LogWarning($"[{nameof(EnemyNetworkBase)}] bossTelegraphPrefab에 AreaCircleVFX가 없습니다.", this);
                PoolManager.ReturnOrDestroyGO(bossTelegraphPrefab, visual, nameof(EnemyNetworkBase));
                return;
            }

            var fallback = new GameObject("BossTelegraph");
            fallback.transform.position = position;
            AreaCircleVFX circle = fallback.AddComponent<AreaCircleVFX>();
            circle.Initialize(radius, duration, BossTelegraphColor);
        }

        [ClientRpc]
        private void TriggerDeathAnimClientRpc()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.SetTrigger(Animator.StringToHash("Die"));
        }

        // VFXSpawner(카탈로그 기반 풀링 재생 경로) 이벤트로 발행한다 — 히트 플래시/스파크와 달리
        // 신규 이펙트라 처음부터 GAME_PLAN §8.5 "이벤트 흐름"의 정식 경로(VFXSpawnEventSO → VFXSpawner)로 구현.
        [ClientRpc]
        private void PlayDeathVFXClientRpc(bool isBoss)
        {
            int cueId = isBoss ? VFXCueIds.BossDeath : VFXCueIds.EnemyDeath;
            float duration = isBoss ? bossDeathVFXDuration : deathVFXDuration;
            vfxSpawnEvent?.Raise(new VFXCue(cueId, transform.position, Vector3.up, 1f, duration, Color.white));
            sfxSpawnEvent?.Raise(new SFXCue(isBoss ? SFXCueIds.BossDeath : SFXCueIds.EnemyDeath, transform.position));
        }

    }
}
