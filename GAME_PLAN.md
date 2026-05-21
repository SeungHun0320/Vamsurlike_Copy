# 3D 뱀서라이크 게임 개발 계획

> 작성일: 2026-05-19  
> 최종 정리: 2026-05-20  
> 엔진: Unity 3D  
> 구현 순서: 싱글플레이 완성 후 Co-op 멀티플레이 확장

---

## 1. 핵심 방향

| 항목 | 결정 |
|---|---|
| 장르 | 3D 쿼터뷰 뱀서라이크 액션 |
| 캐릭터 | 1명으로 시작, 이후 추가 |
| 조작 | WASD 이동, 자동 스킬 발동, 궁극기는 수동 발동 |
| 성장 | XP 획득 → 레벨업 → 스킬/패시브 선택 |
| 아이템 | 드랍 아이템 획득, 보유 스킬과 조합하여 진화 |
| 스테이지 | 시간 생존형. Stage 1은 5분, Stage 2는 10분, 이후 확장 |
| 최종 목표 | 30분 생존 스테이지까지 확장 가능하게 설계 |
| 멀티플레이 | 싱글 완성 후 Co-op 추가 |
| 네트워크 후보 | Unity Netcode for GameObjects + UGS Relay/Lobby |

---

## 2. 현재 예상 개발 기간

현재 프로젝트에는 에셋, 기본 패키지, `Assets/Scripts` 폴더가 준비되어 있으나 실제 게임플레이 코드는 아직 초기 단계이므로, 구현 상태는 Phase 0 후반~Phase 1 초입으로 본다.

| 목표 | 예상 기간 |
|---|---:|
| 최소 플레이 가능 루프: 이동, 적 스폰, 자동 공격, XP 획득 | 1.5~3주 |
| 싱글플레이 MVP: Stage 1, 스킬, 레벨업, 아이템, 보스, 결과창 | 4~7주 |
| 출시 가능한 싱글 데모: UI, 저장, 밸런스, 최적화 포함 | 6~10주 |
| Relay 기반 Co-op | 추가 4~8주 |
| 전용 서버/매치메이킹/배포 | 추가 6~12주 |

권장 목표는 먼저 **6~8주 안에 싱글플레이 데모**를 완성하고, 그 뒤 멀티플레이를 분리해서 진행하는 것이다.

---

## 3. 멀티플레이 대비 설계 원칙

싱글플레이부터 구현하되, 나중에 네트워크 구조로 갈아끼우기 쉽도록 아래 규칙을 지킨다.

1. 입력과 로직을 분리한다.  
   `PlayerInput`은 입력 수집만 담당하고, `PlayerController`가 이동 로직을 처리한다. 나중에 `NetworkPlayerInput`으로 교체할 수 있게 한다.

2. 게임 상태는 중앙 매니저에 모은다.  
   스테이지, 웨이브, 스폰, 드랍, 게임 상태를 흩뿌리지 않는다.

3. 직접 참조를 줄이고 Facade를 사용하되, 책임을 작게 유지한다.  
   `GameInstance.Instance.Core`, `GameInstance.Instance.World`, `PlayerDamageReceiver`를 통해 주요 시스템에 접근하되, Facade가 모든 로직을 떠안지 않게 하고 각 Manager의 역할을 명확히 나눈다.

4. 데이터는 ScriptableObject로 분리한다.  
   캐릭터, 적, 스킬, 아이템, 조합식, 웨이브, 스테이지는 코드가 아니라 데이터로 관리한다.

5. 랜덤은 시드 기반으로 관리하되, 판정은 Host/Server 권한을 우선한다.  
   `Random.Range` 남용을 피하고 `System.Random(seed)`를 사용하되, 물리/AI/프레임 차이까지 완전한 결정성을 기대하지 않는다. 멀티플레이에서는 스폰, 드랍, 데미지 같은 핵심 결과를 Host/Server가 확정한다.

6. 데미지, 드랍, 스폰 판정은 중앙 시스템을 통한다.  
   싱글플레이에서도 판정 경로를 한 곳으로 모아 나중에 서버 권한 모델로 전환하기 쉽게 만든다.

---

## 4. 아키텍처

### GameInstance

`GameInstance`는 Bootstrap 씬에서 생성되고 `DontDestroyOnLoad`로 유지된다.

