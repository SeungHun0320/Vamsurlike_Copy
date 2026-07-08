using UnityEngine;
using Vamsurlike.Items;
using Vamsurlike.Stage;

namespace Vamsurlike.UI.Events
{
    public readonly struct LevelUpOptionsPayload
    {
        public readonly int[] OptionIndices;
        public readonly int[] CurrentLevels;

        public LevelUpOptionsPayload(int[] optionIndices, int[] currentLevels)
        {
            OptionIndices = optionIndices;
            CurrentLevels = currentLevels;
        }
    }

    public readonly struct ChestOptionsPayload
    {
        public readonly ChestChoiceData[] Choices;

        public ChestOptionsPayload(ChestChoiceData[] choices) => Choices = choices;
    }

    public readonly struct SkillSlotsPayload
    {
        public readonly string[] Names;
        public readonly int[]    Levels;
        public readonly int[]    MaxLevels;

        public SkillSlotsPayload(string[] names, int[] levels, int[] maxLevels = null)
        {
            Names     = names;
            Levels    = levels;
            MaxLevels = maxLevels;
        }
    }

    public readonly struct GameFlowPayload
    {
        public readonly GameFlowState Previous;
        public readonly GameFlowState Next;

        public GameFlowPayload(GameFlowState previous, GameFlowState next)
        {
            Previous = previous;
            Next     = next;
        }
    }

    public readonly struct ReviveProgressPayload
    {
        // 0~1 진행도, -1 = 취소
        public readonly float Progress;
        // 다운된 플레이어의 ClientId (WorldReviveProgressUI가 올바른 오브젝트에만 표시하기 위함)
        public readonly ulong DownedClientId;

        public ReviveProgressPayload(float progress, ulong downedClientId = ulong.MaxValue)
        {
            Progress      = progress;
            DownedClientId = downedClientId;
        }
    }

    public readonly struct PlayerStatusPayload
    {
        public readonly ulong  ClientId;
        public readonly string DisplayName;
        public readonly float  Hp;
        public readonly float  MaxHp;
        public readonly bool   IsDowned;          // 1단계: 동료 부활 가능
        public readonly bool   IsDeadWaiting;     // 2단계: 자동 부활 대기 중 (부활 불가)
        public readonly bool   IsAlive;
        public readonly float  DownedTimeRemaining;
        public readonly float  DeadWaitRemaining;

        public PlayerStatusPayload(
            ulong  clientId,
            string displayName,
            float  hp,
            float  maxHp,
            bool   isDowned,
            bool   isDeadWaiting,
            bool   isAlive,
            float  downedTimeRemaining,
            float  deadWaitRemaining)
        {
            ClientId            = clientId;
            DisplayName         = displayName;
            Hp                  = hp;
            MaxHp               = maxHp;
            IsDowned            = isDowned;
            IsDeadWaiting       = isDeadWaiting;
            IsAlive             = isAlive;
            DownedTimeRemaining = downedTimeRemaining;
            DeadWaitRemaining   = deadWaitRemaining;
        }
    }

    public readonly struct SharedLevelPayload
    {
        public readonly int   Level;
        public readonly float Xp;
        public readonly float XpRequired;
        public readonly float NormalizedXp;

        public SharedLevelPayload(int level, float xp, float xpRequired)
        {
            Level       = level;
            Xp          = xp;
            XpRequired  = xpRequired;
            NormalizedXp = xpRequired > 0f ? Mathf.Clamp01(xp / xpRequired) : 0f;
        }
    }

    public readonly struct UltimateCooldownPayload
    {
        public readonly float Remaining;
        public readonly float Duration;
        public readonly bool  IsReady;

        public UltimateCooldownPayload(float remaining, float duration)
        {
            Remaining = remaining;
            Duration  = duration;
            IsReady   = remaining <= 0f;
        }
    }

    public readonly struct BossStatusPayload
    {
        public readonly bool  IsVisible;
        public readonly float Hp;
        public readonly float MaxHp;
        public readonly float NormalizedHp;

        public BossStatusPayload(bool isVisible, float hp, float maxHp)
        {
            IsVisible    = isVisible;
            Hp           = hp;
            MaxHp        = maxHp;
            NormalizedHp = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
        }
    }

    public readonly struct StageTimerPayload
    {
        public readonly float ElapsedSeconds;
        public readonly float BossTimeSeconds;
        public readonly float RemainingToBossSeconds;
        public readonly bool  IsBossPhase;

        public StageTimerPayload(float elapsedSeconds, float bossTimeSeconds, bool isBossPhase)
        {
            ElapsedSeconds         = elapsedSeconds;
            BossTimeSeconds        = bossTimeSeconds;
            RemainingToBossSeconds = Mathf.Max(0f, bossTimeSeconds - elapsedSeconds);
            IsBossPhase            = isBossPhase;
        }
    }

    public readonly struct MatchResultPayload
    {
        public readonly MatchResultEntry[] Entries;
        public MatchResultPayload(MatchResultEntry[] entries) => Entries = entries;
    }

    public readonly struct AcquisitionLogPayload
    {
        public readonly string Message;
        public readonly Sprite Icon;
        public readonly Color  Color;
        public readonly float  Duration;

        public AcquisitionLogPayload(string message, Sprite icon, Color color, float duration)
        {
            Message  = message;
            Icon     = icon;
            Color    = color;
            Duration = duration;
        }
    }
}
