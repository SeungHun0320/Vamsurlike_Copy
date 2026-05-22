# 코딩 규칙 (최우선 적용)

이 파일에 작성한 규칙을 모든 코드 작업에 최우선으로 반영합니다.

---

## 1. SOLID 원칙

| 원칙 | 적용 방식 |
|---|---|
| **S** 단일 책임 | 클래스 하나 = 역할 하나. `PlayerStats`는 스탯만, `PlayerController`는 이동만 |
| **O** 개방/폐쇄 | 새 스킬/아이템 추가 시 기존 코드 수정 금지. `SkillBase` 상속으로 확장 |
| **L** 리스코프 치환 | 파생 클래스는 베이스 계약을 깨지 않음. `BossBase`는 `EnemyBase`를 완전히 대체 가능 |
| **I** 인터페이스 분리 | `IDamageable`, `IPickupable`, `ISkillExecutable` 등 작게 분리. 거대 인터페이스 금지 |
| **D** 의존성 역전 | 구체 클래스가 아닌 인터페이스/추상 클래스에 의존. 직접 `new` 생성 최소화 |

---

## 2. 디자인 패턴 제안 의무

- 리팩터링 또는 패턴 적용이 가능한 구조를 발견하면 **구현 전 반드시 제안**한다.
- 제안 형식: 패턴명 + 적용 이유 + 적용 전/후 구조 비교
- 주요 적용 패턴 목록 (참고용, 상황에 따라 추가):

| 패턴 | 주요 적용 대상 |
|---|---|
| State | `GameManager`, `EnemyAI` 상태 머신 |
| Observer (EventSO) | 시스템 간 이벤트 통신 |
| Object Pool | 몬스터, 투사체, VFX, 데미지 텍스트 |
| Strategy | 스킬 발동 방식, 적 AI 행동 |
| Factory | `SkillFactory`, `EnemyFactory` |
| Command | 레벨업 선택 적용, Undo 가능 구조 |
| Decorator | 스탯 버프/디버프 스택 |
| Singleton | `GameManager`, `PoolManager` 등 전역 매니저 (남용 금지) |

---

## 3. 네이밍 컨벤션 (Unity 스타일)

### 기본 규칙

| 대상 | 스타일 | 예시 |
|---|---|---|
| 클래스 / 구조체 | `PascalCase` | `PlayerController`, `EnemyBase` |
| 메서드 | `PascalCase` | `TakeDamage()`, `SpawnEnemy()` |
| 프로퍼티 | `PascalCase` | `CurrentHP`, `IsAlive` |
| 인터페이스 | `I` + `PascalCase` | `IDamageable`, `IPickupable` |
| 추상 클래스 | `Base` 접미사 | `SkillBase`, `EnemyBase` |
| 이벤트 | `PascalCase` | `OnPlayerDied`, `OnLevelUp` |
| enum 타입 / 값 | `PascalCase` | `GameState.Playing` |
| private 필드 | `camelCase` | `currentHP`, `playerStats` |
| [SerializeField] 필드 | `camelCase` | `moveSpeed`, `attackPower` |
| 상수 (`const` / `static readonly`) | `PascalCase` | `MaxSkillSlot`, `RespawnTime` |
| 지역 변수 / 파라미터 | `camelCase` | `float amount`, `bool isGrounded` |
| 네임스페이스 | `PascalCase` | `Vamsurlike.Player` |

### 코드 예시

```csharp
public class PlayerController : MonoBehaviour
{
    // SerializeField: camelCase
    [SerializeField] private Camera mainCamera;

    // private 필드: camelCase
    private CharacterController cc;
    private PlayerStats          playerStats;
    private float                verticalVelocity = -2f;

    // const / static readonly: PascalCase
    private const float Gravity      = -20f;
    private static int  instanceCount;

    // 프로퍼티: PascalCase
    public float CurrentHP  { get; private set; }
    public bool  IsAlive    => playerStats != null && playerStats.IsAlive;

    // 메서드: PascalCase, 파라미터: camelCase
    public void TakeDamage(float amount) { }
    private void Move() { }
}
```

---

## 4. 예외 처리 (필수)

### 규칙

1. **모든 `GetComponent`, `FindObjectOfType`, `[SerializeField]` 참조는 null 체크 필수**
2. **`OnEnable` / `OnDisable` 이벤트 구독 시 null 확인 필수**
3. **외부 데이터(ScriptableObject) 사용 전 null 및 유효성 체크**
4. **예외 발생 가능한 로직은 `try-catch` 또는 조기 반환(early return)으로 처리**
5. **에러 로그는 `Debug.LogError`, 경고는 `Debug.LogWarning` 구분 사용**

### 코드 패턴 예시

```csharp
// ✅ null 체크 패턴
private void Start()
{
    playerStats = GetComponent<PlayerStats>();
    if (playerStats == null)
    {
        Debug.LogError($"[{nameof(PlayerController)}] PlayerStats 컴포넌트를 찾을 수 없습니다.", this);
        enabled = false;
        return;
    }
}

// ✅ SerializeField null 체크
private void Awake()
{
    if (characterData == null)
    {
        Debug.LogError($"[{nameof(PlayerStats)}] CharacterDataSO가 할당되지 않았습니다.", this);
        return;
    }
    currentHP = characterData.baseHP;
}

// ✅ 이벤트 구독 null 체크
private void OnEnable()
{
    if (onPlayerDied == null)
    {
        Debug.LogWarning($"[{nameof(HUDController)}] onPlayerDied 이벤트 채널이 없습니다.");
        return;
    }
    onPlayerDied.OnEventRaised += HandlePlayerDied;
}

private void OnDisable()
{
    if (onPlayerDied != null)
        onPlayerDied.OnEventRaised -= HandlePlayerDied;
}

// ✅ early return
public void TakeDamage(float amount)
{
    if (!isAlive) return;
    if (amount <= 0f)
    {
        Debug.LogWarning($"[{nameof(EnemyBase)}] 유효하지 않은 데미지 값: {amount}");
        return;
    }
    currentHP -= amount;
    if (currentHP <= 0f) Die();
}
```

---

## 5. 추가 규칙

- **주석**: 코드가 **왜** 이렇게 짜여졌는지 비자명한 경우에만 한국어 주석 1줄 허용. 무엇을 하는지 설명하는 주석 작성.
- **Magic Number 금지**: 수치는 반드시 `const`, `SO 필드`, 또는 명명된 변수로 추출.
- **멀티 대비**: 게임 로직(데미지, 스폰, 드롭)은 반드시 매니저 경유. 직접 처리 금지.
- **랜덤**: `Random.Range` 대신 시드 기반 `System.Random` 인스턴스 사용 (서버/클라 재현성 보장).
- **물리 처리**: `CharacterController`, 중력, `Rigidbody` 관련 처리는 모두 `FixedUpdate` + `Time.fixedDeltaTime`. 입력 읽기는 `Update`.

---

## Notes

- 규칙이 충돌하면 더 구체적인 규칙이 우선됩니다.
- 규칙 추가/변경이 필요하면 이 파일에 직접 수정합니다.