```csharp
public class GameInstance : MonoBehaviour
{
    public static GameInstance Instance { get; private set; }

    [SerializeField] private CoreFacade  coreFacade;
    [SerializeField] private WorldFacade worldFacade;
    [SerializeField] private GameManager gameManager;

    public ICoreFacade  Core  => coreFacade;
    public IWorldFacade World => worldFacade;
    public GameManager  Game  => gameManager;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
```

### Facade 구조

```text
GameInstance
├─ ICoreFacade
│  ├─ AudioManager
│  ├─ SaveManager
│  ├─ SceneLoader
│  ├─ PoolManager
│  └─ TimeManager
├─ IWorldFacade
│  ├─ StageManager
│  ├─ WaveController
│  ├─ SpawnManager
│  ├─ DropManager
│  └─ MapManager
└─ GameManager
   └─ Playing / Paused / GameOver 상태 관리

Player GameObject
└─ PlayerDamageReceiver   ← IDamageable 구현체
   └─ (PlayerStats, PlayerController, PlayerAnimator 등과 함께 배치)
```

`PlayerDamageReceiver` 등 플레이어 컴포넌트들을 `GameInstance` 안에 넣지 않는 이유는 멀티플레이에서 플레이어마다 별도 인스턴스가 필요하기 때문이다.

---

## 5. 핵심 인터페이스

```csharp
public interface ICoreFacade
{
    void PlaySFX(AudioClip clip, Vector3 pos = default);
    void PlayBGM(AudioClip clip, float fadeTime = 1f);
    void SetBGMVolume(float value);
    void SetSFXVolume(float value);
    void LoadScene(string sceneName);
    void SaveSettings();
    GameSettings LoadSettings();
    T GetFromPool<T>(string key) where T : Component;
    void ReturnToPool<T>(string key, T obj) where T : Component;
    float GetGameTime();
}

public interface IWorldFacade
{
    float GetStageElapsedTime();
    bool IsStageCleared();
    void OnEnemyDied(EnemyBase enemy);
    void SpawnEnemy(EnemyDataSO data, Vector3 pos);
    Vector3 GetRandomSpawnPoint();
}

// IPlayerFacade는 Phase 4(레벨업) 이후 필요 시 추가한다.
// 현재 플레이어 진입점:
//   IDamageable  → PlayerDamageReceiver (HP 조회, TakeDamage)
//   PlayerStats  → HP, MoveSpeed 등 스탯 직접 참조
// Phase 4에서 XP·스킬·아이템 시스템이 붙을 때 IPlayerFacade를 정의한다.
```

---

## 6. Unity 설정

### 권장 버전

- Unity 6000.0.x LTS 또는 2022.3 LTS

### 필수 패키지

| 패키지 | 용도 |
|---|---|
| Input System | 플레이어 입력 |
| AI Navigation | NavMesh 기반 이동. 대량 적은 단순 추적/목적지 갱신 제한과 병행 |
| TextMeshPro | UI 텍스트 |
| Cinemachine | 쿼터뷰 카메라 |
| Newtonsoft Json | JSON 저장 |
| Unity Netcode for GameObjects | 멀티플레이 |
| Unity Gaming Services | Relay, Lobby |

### Physics Layer

| Layer | 이름 | 충돌 대상 |
|---:|---|---|
| 6 | Player | Ground, Enemy, XPOrb, Item |
| 7 | Enemy | Ground, Player, Projectile |
| 8 | Projectile | Enemy, Ground |
| 9 | XPOrb | Player |
| 10 | Item | Player |
| 11 | Ground | Player, Enemy, Projectile |

중요 설정:

- Projectile ↔ Player 충돌 OFF
- Enemy ↔ Enemy 충돌 OFF
- XPOrb와 Item은 Trigger 기반

### Tags

```text
Player, Enemy, Boss, Projectile, XPOrb, Item, Ground
```

### 카메라

```text
Cinemachine Virtual Camera
- Follow: Player
- Body: Framing Transposer
- Camera Distance: 12~15
- Damping X/Y/Z: 0.5
- Rotation: X 50, Y 45
- FOV: 40
```

### CharacterController

```text
Height: 1.8
Radius: 0.4
Step Offset: 0.3
Slope Limit: 45
Skin Width: 0.08
```

### 성능 목표

| 항목 | 목표 |
|---|---:|
| FPS | PC 기준 60fps |
| 화면 내 적 | 최종 목표 최대 200마리. MVP는 50~80마리부터 검증 |
| 활성 투사체 | 최대 100개 |
| XP 오브 | 최대 300개 |
| Draw Call | 300 이하 |
| Pool Warm-up | Enemy 50, Projectile 50, XPOrb 100 |

