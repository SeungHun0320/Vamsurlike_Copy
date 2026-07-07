using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vamsurlike.Core;
using Vamsurlike.Stage;
using Vamsurlike.UI.Events;

namespace Vamsurlike.Audio
{
    // Bootstrap DontDestroyOnLoad 오브젝트에 컴포넌트로 배치 (UIEventHub와 동일 패턴).
    // 씬 로드 전에 자동 생성되므로 어느 씬에서도 Instance가 보장된다.
    // 클라이언트 로컬 전용 — 서버 권한/네트워크 동기화와 무관하다.
    public sealed class AudioManager : MonoBehaviour
    {
        private const string MenuSceneName  = "MainMenu";
        private const string StageSceneName = "Stage_01";

        public static AudioManager Instance { get; private set; }

        [Header("BGM")]
        [SerializeField] private AudioClip menuBgm;
        [SerializeField] private AudioClip stageBgm;
        [SerializeField] private AudioClip bossBgm;
        [SerializeField] private AudioClip resultBgm;
        [SerializeField] private float bgmBaseVolume = 0.6f;
        [SerializeField] private float bgmCrossfadeDuration = 1.2f;

        [Header("SFX")]
        [SerializeField] private SFXSpawnEventSO sfxSpawnEvent;
        [SerializeField] private SFXCatalogSO sfxCatalog;
        [SerializeField] private float sfxBaseVolume = 0.8f;
        [SerializeField, Min(1)] private int sfxVoicePoolSize = 12;
        [SerializeField] private float sfxMaxDistance = 40f;

        private AudioSource bgmSourceA;
        private AudioSource bgmSourceB;
        private bool usingSourceA = true;
        private AudioClip currentBgmClip;
        private Coroutine crossfadeRoutine;

        private AudioSource[] sfxVoices;
        private int nextVoiceIndex;

        private float masterVolume = 1f;
        private float bgmVolumeScale = 1f;
        private float sfxVolumeScale = 1f;

        private bool isBossPhase;

        // Bootstrap 씬에 컴포넌트가 없는 경우 자동 생성 (안전망)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[AudioManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmSourceA = CreateBgmSource("BGM_A");
            bgmSourceB = CreateBgmSource("BGM_B");

            sfxVoices = new AudioSource[sfxVoicePoolSize];
            for (int i = 0; i < sfxVoicePoolSize; i++)
                sfxVoices[i] = CreateSfxVoice($"SFXVoice_{i}");
        }

        private void Start()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.OnVolumeChanged += HandleVolumeChanged;
                var s = SettingsManager.Instance.Current;
                HandleVolumeChanged(s.masterVolume, s.bgmVolume, s.sfxVolume);
            }

            if (UIEventHub.Instance != null)
            {
                UIEventHub.Instance.Stage.BossStatusChanged += HandleBossStatusChanged;
                UIEventHub.Instance.Flow.GameFlowChanged     += HandleGameFlowChanged;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnEnable()
        {
            if (sfxSpawnEvent != null)
                sfxSpawnEvent.Raised += HandlePlaySfx;
        }

        private void OnDisable()
        {
            if (sfxSpawnEvent != null)
                sfxSpawnEvent.Raised -= HandlePlaySfx;
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
                SettingsManager.Instance.OnVolumeChanged -= HandleVolumeChanged;
            if (UIEventHub.Instance != null)
            {
                UIEventHub.Instance.Stage.BossStatusChanged -= HandleBossStatusChanged;
                UIEventHub.Instance.Flow.GameFlowChanged     -= HandleGameFlowChanged;
            }
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (Instance == this) Instance = null;
        }

        // ─── 볼륨 ────────────────────────────────────────────────────
        private void HandleVolumeChanged(float master, float bgm, float sfx)
        {
            masterVolume   = Mathf.Clamp01(master);
            bgmVolumeScale = Mathf.Clamp01(bgm);
            sfxVolumeScale = Mathf.Clamp01(sfx);

            float bgmVolume = masterVolume * bgmVolumeScale * bgmBaseVolume;
            if (bgmSourceA != null) bgmSourceA.volume = bgmSourceA.clip == currentBgmClip ? bgmVolume : bgmSourceA.volume;
            ApplyCurrentBgmVolume();
        }

        private void ApplyCurrentBgmVolume()
        {
            // 크로스페이드 중이 아닐 때만 즉시 반영 — 페이드 코루틴이 도는 동안은 그쪽이 volume을 소유한다.
            if (crossfadeRoutine != null) return;
            AudioSource active = usingSourceA ? bgmSourceA : bgmSourceB;
            if (active != null) active.volume = masterVolume * bgmVolumeScale * bgmBaseVolume;
        }

        // ─── BGM ─────────────────────────────────────────────────────
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            isBossPhase = false;

            if (scene.name == MenuSceneName)
                PlayBgm(menuBgm);
            else if (scene.name == StageSceneName)
                PlayBgm(stageBgm);
        }

        private void HandleBossStatusChanged(BossStatusPayload payload)
        {
            if (SceneManager.GetActiveScene().name != StageSceneName) return;
            if (payload.IsVisible == isBossPhase) return;

            isBossPhase = payload.IsVisible;
            PlayBgm(isBossPhase ? bossBgm : stageBgm);
        }

        private void HandleGameFlowChanged(GameFlowPayload payload)
        {
            if (payload.Next is GameFlowState.Clear or GameFlowState.GameOver)
                PlayBgm(resultBgm);
        }

        public void PlayBgm(AudioClip clip)
        {
            if (clip == null || clip == currentBgmClip) return;
            currentBgmClip = clip;

            if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = StartCoroutine(CrossfadeTo(clip));
        }

        private IEnumerator CrossfadeTo(AudioClip clip)
        {
            AudioSource from = usingSourceA ? bgmSourceA : bgmSourceB;
            AudioSource to   = usingSourceA ? bgmSourceB : bgmSourceA;
            usingSourceA = !usingSourceA;

            to.clip = clip;
            to.volume = 0f;
            to.Play();

            float targetVolume = masterVolume * bgmVolumeScale * bgmBaseVolume;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, bgmCrossfadeDuration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                to.volume   = targetVolume * t;
                if (from.isPlaying) from.volume = targetVolume * (1f - t);
                yield return null;
            }

            to.volume = targetVolume;
            if (from.isPlaying) from.Stop();
            crossfadeRoutine = null;
        }

        // ─── SFX ─────────────────────────────────────────────────────
        private void HandlePlaySfx(SFXCue cue)
        {
            if (sfxCatalog == null) return;
            if (!sfxCatalog.TryGetEntry(cue.cueId, out SFXCatalogSO.Entry entry)) return;

            AudioSource voice = sfxVoices[nextVoiceIndex];
            nextVoiceIndex = (nextVoiceIndex + 1) % sfxVoices.Length;

            voice.transform.position = cue.position;
            voice.pitch  = 1f + Random.Range(-entry.pitchVariance, entry.pitchVariance);
            voice.volume = masterVolume * sfxVolumeScale * sfxBaseVolume * entry.volume * Mathf.Max(0f, cue.volumeScale);
            voice.PlayOneShot(entry.clip);
        }

        // ─── 내부 헬퍼 ────────────────────────────────────────────────
        private AudioSource CreateBgmSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            return source;
        }

        private AudioSource CreateSfxVoice(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 1f; // 3D
            source.maxDistance = sfxMaxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
    }
}
