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

## 3. 헝가리안 표기법

### 변수 접두사

타입 식별자는 **소문자**, 이후 이름은 **대문자로 시작(PascalCase)** 한다.

| 타입 | 접두사 | 예시 |
|---|---|---|
| `bool` | `b` | `bIsAlive`, `bIsDead` |
| `int` | `i` | `iKillCount`, `iLevel` |
| `float` | `f` | `fMoveSpeed`, `fCooldown` |
| `string` | `str` | `strEnemyName` |
| `GameObject` | `go` | `goPlayer`, `goProjectile` |
| `Transform` | `tr` | `trTarget`, `trSpawnPoint` |
| `T[]` | `arr` | `arrEnemies`, `arrSkills` |
| `List<T>` | `list` | `listEnemies`, `listSkills` |
| `Dictionary<K,V>` | `dict` | `dictSkillMap` |
| `Coroutine` | `co` | `coAttackRoutine` |
| `Action` / `Func` | `on` | `onDeath`, `onLevelUp` |
| `ScriptableObject` | `so` | `soCharacterData` |
| 커스텀 클래스/컴포넌트 | 없음 (타입명이 식별자) | `m_playerStats`, `m_skillManager` |

### 접근 제한자별 규칙

```csharp
// 멤버 변수: m_ + 소문자 타입 접두사 + PascalCase 이름
// public 필드는 최대한 자제 — 외부 노출 필요 시 프로퍼티 사용
private   float           m_fCurrentHP;
private   bool            m_bIsAttacking;
protected int             m_iLevel;
[SerializeField]
private   List<SkillBase> m_listEquippedSkills;
private   PlayerStats     m_playerStats;       // 커스텀 클래스: 타입 접두사 없이 m_ + camelCase

// const: 소문자 타입 접두사 + PascalCase 이름 (m_ 없음)
private const float fRespawnTime  = 3.0f;
private const int   iMaxSkillSlot = 6;

// static: 소문자 타입 접두사 + PascalCase 이름 (m_ 없음)
private static int iInstanceCount;

// 지역 변수: 소문자 타입 접두사 + PascalCase 이름
float fSpeed      = 5.0f;
bool  bIsGrounded = false;

// 인터페이스: I 접두사 (기존 C# 관례 유지)
public interface IDamageable { }

// 추상 클래스: Base 접미사
public abstract class SkillBase { }
public abstract class EnemyBase  { }
```

### 메서드 / 프로퍼티

```csharp
// 메서드: PascalCase, 동사로 시작, 파라미터도 소문자 타입 접두사 + PascalCase
public void TakeDamage(float fAmount) { }
private void SpawnEnemy() { }

// 프로퍼티: PascalCase, 헝가리안 없음 (타입 명확)
public float CurrentHP { get; private set; }
public bool  IsAlive   { get; private set; }

// 이벤트 채널 멤버 필드: m_ + on 접두사 + PascalCase
[SerializeField] private VoidEventSO m_onPlayerDied;
[SerializeField] private IntEventSO  m_onLevelUp;
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
// ✅ 올바른 null 체크 패턴
private void Start()
{
    m_playerStats = GetComponent<PlayerStats>();   // 멤버니까 m_ 붙음, 타입 접두사 생략 가능(컴포넌트류)
    if (m_playerStats == null)
    {
        Debug.LogError($"[{nameof(PlayerController)}] PlayerStats 컴포넌트를 찾을 수 없습니다.", this);
        enabled = false;   // 컴포넌트 비활성화로 이후 오류 방지
        return;
    }
}

// ✅ SerializeField null 체크
private void Awake()
{
    if (soCharacterData == null)
    {
        Debug.LogError($"[{nameof(PlayerStats)}] CharacterDataSO가 할당되지 않았습니다.", this);
        return;
    }
    m_fCurrentHP = soCharacterData.fBaseHP;
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

// ✅ try-catch: 외부 시스템(파일, 네트워크) 경계
private void LoadStageData(string strPath)
{
    try
    {
        // 로드 로직
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[{nameof(StageManager)}] 스테이지 데이터 로드 실패: {e.Message}");
    }
}

// ✅ early return: 조건 불충족 시 조기 반환
public void TakeDamage(float fAmount)
{
    if (!m_bIsAlive) return;
    if (fAmount <= 0f)
    {
        Debug.LogWarning($"[{nameof(EnemyBase)}] 유효하지 않은 데미지 값: {fAmount}");
        return;
    }
    m_fCurrentHP -= fAmount;
    if (m_fCurrentHP <= 0f) Die();
}
```

---

## 5. 추가 규칙

- **주석**: 코드가 **왜** 이렇게 짜여졌는지 비자명한 경우에만 한국어 주석 1줄 허용. 무엇을 하는지 설명하는 주석은 작성하지 않음.
- **Magic Number 금지**: 수치는 반드시 `const`, `SO 필드`, 또는 명명된 변수로 추출.
- **단위 표기**: `f` 접두사 float 변수 중 단위가 불명확하면 주석 또는 변수명에 단위 포함 (`fCooldownSec`, `fRangeMeters`).
- **멀티 대비**: 게임 로직(데미지, 스폰, 드롭)은 반드시 매니저 경유. 직접 처리 금지.
- **랜덤**: `Random.Range` 대신 시드 기반 `System.Random` 인스턴스 사용 (서버/클라 재현성 보장).

---

## Notes

- 규칙이 충돌하면 더 구체적인 규칙이 우선됩니다.
- 헝가리안 표기법과 C# 기본 스타일이 충돌할 경우, 헝가리안 우선 적용합니다.
- 규칙 추가/변경이 필요하면 이 파일에 직접 수정합니다.