---

## 7. 전투 공식

### 데미지

```text
FinalDamage = SkillBaseDamage[level]
            * (1 + PlayerAttackMultiplier)
            * (1 - EnemyDefenseRate)

EnemyDefenseRate = enemyDefense / (enemyDefense + 100)
```

### 시간 기반 난이도 스케일링

```text
EnemyHP(t)     = BaseHP     * (1 + t / 60 * 0.15)
EnemyDamage(t) = BaseDamage * (1 + t / 60 * 0.10)
SpawnRate(t)   = BaseRate   * (1 + t / 60 * 0.20)
```

### 레벨업 XP

```text
XPRequired(level) = Mathf.RoundToInt(10f * Mathf.Pow(level, 1.5f))
```

---

## 8. 폴더 구조

```text
Assets/
├─ Scripts/
│  ├─ Core/
│  │  ├─ GameInstance.cs
│  │  ├─ GameManager.cs
│  │  ├─ TimeManager.cs
│  │  ├─ ObjectPool.cs
│  │  ├─ PoolManager.cs
│  │  ├─ SceneLoader.cs
│  │  ├─ SaveManager.cs
│  │  ├─ AudioManager.cs
│  │  ├─ Facades/
│  │  └─ Events/
│  ├─ Player/
│  ├─ Enemy/
│  ├─ Skills/
│  ├─ Items/
│  ├─ Upgrades/
│  ├─ Stage/
│  ├─ UI/
│  └─ Data/
├─ Data/
│  ├─ Characters/
│  ├─ Enemies/
│  ├─ Skills/
│  ├─ Items/
│  ├─ CombineRecipes/
│  └─ Stages/
├─ Prefabs/
│  ├─ Player/
│  ├─ Enemies/
│  ├─ Skills/
│  ├─ Items/
│  └─ VFX/
├─ Scenes/
│  ├─ Bootstrap.unity
│  ├─ MainMenu.unity
│  ├─ Stage_01.unity
│  └─ Stage_02.unity
└─ Resources/
   ├─ Models/
   ├─ Animations/
   ├─ Materials/
   ├─ Textures/
   └─ UI/
```

---

## 9. ScriptableObject 데이터

### CharacterDataSO

```text
string characterName
Sprite portrait
GameObject modelPrefab
float baseHP
float baseMoveSpeed
float baseAttackPower
float baseDefense
float basePickupRadius
SkillDataSO[] startingSkills
```

### EnemyDataSO

```text
string enemyName
GameObject prefab
float hp
float moveSpeed
float attackPower
float defense
float attackRange
float attackInterval
int xpDrop
DropTableSO dropTable
bool isElite
bool isBoss
```

### SkillDataSO

```text
string skillName
Sprite icon
SkillType skillType
bool isManual
int maxLevel
SkillLevelData[] levels
CombineRecipeSO evolutionRecipe
GameObject effectPrefab
AudioClip sfx
```

### ItemDataSO

```text
string itemName
Sprite icon
ItemType itemType
int maxLevel
ItemLevelData[] levels
```

### CombineRecipeSO

```text
SkillDataSO requiredSkill
ItemDataSO requiredItem
SkillDataSO resultSkill
bool isAutoEvolve
string evolutionDescription
```

### WaveDataSO

```text
string stageName
float surviveDuration
WaveEntryData[] entries
```

### StageDataSO

```text
string stageName
SceneReference sceneRef
WaveDataSO waveData
EnemyDataSO bossEnemy
float bossSpawnTime
AudioClip bgm
```

---

## 10. 구현 Phase

### Phase 0. 프로젝트 세팅

Done when: Bootstrap 씬이 켜지고 MainMenu 씬으로 자동 전환되며 콘솔 에러가 없다.

- [ ] Unity 버전 확정
- [ ] 필수 패키지 설치
- [ ] Physics Layer와 Tag 설정
- [ ] 폴더 구조 생성
- [ ] Bootstrap, MainMenu, Stage_01 씬 생성
- [ ] Bootstrap에서 MainMenu 자동 전환 테스트
- [ ] Git 초기화와 Unity `.gitignore` 추가

예상 기간: 1~2일

### Phase 1. 코어와 플레이어

Done when: 캐릭터가 WASD로 이동하고 Cinemachine 카메라가 따라오며 HP/MoveSpeed 값을 Inspector에서 확인할 수 있다.

- [x] GameInstance 최소 구조 구현
- [x] GameManager 기본 상태 구현
- [ ] SceneLoader 최소 구현
- [x] CharacterDataSO 구현
- [x] PlayerInput 구현
- [x] PlayerController 구현 (FixedUpdate, 중력, 카메라 상대 이동)
- [x] PlayerStats 구현 (CharacterDataSO 연결, TakeDamage, Heal)
- [x] PlayerDamageReceiver 구현 (IDamageable 구현, null 체크)
- [x] Cinemachine 카메라 설정 (CinemachineFollow offset -18,30,-18)
- [x] PlayerAnimator 연결 (상태머신, Speed/Die 파라미터)
- [x] 더미 적 1마리를 배치하고 데미지 로그 확인

Phase 1에서는 저장, 오디오, 범용 이벤트, 풀링을 완성하려고 하지 않는다. 먼저 움직이는 플레이어와 카메라, HP/MoveSpeed 데이터, 간단한 데미지 흐름을 확인한다.

예상 기간: 2~4일

### Phase 2. 적과 스폰

Done when: 적 3종이 화면 밖에서 스폰되고 플레이어를 추적하며 공격, 사망, XP 드랍까지 동작한다.

- [ ] EnemyDataSO 3종 작성
- [ ] WaveDataSO, WaveEntryData 구현
- [ ] EnemyBase 구현
- [ ] EnemyAI 구현
- [ ] 대량 적 이동 방식 결정: NavMeshAgent, 단순 추적, 또는 혼합 방식
- [ ] SpawnManager 구현
- [ ] WaveController 구현
- [ ] ObjectPool과 PoolManager 구현
- [ ] ExperienceOrb 구현
- [ ] Enemy 50개, XPOrb 100개 풀 예열

예상 기간: 4~6일

### Phase 3. 스킬 시스템

Done when: 자동 스킬이 주기적으로 발동하고 적에게 데미지를 주며 FloatingText가 표시된다.

- [ ] SkillDataSO와 SkillLevelData 구현
- [ ] SkillBase 구현
- [ ] AutoTargeting 구현
- [ ] SkillManager 구현
- [ ] ProjectileSkill 구현
- [ ] OrbitalSkill 구현
- [ ] AuraSkill 구현
- [ ] UltimateSkill 구현
- [ ] FloatingText 구현
- [ ] 기본 스킬 4~6종 데이터 작성

예상 기간: 4~7일

### Phase 4. 레벨업과 업그레이드

Done when: XP를 모아 레벨업하면 게임이 일시정지되고 선택지 3~4개가 표시되며 선택 결과가 적용된다.

- [ ] PlayerLevelSystem 구현
- [ ] PassiveStatHandler 구현
- [ ] LevelUpManager 구현
- [ ] UpgradeOption 구현
- [ ] LevelUpUI 구현
- [ ] 패시브 스탯 종류 정의
- [ ] XP 곡선 1차 밸런싱

예상 기간: 3~5일

### Phase 5. 아이템과 조합

Done when: 적이 아이템을 드랍하고, 아이템 보유 상태에 따라 스킬 진화 또는 조합 UI가 동작한다.

- [ ] ItemDataSO, ItemLevelData, DropTableSO 구현
- [ ] CombineRecipeSO 구현
- [ ] ItemBase 구현
- [ ] ItemManager 구현
- [ ] DropManager 구현
- [ ] ItemPickup 구현
- [ ] CombinationSystem 구현
- [ ] CombinationUI 구현
- [ ] SummonSkill 구현
- [ ] 아이템 6~8종, 조합식 3~4종 작성

예상 기간: 4~7일

### Phase 6. 스테이지와 보스

Done when: Stage_01에서 5분 생존, 중간 보스 등장, 승리/패배 결과 UI 표시가 동작한다.

- [ ] StageDataSO 구현
- [ ] StageManager 구현
- [ ] MapManager 구현
- [ ] BossBase 구현
- [ ] BossHealthBar 구현
- [ ] Stage_01 웨이브 데이터 작성
- [ ] 보스 1종 데이터 작성

예상 기간: 4~7일

### Phase 7. UI, 저장, 최적화, 밸런스

Done when: 메인 메뉴 → 스테이지 → 결과 화면 → 메인 메뉴 루프가 에러 없이 완성된다.

- [ ] GameEventSO와 EventListener 구현
- [ ] AudioManager 구현
- [ ] SaveManager 구현
- [ ] TimeManager 정리
- [ ] HUDController 구현
- [ ] SkillSlotUI, ItemSlotUI 구현
- [ ] ResultUI 구현
- [ ] LoadingScreen 구현
- [ ] MainMenu 구현
- [ ] 설정 UI 구현
- [ ] 카메라 쉐이크 연결
- [ ] 씬 전환 연출
- [ ] GPU Instancing 적용
- [ ] Profiler로 FPS/Draw Call 확인
- [ ] XP, 적 스케일링, 스킬 수치 최종 밸런싱

예상 기간: 1~2주

### Phase 8. 멀티플레이

싱글플레이 완성 후 시작한다.

#### Phase 8a. Host-Client + Relay

- [ ] Unity NGO, Unity Transport, UGS SDK 설치
- [ ] NetworkBootstrapper 구현
- [ ] LobbyManager 구현
- [ ] RelayManager 구현
- [ ] NetworkPlayerInput 구현
- [ ] PlayerController 네트워크 동기화
- [ ] PlayerStats HP 동기화
- [ ] EnemyBase Host 권한 처리
- [ ] SpawnManager와 WaveController 서버 권한 처리
- [ ] 스킬 판정 ServerRpc/ClientRpc 구조로 전환
- [ ] 플레이어별 LevelUpManager 처리
- [ ] DropManager Host 권한 처리
- [ ] 2~4인 Co-op 테스트

예상 기간: 4~8주

#### Phase 8b. Dedicated Server

- [ ] Dedicated Server 빌드 타깃 추가
- [ ] ServerGameManager와 ClientGameManager 분리
- [ ] NetworkInputData 구현
- [ ] Client-side Prediction 기초 구현
- [ ] Server Reconciliation 기초 구현
- [ ] NetworkTickSystem 30tick/s 기준 동기화
- [ ] 치트 방지 기초 검증
- [ ] 서버 빌드와 클라이언트 빌드 분리 테스트

예상 기간: 4~8주

#### Phase 8c. 서버 호스팅과 매치메이킹

- [ ] Linux 서버 빌드
- [ ] VPS 또는 UGS Multiplay 배포
- [ ] MatchmakingManager 구현
- [ ] 서버 헬스체크 구현
- [ ] 서버 로그 수집
- [ ] 동시 접속 부하 테스트

예상 기간: 2~4주

---

## 11. 우선순위 로드맵

```text
Phase 0: 프로젝트 세팅
  ↓
Phase 1: 플레이어 이동 + 코어 매니저
  ↓
Phase 2: 적 스폰/AI + XP
  ↓
Phase 3: 스킬 시스템
  ↓
Phase 4: 레벨업/업그레이드
  ↓
Phase 5: 아이템/조합
  ↓
Phase 6: 스테이지/보스
  ↓
Phase 7: UI/저장/최적화/밸런스
  ↓
Phase 8a: NGO + Relay Co-op
  ↓
Phase 8b: Dedicated Server
  ↓
Phase 8c: 서버 호스팅/매치메이킹
```

---

## 12. 주요 리스크

| 리스크 | 영향 | 대응 |
|---|---|---|
| 멀티플레이 전환 | 매우 큼 | 싱글 코드부터 입력, 판정, 랜덤, 상태 관리를 분리 |
| 200마리 적 성능 | 큼 | Pool, NavMesh 갱신 빈도 제한, GPU Instancing 사용 |
| XP 오브 300개 | 중간 | OverlapSphere 체크 주기를 0.1초 단위로 제한 |
| 스킬/아이템 밸런스 | 큼 | 데이터 기반 수치 조정, 초반에는 스킬 수를 제한 |
| UI 작업량 | 중간 | MVP에서는 HUD, 레벨업, 결과창만 먼저 구현 |
| 전용 서버 | 매우 큼 | Phase 8a Relay Co-op이 안정된 뒤 별도 진행 |

---

## 13. 개발 원칙

1. 먼저 재미있는 5분짜리 Stage 1을 만든다.
2. 시스템을 너무 일찍 일반화하지 않는다.
3. 단, 멀티플레이 전환을 막는 구조는 피한다.
4. 데이터는 ScriptableObject로 빼서 Inspector에서 조정 가능하게 한다.
5. `Destroy` 대신 Pool을 기본으로 사용한다.
6. 데미지 판정은 성능을 위해 이벤트보다 직접 호출을 우선한다.
7. 씬은 Bootstrap → MainMenu → Stage_XX 흐름으로 유지한다.
8. 매 Phase 끝마다 플레이 가능한 상태를 만든다.
