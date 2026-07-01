# 3D 뱀서라이크 멀티플레이 게임 개발 계획

> 작성일: 2026-05-19  
> 전면 개정: 2026-05-22 — 처음부터 서버 권한 기반 멀티플레이로 전환  
> 엔진: Unity 6 LTS  
> 구현 순서: 네트워크 파운데이션 → 네트워크 플레이어 → 게임플레이 시스템

---

## 1. 핵심 방향

| 항목 | 결정 |
|---|---|
| 장르 | 3D 쿼터뷰 뱀서라이크 Co-op 액션 |
| 플레이어 수 | 1~4인 Co-op |
| 조작 | WASD 이동, 자동 스킬, 궁극기 수동 발동 |
| 성장 | XP → 레벨업 → 스킬/패시브 선택 (공유 XP → 동시 레벨업 → 시간 정지 → 각자 선택 UI → 전원 완료 후 재개) |
| 아이템 | 드랍 획득 + 스킬 조합 진화 |
| 스테이지 | 시간 생존형. Stage 1: 5분, 최장 30분 확장 가능 |
| 네트워크 | Unity Netcode for GameObjects + UGS (Relay, Lobby) |
| 서버 | 로컬 Windows PC에서 Server Build 실행. 원격 플레이는 Relay 코드 공유 방식 |
| 솔로 플레이 | 로컬 Host로 진행 (서버 + 클라이언트 동일 프로세스) |
| 기존 코드 | Phase 1 코드 전면 폐기. NGO 기반으로 재작성 |

---

## 2. 예상 개발 기간

| 목표 | 예상 기간 |
|---|---:|
| 네트워크 연결 + 2인 이동 동기화 | 2~3주 |
| 4인 Co-op MVP: 이동, 적, 자동 스킬, XP | 5~8주 |
| 싱글플레이 데모 수준 콘텐츠 (스킬, 레벨업, 보스) | 8~12주 |
| 로컬 서버 빌드 안정화 + 친구와 원격 플레이 | 추가 1~2주 |

---

## 3. 네트워크 아키텍처

### 3.1 서버 권한 모델

모든 게임 결정은 **서버**가 내린다. 클라이언트는 의도(intent)만 보내고 결과를 표시한다.

| 시스템 | 권한 | 방식 |
|---|---|---|
| 플레이어 이동 | **서버** | 입력 방향 `[ServerRpc]` → 서버가 이동·중력 처리 → NetworkTransform 보간 |
| 데미지 판정 | 서버 | 서버 내부 계산. 클라이언트는 데미지 값을 보내지 않음 |
| 스킬 발동 | 서버 | 클라이언트 요청(의도) → 서버 쿨다운 검증 → 서버 실행 |
| 적 AI / 이동 | 서버 전용 | `IsServer` 체크, 클라이언트는 NetworkTransform 수신만 |
| 스폰 / 웨이브 | 서버 전용 | SpawnManager, WaveController 서버에서만 실행 |
| XP 드랍 | 서버 | 서버 데이터 목록 + 클라이언트 XPOrbVisualProxy (NetworkObject 아님) |
| 아이템 드랍 | 서버 | NetworkedItemPickup (NetworkObject — 드랍 빈도 낮아 허용) |
| 레벨업 옵션 | 서버 | 서버가 옵션 생성 → NGO 2.x: `[Rpc(SendTo.SpecificClients)]`, NGO 1.x: `ClientRpcParams`로 해당 clientId에만 전달 |
| 게임 상태 | 서버 | Playing / LevelingUp / ChestOpening / BossPhase / Clear / GameOver를 NetworkVariable로 동기화 |

### 3.2 연결 흐름

운영 환경도 로컬 Windows 서버다. Linux 배포, Matchmaking, Multiplay는 사용하지 않는다.

#### 개발 (에디터)

```text
[에디터 Host]
    └─ NetworkManager.StartHost()  ← 서버 + 클라이언트 동시 실행
       빠른 반복용. Multiplayer Play Mode로 클라이언트 추가
```

#### 운영 (로컬 서버 빌드)

```text
[서버 PC — Windows Server Build]
    └─ StartServer() 실행
       ──▶ UGS Relay Allocation (Relay 코드 획득)
       ──▶ 코드 공유 (Discord, 문자 등)

[클라이언트 PC]
    └─ 코드 입력 ──▶ Relay 경유 서버 접속 ──▶ StartClient()

[같은 LAN이면] 직접 IP 접속도 가능 (Relay 불필요)
```

> **Phase 1 필수:** `StartServer()` 로컬 smoke test를 Phase 1 완료 기준에 포함한다. Host 모드로만 테스트하면 서버 전용 경로 버그를 뒤늦게 발견한다.

### 3.3 개발 환경 테스트

| 방법 | 단계 | 용도 |
|---|---|---|
| Unity Multiplayer Play Mode | Phase 1~ | 에디터 안에서 가상 플레이어 4개 실행 |
| ParrelSync | Phase 1~ | 에디터 2개 동시 실행 (Host + Client 역할 분리) |
| 로컬 Windows Server Build | Phase 1 완료 기준 | `StartServer()` 경로 smoke test |
| Server Build + 에디터 클라이언트 | Phase 2~ | 운영 환경과 동일한 구성으로 테스트 |

---

## 4. 아키텍처

### GameNetworkManager

Host / Client / Server 모드 진입점을 통합 관리한다.

구현 방식은 NGO 버전에 따라 두 가지 중 하나를 선택한다:

- **상속 방식**: `NetworkManager`를 직접 상속. 내부 메서드 접근이 편리하지만 NGO 버전 업에 따라 파괴적 변경이 생길 수 있다.
- **래퍼 방식 (권장)**: `MonoBehaviour`로 두고 `NetworkManager.Singleton.StartHost()` 등을 호출. NetworkManager와 결합도가 낮아 유지보수가 안전하다.

```csharp
// 래퍼 방식 예시
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Instance { get; private set; }

    public void StartAsHost()   { /* Relay Allocation 생성 후 NetworkManager.Singleton.StartHost() */ }
    public void StartAsClient() { /* Relay Join 후 NetworkManager.Singleton.StartClient() */ }
    public void StartAsServer() { /* Relay Allocation 생성 또는 직접 IP로 NetworkManager.Singleton.StartServer() */ }
}
```

### GameInstance (비-네트워크 싱글턴)

GameInstance는 NetworkObject가 아니다. Bootstrap에서 생성, DontDestroyOnLoad. 로컬 서비스(오디오, 풀, 씬)만 관리한다.

```csharp
public class GameInstance : MonoBehaviour
{
    public static GameInstance I { get; private set; }
    public ICoreFacade  Core  => coreFacade;
    public IWorldFacade World => worldFacade;   // 서버에서만 실질적으로 동작
}
```

### 전체 구조

```text
Bootstrap (DontDestroyOnLoad)
├─ GameNetworkManager       ← NGO NetworkManager 상속
├─ GameInstance
│  ├─ ICoreFacade
│  │  ├─ AudioManager       (클라이언트: 로컬 사운드)
│  │  ├─ SaveManager        (클라이언트: 로컬 세이브)
│  │  ├─ SceneLoader        (네트워크 씬 로딩 포함)
│  │  └─ PoolManager        (서버/클라이언트 각자 풀)
│  └─ IWorldFacade
│     ├─ StageNetworkManager ← IsServer 체크 후 실행
│     ├─ WaveController      ← IsServer 전용
│     ├─ NetworkSpawnManager ← IsServer 전용
│     └─ DropManager         ← IsServer 전용
│
└─ LobbyManager / RelayManager

Stage 씬 (Network Objects)
├─ NetworkedPlayer (per player)
│  ├─ PlayerNetworkController  ← CharacterController + NetworkTransform
│  ├─ PlayerNetworkStats       ← NetworkVariable<float> HP, MoveSpeed, PickupRadius 등
│  ├─ PlayerNetworkInput       ← 입력 수집 → ServerRpc
│  ├─ PlayerNetworkAnimator    ← NetworkAnimator
│  ├─ SkillManager             ← IsServer에서 스킬 쿨다운 관리
│  └─ PassiveStatHandler       ← 플레이어별 패시브 스탯 배율
│
├─ SharedLevelSystem (stage/global)
│  ├─ NetworkVariable<float> SharedXP
│  └─ NetworkVariable<int> SharedLevel
│
├─ NetworkedEnemy (per enemy, server spawned)
│  ├─ EnemyNetworkBase         ← IsServer에서 AI 실행
│  └─ NetworkTransform         ← 서버→클라이언트 위치 동기화
│
├─ XPOrbVisualProxy (NetworkObject 아님 — 클라이언트 로컬 비주얼, 픽업은 PlayerPickupController ServerRpc)
└─ NetworkedItemPickup (NetworkObject — 아이템은 드랍 빈도가 낮아 개별 동기화 허용)
```

---

## 5. 핵심 인터페이스

```csharp
// 로컬 서비스 (변경 없음)
public interface ICoreFacade
{
    void PlaySFX(AudioClip clip, Vector3 pos = default);
    void PlayBGM(AudioClip clip, float fadeTime = 1f);
    void LoadScene(string sceneName);
    void SaveSettings();
    GameSettings LoadSettings();
    T GetFromPool<T>(string key) where T : Component;
    void ReturnToPool<T>(string key, T obj) where T : Component;
}

// 서버 전용 게임 상태 (클라이언트에서 호출 시 로그 경고)
public interface IWorldFacade
{
    float GetStageElapsedTime();
    bool IsStageCleared();
    void OnEnemyDied(EnemyNetworkBase enemy, ulong killerClientId);
    void SpawnEnemy(EnemyDataSO data, Vector3 pos);
    Vector3 GetRandomSpawnPoint();
}

// 네트워크 플레이어 접근 진입점 (Phase 5 이후 추가)
public interface IPlayerNetworkFacade
{
    NetworkVariable<float> HP { get; }
    void ApplyLevelUpChoice(int choiceIndex); // ServerRpc (SubmitLevelUpChoiceServerRpc)
}

// 공유 레벨 시스템 — XP/Level은 플레이어별이 아닌 게임 전체 공유 (Phase 5 이후 추가)
public interface ISharedLevelSystem
{
    NetworkVariable<float> SharedXP    { get; }
    NetworkVariable<int>   SharedLevel { get; }
    void AddXP(float amount);          // IsServer 체크 내부 처리
}
```

---

## 6. NetworkBehaviour 패턴

### 서버 전용 로직 분리

```csharp
public class EnemyNetworkBase : NetworkBehaviour
{
    private void Update()
    {
        if (!IsServer) return;   // 서버에서만 AI 실행
        UpdateAI();
    }

    [ClientRpc]
    private void PlayDeathVFXClientRpc() { /* 모든 클라이언트 VFX */ }
}
```

### 입력 → ServerRpc (서버 권한 이동)

```csharp
public class PlayerNetworkInput : NetworkBehaviour
{
    private void Update()
    {
        if (!IsOwner) return;
        Vector2 dir = actions.Player.Move.ReadValue<Vector2>();
        SubmitMoveInputServerRpc(dir);   // 의도(방향)만 전송
    }

    [ServerRpc]
    private void SubmitMoveInputServerRpc(Vector2 dir)
    {
        // 서버가 속도 적용·중력·충돌 처리 후 위치 결정
        // NetworkTransform(Server Authority)이 클라이언트로 보간 전송
    }
}
```

### 특정 클라이언트에만 RPC 전송 (NGO 문법)

NGO에는 Mirror의 `[TargetRpc]`가 없다. NGO 2.x 방식(Unity 6 기준):

```csharp
// NGO 2.x — [Rpc(SendTo.SpecificClients)]
[Rpc(SendTo.SpecificClients)]
private void ShowLevelUpOptionsRpc(int[] optionIndices, RpcParams rpcParams = default) { }

// 호출
ShowLevelUpOptionsRpc(optionIndices, RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
```

NGO 1.x를 사용할 경우 `ClientRpcParams`로 대체:

```csharp
// NGO 1.x — ClientRpcParams
[ClientRpc]
private void ShowLevelUpOptionsClientRpc(int[] optionIndices, ClientRpcParams rpcParams = default) { }

// 호출
ShowLevelUpOptionsClientRpc(optionIndices, new ClientRpcParams {
    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
});
```

> `UpgradeOptionSO` 같은 ScriptableObject는 NGO로 직접 직렬화하지 않는다. 서버는 고정된 업그레이드 카탈로그의 `int[] optionIndices`만 전송하고, 클라이언트는 로컬 `UpgradeOptionSO[]`를 같은 인덱스로 조회해 UI를 표시한다.

### NetworkVariable

```csharp
private NetworkVariable<float> _hp = new NetworkVariable<float>(
    100f,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server
);
```

---

## 7. Unity 설정

### 필수 패키지

| 패키지 | 용도 |
|---|---|
| Netcode for GameObjects (NGO) | 네트워크 핵심 |
| Unity Transport | 전송 레이어 |
| Multiplayer Play Mode | 에디터 내 멀티 테스트 |
| Multiplayer Tools | 네트워크 Profiler, Scene Debugger |
| UGS Authentication | UGS 로그인 |
| UGS Lobby | 방 생성·참여 (미래 확장용, 초기엔 Relay 코드 직접 공유로 대체 가능) |
| UGS Relay | 로컬 서버 ↔ 원격 클라이언트 중계 |
| Input System | 플레이어 입력 |
| AI Navigation | NavMesh 기반 적 이동 |
| Cinemachine | 쿼터뷰 카메라 (클라이언트 전용) |
| TextMeshPro | UI 텍스트 |
| Newtonsoft Json | JSON 저장 |

### Physics Layer

| Layer | 이름 | 충돌 대상 |
|---:|---|---|
| 6 | Player | Ground, Enemy, XPOrb, Item |
| 7 | Enemy | Ground, Player, Projectile |
| 8 | Projectile | Enemy, Ground |
| 9 | XPOrb | Player |
| 10 | Item | Player |
| 11 | Ground | Player, Enemy, Projectile |

중요:
- Projectile ↔ Player 충돌 OFF
- Enemy ↔ Enemy 충돌 OFF
- XPOrb, Item은 Trigger 기반 + ServerRpc 픽업 검증

### Tags

```text
Player, Enemy, Boss, Projectile, XPOrb, Item, Ground
```

### 카메라 (클라이언트 전용 — IsLocalPlayer 기준으로 생성)

```text
Cinemachine Virtual Camera
- Follow: 로컬 플레이어 Transform
- Body: Framing Transposer, Distance 12~15
- Rotation: X 50, Y 45 / FOV 40
- Damping X/Y/Z: 0.5
```

### CharacterController

```text
Height: 1.8 / Radius: 0.4
Step Offset: 0.3 / Slope Limit: 45 / Skin Width: 0.08
```

### 성능 목표

| 항목 | 목표 | 비고 |
|---|---:|---|
| FPS | 60fps (PC) | |
| 화면 내 적 | 최대 200마리 | MVP는 50~80마리부터 검증 |
| 활성 투사체 | 최대 100개 | NetworkObject. Phase 4에서 검증 |
| XP 오브 | 최대 300개 | NetworkObject 아님 — 아래 참고 |
| Draw Call | 300 이하 | GPU Instancing 필수 |
| 네트워크 틱 | 서버 30tick/s | NGO `NetworkManager.NetworkTickSystem` |
| Pool Warm-up | Enemy 50, Projectile 50 | XPOrb는 별도 처리 |
| Object Visibility | Phase 3 도입 | `CheckObjectVisibility` 거리 기반, 범위 밖 Enemy 동기화 제외 |

**XP 오브 처리 방식 (NetworkObject 회피):** XP 오브를 전부 NetworkObject로 두면 300개 Spawn/Despawn이 서버·클라이언트 모두 부담이다. 대신:
- 서버: `XPOrbData { Vector3 pos; ulong id; }` 목록 관리 (NetworkList 또는 ServerRpc 브로드캐스트)
- 클라이언트: 로컬 비주얼 프록시 오브젝트 생성 (NetworkObject 아님)
- 픽업: 클라이언트 OverlapSphere → 소유 `PlayerPickupController.RequestXPPickupServerRpc(ulong orbId)` → 서버 검증 후 `SharedLevelSystem.AddXP(orbData.xp)` → 목록에서 제거 → `[ClientRpc] DestroyOrbVisualClientRpc(ulong id)` 통보

**Object Visibility (Phase 3 필수 검증):** Phase 8까지 미루면 적 200마리 동기화 부하를 뒤늦게 발견한다. Phase 3에서 50마리 기준 Network Profiler를 먼저 확인하고 `NetworkObject.CheckObjectVisibility` 오버라이드로 거리 기반 Visibility를 구현해 Phase 3 완료 기준에 포함한다.

---

## 8. 전투 공식

### 데미지 (서버에서만 계산)

```text
FinalDamage = SkillBaseDamage[level]
            * (1 + PlayerAttackMultiplier)
            * (1 - EnemyDefenseRate)

EnemyDefenseRate = enemyDefense / (enemyDefense + 100)
```

### 시간 기반 난이도 스케일링 (서버 WaveController)

```text
EnemyHP(t)     = BaseHP     * (1 + t / 60 * 0.15)
EnemyDamage(t) = BaseDamage * (1 + t / 60 * 0.10)
SpawnRate(t)   = BaseRate   * (1 + t / 60 * 0.20)
```

### 레벨업 XP

```text
XPRequired(level) = Mathf.RoundToInt(10f * Mathf.Pow(level, 1.5f))
```

### Co-op 밸런싱 (플레이어 수 연동)

```text
EnemyHP     *= 1 + (playerCount - 1) * 0.3f
SpawnRate   *= 1 + (playerCount - 1) * 0.5f
XP          = 공유 풀 (SharedXP). 플레이어 수에 관계없이 XP는 하나의 NetworkVariable<float>로 관리
              → 적 XP 드랍량은 플레이어 수에 따라 스케일하지 않음 (공유이므로 자연 조정)
```

---

## 9. 폴더 구조

```text
Assets/
├─ Scripts/
│  ├─ Network/
│  │  ├─ GameNetworkManager.cs
│  │  ├─ NetworkBootstrapper.cs
│  │  ├─ LobbyManager.cs
│  │  └─ RelayManager.cs
│  ├─ Core/
│  │  ├─ GameInstance.cs
│  │  ├─ GameManager.cs          ← NetworkVariable<GameState>
│  │  ├─ ObjectPool.cs
│  │  ├─ PoolManager.cs
│  │  ├─ SceneLoader.cs          ← NetworkManager.SceneManager 연동
│  │  ├─ SaveManager.cs
│  │  ├─ AudioManager.cs
│  │  ├─ Facades/
│  │  └─ Events/
│  ├─ Player/
│  │  ├─ PlayerNetworkController.cs
│  │  ├─ PlayerNetworkStats.cs
│  │  ├─ PlayerNetworkInput.cs
│  │  ├─ PlayerNetworkAnimator.cs
│  │  └─ SkillManager.cs
│  ├─ Enemy/
│  │  ├─ EnemyNetworkBase.cs
│  │  ├─ EnemyAI.cs              ← IsServer 전용
│  │  └─ BossNetworkBase.cs
│  ├─ Skills/
│  │  ├─ SkillNetworkBase.cs
│  │  ├─ AutoTargeting.cs
│  │  ├─ ProjectileNetworkSkill.cs
│  │  ├─ OrbitalNetworkSkill.cs
│  │  └─ AuraNetworkSkill.cs
│  ├─ Items/
│  ├─ Stage/
│  │  ├─ StageNetworkManager.cs
│  │  ├─ SharedLevelSystem.cs
│  │  ├─ WaveController.cs
│  │  ├─ NetworkSpawnManager.cs
│  │  └─ DropManager.cs
│  ├─ Upgrades/
│  │  ├─ LevelUpManager.cs
│  │  ├─ UpgradeOptionSO.cs
│  │  └─ PassiveStatHandler.cs
│  ├─ UI/
│  └─ Data/
├─ Data/
│  ├─ Characters/ / Enemies/ / Skills/ / Items/ / CombineRecipes/ / Stages/
├─ Prefabs/
│  ├─ Player/ / Enemies/ / Skills/ / Items/ / VFX/
├─ Scenes/
│  ├─ Bootstrap.unity
│  ├─ MainMenu.unity
│  ├─ Stage_01.unity
│  └─ Stage_02.unity
└─ Resources/
   ├─ Models/ / Animations/ / Materials/ / Textures/ / UI/
```

---

## 10. ScriptableObject 데이터

### CharacterDataSO

```text
string characterName / Sprite portrait / GameObject modelPrefab
float baseHP / baseMoveSpeed / baseAttackPower / baseDefense / basePickupRadius
SkillDataSO[] startingSkills
```

### EnemyDataSO

```text
string enemyName / GameObject prefab
float hp / moveSpeed / attackPower / defense / attackRange / attackInterval
int xpDrop / DropTableSO dropTable / bool isElite / bool isBoss
```

### SkillDataSO

```text
string skillName / Sprite icon / SkillType skillType / bool isManual
int maxLevel / SkillLevelData[] levels
CombineRecipeSO evolutionRecipe / GameObject effectPrefab / AudioClip sfx
```

### ItemDataSO / CombineRecipeSO

구조는 기존 계획과 동일.

### DataTableSO 패턴 (Phase 7 도입)

스테이지·웨이브처럼 **동종 데이터가 여러 행**으로 늘어나는 경우, 개별 `.asset` 파일 대신 단일 테이블 에셋으로 관리한다.

| 테이블 에셋 | 행 타입 | 위치 |
|---|---|---|
| `StageTable.asset` | `StageRow` | `Assets/Data/Stages/` |
| `WaveTable.asset` | `WaveRow` | `Assets/Data/Stages/` |
| `EnemyScalingTable.asset` | `ScalingRow` | `Assets/Data/Stages/` |

규칙: 행 타입은 `[Serializable] struct`. 외부 참조(EnemyDataSO 등)는 필드로 허용. 인덱스 또는 ID 기반 조회는 `DataTableSO<TRow>.TryGet(int index)` 통일.

---

## 11. 구현 Phase

### Phase 0. 프로젝트 세팅

Done when: Bootstrap → MainMenu 씬이 에러 없이 전환되고 NGO/UGS 패키지가 설치되어 있다.

- [ ] Unity 6 LTS 버전 확정
- [ ] 필수 패키지 설치 (NGO, Transport, Multiplayer Play Mode, UGS SDK 포함)
- [ ] Physics Layer, Tag 설정
- [ ] 폴더 구조 생성
- [ ] Bootstrap, MainMenu, Stage_01 씬 생성
- [ ] Bootstrap → MainMenu 자동 전환 테스트
- [ ] Git 초기화 (Unity .gitignore)
- [ ] Multiplayer Play Mode 환경 설정 (가상 플레이어 4개)

예상 기간: 1~2일

---

### Phase 1. 네트워크 파운데이션

Done when: 2개 이상의 클라이언트가 Relay 또는 로컬 직접 접속(127.0.0.1)으로 서버에 연결되고, 메인 메뉴 씬에서 "플레이어 X명 접속" 로그가 찍힌다.

- [ ] GameNetworkManager 구현 (Host / Client / Server 모드 분기)
- [ ] NetworkBootstrapper 구현 (Bootstrap 씬 초기화)
- [ ] UGS Authentication 초기화 (익명 로그인)
- [ ] LobbyManager 구현 (방 생성, 방 검색, 방 참여)
- [ ] RelayManager 구현 (Relay 코드 발급 · 접속)
- [ ] 메인 메뉴 연결 UI (방 만들기 / 참여 / 솔로 시작)
- [ ] GameInstance 최소 구조 (DontDestroyOnLoad, ICoreFacade / IWorldFacade 인터페이스)
- [ ] Windows Dedicated Server Build 타깃 추가 (`UNITY_SERVER` 심볼 등록)
- [ ] **로컬 Windows Server Build smoke test** — 서버 빌드를 로컬에서 실행해 클라이언트 접속 로그 확인 (Phase 1 완료 기준)
- [ ] Multiplayer Play Mode로 Host + Client 2인 접속 테스트
- [ ] SceneLoader 구현 (NetworkManager.SceneManager 기반 씬 동기화)
- [ ] 로컬 PC 간이 서버 실행 경로 구현 (`-server` 또는 `-batchmode` 실행 시 StartServer())
- [ ] Editor Client가 `127.0.0.1` 서버에 접속하는 테스트
- [ ] Client Build 2개가 로컬 서버에 접속하는 테스트

예상 기간: 5~8일

---

### Phase 2. 네트워크 플레이어

Done when: 4명이 Stage_01에 접속하고 WASD 이동이 모든 클라이언트에서 동기화되며 로컬 Cinemachine이 각자 자신의 캐릭터를 따라간다.

- [ ] PlayerNetworkController 구현
  - NetworkBehaviour + CharacterController
  - NetworkTransform (Server Authority: 서버가 위치를 쓰고 클라이언트는 보간 수신)
  - `[ServerRpc] SubmitMoveInputServerRpc(Vector2 dir)` — 입력 의도만 전송
  - FixedUpdate 기반 중력·이동·충돌 (서버에서 실행)
  - 서버: 최대 속도 초과 검증 (Speed Hack 방어)
- [ ] PlayerNetworkInput 구현
  - IsOwner일 때만 Input Action 수집
  - 매 FixedUpdate마다 이동 방향 ServerRpc 전송
- [ ] PlayerNetworkStats 구현
  - `NetworkVariable<float>` HP, MoveSpeed
  - `TakeDamage(float amount)` — **서버 내부 메서드**, 클라이언트가 데미지 값을 보내지 않음
  - HUD는 `hp.OnValueChanged` 구독으로 갱신 (별도 ClientRpc 불필요)
  - 피격 연출처럼 값과 별개의 이벤트가 필요할 때만 `[ClientRpc]` 추가
- [ ] PlayerNetworkAnimator 구현 (NetworkAnimator 연동)
- [ ] `PlayerNetworkStats`에 다운 상태 추가 (2단계 다운 시스템)
  - `NetworkVariable<bool>` IsDowned — 1단계: HP=0 직후 진입, 동료 부활 가능
  - `NetworkVariable<bool>` IsDeadWaiting — 2단계: 1단계 타이머 만료 후 진입, 부활 불가·자동 부활 대기 중
  - `NetworkVariable<float>` DownedTimeRemaining — 1단계 카운트다운 (동료 부활 가능 창)
  - `CanAct = IsAlive && !IsDowned.Value && !IsDeadWaiting.Value` — 이동·스킬 발동 조건
- [ ] PlayerReviveHandler 구현 (서버 권한, 부활 흐름 전담)
  - `static List<PlayerReviveHandler> All` — 범위 탐색용 레지스트리 (OnNetworkSpawn/Despawn 자동 등록)
  - `BeginReviveServerRpc` / `CancelReviveServerRpc` — `RequireOwnership = false` (1단계 중에만 가능)
  - 구조자가 일정 거리 내에 있는 동안 진행도 누적 → 완료 시 `IsDowned=false` + HP 일부 복구 (deathCount 증가 없음)
  - 1단계 타이머 만료 → 2단계(DeadWaiting) 진입: `NetworkVariable<float>` DeadWaitRemaining 카운트다운
  - 2단계 대기 시간 = `baseDeadWaitDuration × (1 + deathPenaltyRatio)^(deathCount-1)` (사망마다 증가)
  - 2단계 타이머 만료 → 자동 부활 (HP 일부 복구)
- [ ] `PlayerNetworkInput`에 E키 부활 상호작용 추가 (`PlayerReviveHandler.All` 레지스트리 순회로 탐색)
- [ ] 로컬 Cinemachine 설정 (OnNetworkSpawn에서 IsLocalPlayer 기준으로 카메라 활성화)
- [ ] NetworkPlayerSpawner 구현 (서버가 플레이어 스폰 위치 지정)
- [ ] CharacterDataSO 연결 (baseHP, baseMoveSpeed 초기화)
- [ ] 더미 적 배치 테스트 (서버 내부에서 TakeDamage 호출 → NetworkVariable HP 감소 → 클라이언트 HUD 확인)

예상 기간: 3~5일

---

### Phase 3. 적과 스폰

Done when: 서버가 적 3종을 스폰하고 NavMesh로 플레이어를 추적하며, 공격·사망·XP 드랍이 모든 클라이언트에 동기화된다.

- [ ] EnemyDataSO 3종 작성
- [ ] WaveDataSO, WaveEntryData 구현 (Co-op 플레이어 수 배율 포함) — Phase 7에서 WaveTableSO로 대체되는 임시 구조
- [ ] EnemyNetworkBase 구현
  - NetworkBehaviour + NetworkTransform (Server Authority)
  - AI 로직은 `if (!IsServer) return;` 가드
  - `[ClientRpc] PlayDeathVFXClientRpc()` — 전체 클라이언트 사망 연출
- [ ] EnemyAI 구현 (NavMeshAgent, 서버 전용)
- [ ] NetworkSpawnManager 구현 (서버가 NetworkObject.Spawn())
- [ ] WaveController 구현 (서버 전용, 스폰 간격·갯수 관리)
- [ ] NetworkObjectPool 구현 (NGO 기반 풀링 — Enemy, Projectile 전용)
- [ ] XPOrb 구현 (NetworkObject **아님**)
  - 서버: `XPOrbData { ulong id; Vector3 pos; int xp; }` 목록 관리
  - 적 사망 시 서버 `[ClientRpc] SpawnXPOrbVisualClientRpc(ulong id, Vector3 pos)` → 클라이언트 비주얼 프록시 로컬 생성
  - 클라이언트 OverlapSphere → 소유 `PlayerPickupController.RequestXPPickupServerRpc(ulong orbId)` (PlayerPickupController : NetworkBehaviour) → 서버 검증 후 `SharedLevelSystem.AddXP(orbData.xp)` → 목록 제거 → `[ClientRpc] DestroyOrbVisualClientRpc(ulong id)`
  - 비주얼 프록시는 NetworkBehaviour가 아니므로 직접 RPC를 보낼 수 없다
- [ ] DropManager 구현 (서버 전용, XP 오브 드랍 확률 처리)
- [ ] **Object Visibility 검증 (Phase 3 완료 기준 포함)**
  - `NetworkObject.CheckObjectVisibility` 오버라이드로 거리 기반 Visibility 직접 구현
  - 50마리 기준 Network Profiler로 대역폭·CPU 측정
  - 기준치 초과 시 가시 범위 축소 또는 동기화 주기 조정
- [ ] Enemy 50개, Projectile 50개 풀 예열

예상 기간: 5~7일

---

### Phase 4. 스킬 시스템

Done when: 자동 스킬이 서버에서 발동·판정되고 FloatingText가 모든 클라이언트에 표시된다.

- [ ] SkillDataSO, SkillLevelData 구현
- [ ] SkillNetworkBase 구현
  - 쿨다운 타이머: 서버(`IsServer`)에서만 실행
  - `[ClientRpc] PlaySkillVFXClientRpc(Vector3 pos)` — 비주얼만 클라이언트
- [ ] AutoTargeting 구현 (서버 전용, Physics.OverlapSphere)
- [ ] SkillManager 구현 (플레이어 컴포넌트, IsServer에서 스킬 발동 결정)
- [ ] ProjectileNetworkSkill 구현 (NetworkObject 투사체, 서버가 이동+충돌 처리)
- [ ] OrbitalNetworkSkill 구현 (서버 충돌, 클라이언트 회전 비주얼)
- [ ] AuraNetworkSkill 구현 (서버 틱 데미지)
- [ ] UltimateSkill 구현 (`[ServerRpc] ActivateUltServerRpc()` — 클라이언트 버튼 → 서버 실행)
- [ ] FloatingText 구현 (`[ClientRpc]`로 데미지 숫자 전달)
- [ ] 기본 스킬 4~6종 데이터 작성

예상 기간: 5~8일

---

### Phase 5. 레벨업과 업그레이드

Done when: 모든 플레이어가 공유 XP로 동시에 레벨업하면, 게임이 일시정지되고 각 클라이언트에 독립적인 선택지 UI가 나타나며, 전원이 선택 완료 후 게임이 재개된다.

#### 공유 XP + 동시 레벨업 흐름

```text
[서버] XP 획득 → SharedXP NetworkVariable 증가
     → 레벨업 조건 달성 시:
         1. SharedLevel 증가
         2. GameState → LevelingUp (NetworkVariable) + 서버 Time.timeScale = 0
         3. 각 클라이언트에 개별 옵션 인덱스 전송 (ShowLevelUpOptionsRpc(int[] optionIndices), SpecificClients)
         4. pendingChoices 집합 초기화 (아직 선택 안 한 플레이어 목록)

[각 클라이언트] GameState.LevelingUp 감지 → Time.timeScale = 0 → 자신의 선택 UI 표시

[클라이언트 선택] SubmitLevelUpChoiceServerRpc(int choiceIndex) 호출

[서버] pendingChoices에서 해당 clientId 제거
     → 모든 플레이어 선택 완료 시:
         1. 각 플레이어에 선택한 업그레이드 적용
         2. GameState → Playing
         → 서버와 모든 클라이언트 Time.timeScale = 1 복구
```

> `LevelingUp` 진입/복귀 시 서버와 클라이언트 모두 `Time.timeScale = 0/1`을 적용한다. 전용 서버 빌드(`UNITY_SERVER`)에서도 적 AI, 물리, 쿨다운, 투사체 갱신에 영향을 주므로 서버에도 적용한다. 또한 서버 gameplay tick(WaveController, SkillManager, EnemyAI, Projectile 이동/충돌 등)은 `GameState.Playing` 상태일 때만 실행되도록 가드해 상태 전환을 명시적으로 보호한다. UI 애니메이션과 입력 대기는 `unscaledDeltaTime`을 사용한다.

#### 구현 항목

- [ ] SharedLevelSystem 구현 (NetworkBehaviour, 서버 전용 로직)
  - `NetworkVariable<float>` SharedXP
  - `NetworkVariable<int>` SharedLevel
  - XP 추가는 서버만 (`IsServer` 체크)
  - `XPOrbManager.TryPickup`은 `PlayerNetworkStats.AddXP`가 아니라 `SharedLevelSystem.AddXP`로 연결
  - 레벨업 조건 달성 시 `GameState → LevelingUp` 전환 + 각 클라이언트 옵션 전송
- [ ] `StageRuntime` 최소 구조 구현 (Phase 7에서 완성 — 여기서는 `NetworkVariable<GameState> CurrentState`와 `SetGameState()` 진입점만 필요. Phase 5~6 코드가 `StageRuntime.Instance`를 참조하므로 먼저 존재해야 함)
- [ ] GameState에 `LevelingUp` 추가 (`NetworkVariable<GameState>` in StageRuntime)
  - 서버 gameplay tick은 `GameState.Playing`일 때만 진행
  - `LevelingUp` 진입: 서버와 모든 클라이언트 `Time.timeScale = 0`
  - `Playing` 복귀: 서버와 모든 클라이언트 `Time.timeScale = 1`
- [ ] LevelUpManager 구현 (서버가 플레이어별 랜덤 옵션 생성)
  - pendingChoices `HashSet<ulong>` 로 선택 완료 추적
  - 클라이언트 이탈 시 `OnClientDisconnected` 콜백에서 해당 `clientId`를 `pendingChoices`에서 제거
  - 이탈 처리 후 `pendingChoices`가 비면 남은 선택 결과를 적용하고 `GameState → Playing` 복귀
  - 전원 완료 → 업그레이드 적용 → GameState → Playing
- [ ] LevelUpUI 구현 (IsLocalPlayer 기준, timeScale=0 중 조작 가능하도록 unscaledTime 사용)
  - `ShowLevelUpOptionsRpc(int[] optionIndices)` (NGO 2.x: `[Rpc(SendTo.SpecificClients)]`, NGO 1.x: `ClientRpcParams`)
  - 수신한 인덱스로 로컬 `UpgradeOptionSO[]` 카탈로그를 조회해 UI 표시
  - 선택 확정 → `SubmitLevelUpChoiceServerRpc(int choiceIndex)`
- [ ] PassiveStatHandler 구현 (플레이어별 스탯 배율, NetworkVariable<float>)
- [ ] UpgradeOptionSO 목록 정의 (스킬 레벨업, 패시브 스탯, 새 스킬 획득)
  - 서버와 클라이언트가 같은 순서의 카탈로그를 사용하도록 고정 인덱스/ID 관리
- [ ] XP 곡선 1차 밸런싱

예상 기간: 3~5일

---

### Phase 6. 아이템 드랍 · 스킬 조합 · 신규 스킬

Done when: 적이 3종 아이템을 드랍하고, 상자 픽업 시 전원에게 스킬 선택 UI가 뜨며, 스킬이 만렙이고 CombineRecipeSO가 존재하면 진화 카드가 슬롯에 등장해 해당 플레이어의 스킬이 조합 스킬로 진화한다.

---

#### 아이템 3종

| 아이템 | 효과 | 처리 방식 |
|---|---|---|
| 상자 (Chest) | **전원** 스킬 업그레이드 선택 창 3개 + 시간 정지 | `ChestOpening` 상태 전환 → 전원 선택 → 조합 조건 검사 → `Playing` 복귀 |
| 체력 (HealthOrb) | 픽업한 플레이어 HP 즉시 회복 | 픽업 즉시 서버가 `PlayerNetworkStats.Heal(value)` |
| 미사일 (Missile) | 서버 기준 큰 반경 내 전체 적에게 AoE 데미지 | 픽업 즉시 서버가 `EnemyRegistry` 또는 `Physics.OverlapSphere`(픽업 위치 기준 큰 반경) → 전 적 `TakeDamage(value)` |

**공통 픽업 흐름** (모두 NetworkObject, 드랍 빈도 낮아 개별 동기화 허용):
```text
서버 드랍 → NetworkObject.Spawn()
클라이언트 범위 진입 → PlayerPickupController.RequestItemPickupServerRpc(ulong networkObjectId)
서버: 거리 검증 → ItemType별 효과 분기 → NetworkObject.Despawn()
```

**상자 픽업 흐름:**
```text
누구든 상자 픽업
  → GameState → ChestOpening
  → Time.timeScale = 0
  → ChestRewardManager: 플레이어별 카드 생성
      [카드 생성 규칙] CombineSystem.GetEvolutionCards(player) 먼저 실행
        → 만렙 + CombineRecipeSO 존재 → 진화 카드 슬롯 선배치
        → 남은 슬롯 → 일반 업그레이드 카탈로그에서 랜덤 채움
      → 전체 플레이어에게 카드 인덱스 RPC 전송
  → 전원 선택 완료 (pendingChoices 소진 + 이탈자 자동 제거)
      → 진화 카드 선택 시: SkillManager.EvolveSkill(source, evolved)
      → 일반 카드 선택 시: SkillManager.UpgradeSkill(skill)
  → SharedLevelSystem.CheckLevelUp() 재검사   ← 선택 중 누적된 XP 처리
  → GameState → Playing
```

> **ChestOpening은 LevelingUp과 별도 GameState로 확정.** 상태 의미가 달라 디버깅·로그 구분이 편하고, `StageRuntime.OnGameStateChanged`와 각 tick 가드에서 두 상태를 명확히 처리할 수 있다. `GameState` enum 추가는 이 Phase(6)의 구현 항목에 포함됨.

> **ChestRewardManager는 LevelUpManager와 별도로 구현.** LevelUpManager는 공유 XP 레벨업 책임이 이미 있어 상자 흐름을 합치면 책임이 커진다. 구조가 거의 동일하므로 복사 후 분리하는 것이 유지보수에 유리하다.

---

#### 스킬 조합 시스템

**핵심 규칙**: 조합 조건은 단 하나 — **해당 플레이어가 보유한 스킬이 만렙(`level >= maxLevel`)이고 유효한 `CombineRecipeSO`가 존재**하면 상자 UI의 카드 슬롯에 진화 카드로 출력된다.

```text
CombineRecipeSO
  sourceSkill  : SkillDataSO   ← 진화 전 스킬
  evolvedSkill : SkillDataSO   ← 진화 후 스킬 (기존 스킬 대체)
```

**상자 카드 생성 규칙** (서버, 플레이어별):
```text
1. 해당 플레이어의 OwnedSkill 순회
     → level >= maxLevel && CombineRecipeSO 있음 → 진화 카드 후보에 추가
2. 진화 카드 후보를 먼저 슬롯에 배치 (최대 3개)
3. 남은 슬롯은 일반 업그레이드 카탈로그(UpgradeOptionSO)에서 랜덤으로 채움
4. 총 3장을 클라이언트에 전송
```

예시:
```
보유: 기본 투사체 Lv3(만렙), 오라 Lv2, 궤도체 Lv1
레시피: 기본 투사체 Lv3 → 관통 폭발탄

카드 슬롯:
  슬롯 1 → 관통 폭발탄(진화)   ← 만렙 + 레시피 자동 배치
  슬롯 2 → 오라 레벨업          ← 일반 업그레이드
  슬롯 3 → 이동속도 +1          ← 일반 업그레이드
```

**진화 선택 시 흐름:**
```text
플레이어가 진화 카드 선택 → SubmitChestChoiceServerRpc(choiceIndex)
  → ChestRewardManager: 해당 선택이 진화 카드임을 확인
  → SkillManager.EvolveSkill(sourceSkill, evolvedSkill)
      → sourceSkill OwnedSkill 제거
      → evolvedSkill OwnedSkill 추가 (level=1)
  → ShowEvolutionNoticeClientRpc(clientId)   ← 해당 플레이어에게 진화 연출 알림
```

> 진화 카드는 선택 강제가 아니다. 플레이어가 원하면 다른 일반 업그레이드 카드를 골라 진화를 미룰 수 있다.

---

#### 신규 스킬 3종

**새 SkillCastType 추가**: `Grenade`, `ScatterShot`, `Melee`

| 스킬 | 타입 | 특성 |
|---|---|---|
| 수류탄 (Grenade) | `SkillCastType.Grenade` | 포물선 투척, 착지 시 스플래시 데미지. 투척 범위 내 랜덤 착탄 지점 |
| 산탄 (ScatterShot) | `SkillCastType.ScatterShot` | 부채꼴 무작위 방향, 공격속도 빠름·대미지 약함. duration + cooldown 있음 |
| 망치 (Hammer) | `SkillCastType.Melee` | 전방 근접 스플래시. 공격속도 느림·대미지 높음. `LastNonZeroMoveDirection` 기준 판정 |

**SkillLevelData 신규 필드:**
```text
// Grenade
float grenadeRange        // 착탄 가능 반경
float grenadeArcHeight    // 포물선 높이
float splashRadius        // 착지 스플래시 반경

// ScatterShot
int   scatterBulletCount  // 한 번에 발사 수
float scatterAngle        // 부채꼴 각도 (랜덤 분산)
float burstDuration       // 지속 발사 시간

// Melee
float meleeArcAngle       // 전방 판정 각도 (OverlapBox 또는 OverlapSphere)
float meleeRange          // 판정 거리
```

> **망치 주의**: 멈춘 직후 `MoveInput`이 `Vector2.zero`가 되면 방향을 잃는다. `PlayerNetworkController`에 `LastNonZeroMoveDirection` 서버 상태를 추가해 망치 판정에 사용한다. 기본값은 `Vector3.forward`.

---

#### 구현 항목

- [ ] `GameState` enum에 `ChestOpening` 추가 (Phase 7보다 먼저 추가)
  - `StageRuntime.OnGameStateChanged`: `ChestOpening` 진입/복귀 시 `Time.timeScale = 0/1`
  - 서버 gameplay tick 가드: `EnemyAI`, `SkillManager`, `NetworkProjectile` — `ChestOpening`도 차단
- [ ] ItemDataSO 구현 (`ItemType` enum: Chest / HealthOrb / Missile, `float value`)
- [ ] DropTableSO 구현 (아이템별 드랍 확률 테이블)
  - `EnemyDataSO.dropTable` 필드에 연결 — 적 사망 시 `DropManager`가 해당 적의 `dropTable`을 참조해 드랍 결정
- [ ] NetworkedItemPickup 구현 (NetworkObject)
  - Chest: `ChestRewardManager.BeginChestReward()`
  - HealthOrb: 픽업한 플레이어에게 `Heal(value)`
  - Missile: `EnemyRegistry` 전체 또는 픽업 위치 기준 `Physics.OverlapSphere` 큰 반경 → `TakeDamage(value)`
- [ ] DropManager에 아이템 드랍 연결 (`EnemyNetworkBase.HandleDeath` 경유)
- [ ] ChestRewardManager 구현 (LevelUpManager와 별도, 구조 동일)
  - `pendingChoices HashSet<ulong>` + `OnClientDisconnected` 이탈 처리
  - **카드 직렬화**: `int[]`만으로는 일반 업그레이드 카탈로그 인덱스와 진화 레시피 인덱스를 구분할 수 없다. `ChestChoiceData { ChestChoiceType type; int index; }` 구조체를 정의해 NGO로 직렬화하고 RPC에 `ChestChoiceData[]`를 전달한다. (`ChestChoiceType`: `UpgradeOption` / `Evolution`)
  - **카드 생성 시**: `CombineSystem.GetEvolutionCards(player)` → 만렙+레시피 진화 카드 선배치 → 빈 슬롯은 일반 업그레이드
  - **선택 수신 시**: `ChestChoiceData.type`에 따라 → 진화: `SkillManager.EvolveSkill()`, 일반: `SkillManager.UpgradeSkill()`
  - 전원 완료 후 → `SharedLevelSystem.CheckLevelUp()` → `GameState → Playing`
- [ ] CombineRecipeSO 구현 (`sourceSkill` + `evolvedSkill` 두 필드만)
- [ ] CombineSystem 구현 (서버 전용)
  - `GetEvolutionCards(player)`: OwnedSkill 순회 → `level >= maxLevel` + `CombineRecipeSO` 존재 → 진화 카드 목록 반환 (카드 생성 전에 호출)
  - `ShowEvolutionNoticeClientRpc` (해당 클라이언트에만, 진화 연출용)
- [ ] `SkillManager.EvolveSkill(sourceSkill, evolvedSkill)` 추가
- [ ] `PlayerNetworkController`에 `LastNonZeroMoveDirection` 서버 상태 추가
- [ ] `SkillCastType`에 `Grenade`, `ScatterShot`, `Melee` 추가
- [ ] `SkillLevelData`에 신규 필드 추가
- [ ] GrenadeNetworkSkill 구현 (서버 포물선 계산 + 착지 스플래시)
- [ ] ScatterShotNetworkSkill 구현 (랜덤 부채꼴 발사, duration/cooldown)
- [ ] MeleeNetworkSkill 구현 (`LastNonZeroMoveDirection` 기반 OverlapBox)
- [ ] 아이템 ScriptableObject 3종 작성
- [ ] 조합 레시피 3~4종 작성

예상 기간: 5~8일

---

### Phase 7. 스테이지와 보스 (데이터 테이블 기반)

Done when: Stage_01에서 5분 생존 후 보스가 등장하고, 보스 처치/전멸 결과가 전원에게 동기화된다.

---

#### 데이터 테이블 설계

"ScriptableObject 1개 = 데이터 1개" 방식 대신 **DataTableSO 패턴**을 사용한다.
테이블 1개 에셋이 전체 행(row)을 보관하며, 인덱스 또는 ID로 조회한다. UE4 DataTable과 동일한 발상.

```csharp
// 공통 제네릭 베이스 — Assets/Scripts/Data/DataTableSO.cs
public abstract class DataTableSO<TRow> : ScriptableObject
    where TRow : struct
{
    [SerializeField] private List<TRow> rows = new();

    public IReadOnlyList<TRow> Rows       => rows;
    public int                 Count      => rows.Count;
    public TRow                this[int i] => rows[i];

    public bool TryGet(int index, out TRow row)
    {
        if (index < 0 || index >= rows.Count) { row = default; return false; }
        row = rows[index];
        return true;
    }
}
```

---

#### 테이블 스키마

**StageTableSO** — `Assets/Data/Stages/StageTable.asset`

| 필드 | 타입 | 설명 |
|---|---|---|
| `stageId` | `int` | 고유 ID (1부터) |
| `stageName` | `string` | "Stage 01 — Survival" |
| `durationSeconds` | `float` | 생존 목표 시간 (기본 300초) |
| `waveGroupId` | `int` | WaveTableSO에서 이 ID와 일치하는 행들을 시퀀스로 사용 |
| `bossData` | `EnemyDataSO` | 보스 스폰 데이터 (null이면 보스 없음) |
| `clearCondition` | `StageClearCondition` | `TimeSurvival` / `BossKill` / `BothRequired` |

**WaveTableSO** — `Assets/Data/Stages/WaveTable.asset`

| 필드 | 타입 | 설명 |
|---|---|---|
| `waveGroupId` | `int` | StageRow.waveGroupId와 매핑 |
| `sequenceIndex` | `int` | 웨이브 순서 (0부터, 오름차순) |
| `entries` | `WaveEntryData[]` | 적 종류 + 수 + 스폰 간격 (spawnActionName이 비어 있을 때 사용) |
| `waveDuration` | `float` | 이 웨이브 종료 후 다음까지 대기 시간(초) |
| `loopFromHere` | `bool` | 이 행 이후 루프 시작점 여부 |
| `spawnActionName` | `string` | 호출할 커스텀 스폰 함수 이름. 비어 있으면 entries 기반 기본 스폰 실행 |

```csharp
[Serializable]
public struct WaveRow
{
    public int             waveGroupId;
    public int             sequenceIndex;
    public WaveEntryData[] entries;
    public float           waveDuration;
    public bool            loopFromHere;
    public string          spawnActionName; // 예: "SpawnEliteRing", "SpawnBossMinions"
}
```

---

#### Named Spawn Action 패턴

`spawnActionName`이 지정된 웨이브는 `entries` 대신 이름으로 등록된 커스텀 함수를 실행한다.

**WaveSpawnActionRegistry** — `WaveController`가 소유하는 딕셔너리 레지스트리

```csharp
// WaveController 내부
private readonly Dictionary<string, Func<WaveRow, IEnumerator>> spawnActions = new();

private void RegisterSpawnActions()
{
    spawnActions["SpawnEliteRing"]    = SpawnEliteRing;
    spawnActions["SpawnBossMinions"]  = SpawnBossMinions;
    spawnActions["SpawnAmbush"]       = SpawnAmbush;
    // 새 스폰 패턴 추가 시 이곳에만 등록
}
```

**WaveController 디스패치 흐름**

```csharp
private IEnumerator ExecuteWave(WaveRow wave)
{
    if (!string.IsNullOrEmpty(wave.spawnActionName) &&
        spawnActions.TryGetValue(wave.spawnActionName, out var action))
    {
        yield return StartCoroutine(action(wave));   // 커스텀 스폰
    }
    else
    {
        yield return StartCoroutine(DefaultSpawnWave(wave));  // entries 기반 기본 스폰
    }
    yield return new WaitForSeconds(wave.waveDuration);
}
```

**커스텀 스폰 함수 예시**

```csharp
// 원형 포위 — 8방향에서 엘리트 동시 스폰
private IEnumerator SpawnEliteRing(WaveRow wave)
{
    if (wave.entries.Length == 0) yield break;
    var entry = wave.entries[0];
    int count = Mathf.Max(1, entry.count);
    for (int i = 0; i < count; i++)
    {
        float angle = i * (360f / count) * Mathf.Deg2Rad;
        Vector3 pos = GetCenterPosition() +
                      new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
        spawnManager.SpawnEnemy(entry.enemyData, pos, hpMul, dmgMul);
    }
}

// 보스 미니언 — 보스 HP 50% 이하 트리거로 호출
private IEnumerator SpawnBossMinions(WaveRow wave) { ... }
```

**등록 규칙:**
- 함수 이름은 `PascalCase` + `Spawn` 접두사 (`SpawnEliteRing`, `SpawnAmbush`)
- 시그니처 고정: `IEnumerator FuncName(WaveRow wave)`
- 코드에 없는 이름을 테이블에 입력하면 기본 스폰으로 폴백 (LogWarning 출력)

```csharp
// 미등록 이름 경고
if (!string.IsNullOrEmpty(wave.spawnActionName) &&
    !spawnActions.ContainsKey(wave.spawnActionName))
{
    Debug.LogWarning($"[WaveController] 미등록 spawnActionName='{wave.spawnActionName}' → 기본 스폰 실행");
}
```

**EnemyScalingTableSO** — `Assets/Data/Stages/EnemyScalingTable.asset`

시간 경과에 따른 난이도 배율을 인라인 수식 대신 테이블로 관리. `WaveController`에서 경과 시간 기준으로 가장 가까운 행을 조회한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `timeMinutes` | `float` | 이 행이 적용되는 시작 분 |
| `hpMultiplier` | `float` | 기준 HP 배율 |
| `damageMultiplier` | `float` | 기준 공격력 배율 |
| `spawnRateMultiplier` | `float` | 스폰 속도 배율 |

```text
예시 행:
 0분:  HP ×1.00 / Dmg ×1.00 / Rate ×1.00
 2분:  HP ×1.30 / Dmg ×1.20 / Rate ×1.40
 5분:  HP ×1.75 / Dmg ×1.50 / Rate ×2.00
10분:  HP ×2.50 / Dmg ×2.00 / Rate ×3.00
```

→ §8의 인라인 공식(`1 + tMin * 0.15f` 등)을 이 테이블로 대체.  
  디자이너가 코드 수정 없이 Inspector에서 직접 수치 조정 가능.

**Co-op 배율은 테이블 분리 없이 코드에서 플레이어 수 기반으로 곱한다** (플레이어 수는 런타임 값이라 테이블화 불필요).

---

#### 조회 유틸리티

```csharp
// WaveController 내부에서 사용
private ScalingRow GetCurrentScaling(EnemyScalingTableSO table, float elapsedSeconds)
{
    float elapsedMinutes = elapsedSeconds / 60f;
    ScalingRow result = table[0];
    for (int i = 0; i < table.Count; i++)
    {
        if (table[i].timeMinutes <= elapsedMinutes)
            result = table[i];
        else
            break;
    }
    return result;
}
```

---

#### 구현 항목

**데이터 테이블 인프라**
- [ ] `DataTableSO<TRow>` 제네릭 베이스 구현 (`Assets/Scripts/Data/DataTableSO.cs`)
- [ ] `StageTableSO` + `StageRow` 구조체 구현
- [ ] `WaveTableSO` + `WaveRow` 구조체 구현 (기존 `WaveDataSO` 대체)
- [ ] `EnemyScalingTableSO` + `ScalingRow` 구조체 구현
- [ ] `StageClearCondition` enum 추가 (`TimeSurvival`, `BossKill`, `BothRequired`)

**WaveController 리팩터링**
- [ ] `WaveDataSO` 폐기 — `WaveDataSO[]` 직접 참조를 `WaveTableSO` + `StageTableSO` 참조로 교체 (Phase 3에서 임시 사용한 구조를 이 단계에서 완전히 제거)
- [ ] 인라인 난이도 공식 → `EnemyScalingTableSO.GetCurrentScaling()` 조회로 교체
- [ ] `waveGroupId` 기반으로 해당 스테이지 웨이브 행만 필터링해 시퀀스 실행
- [ ] `RegisterSpawnActions()` 구현 (딕셔너리 등록)
- [ ] `ExecuteWave()` 디스패치 로직 구현 (`spawnActionName` → 커스텀 / 기본 폴백)
- [ ] 커스텀 스폰 함수 1~2종 구현 (`SpawnEliteRing` 등)

**데이터 에셋 작성**
- [ ] `StageTable.asset` — Stage_01 행 1개 작성
- [ ] `WaveTable.asset` — Stage_01용 웨이브 4~6행 작성 (loopLastWave 행 포함)
- [ ] `EnemyScalingTable.asset` — 0 / 2 / 5 / 10 / 15 / 20분 기준 6행 작성

**스테이지 런타임**
- [ ] `StageRuntime`에 생존 타이머 추가 (서버 전용, `ElapsedTime` NetworkVariable 동기화)
- [ ] `GameState` enum에 `BossPhase`, `Clear`, `GameOver` 추가
- [ ] 생존 타이머가 `StageRow.durationSeconds` 도달 → `BossPhase` 전환 + 보스 스폰
- [ ] `StageRuntime.LoadStage(int stageId)` — `StageTableSO`에서 행 조회 후 WaveController에 전달

**보스**
- [ ] `BossNetworkBase : EnemyNetworkBase` 구현 (고유 페이즈 전환 로직)
- [ ] 보스 HP `NetworkVariable<float>` → 전체 클라이언트 HUD 동기화 (`BossHealthBar`)
- [ ] 보스 처치 → `GameState.Clear`
- [ ] 전원 동시 다운 → `GameState.GameOver` (영구 사망 없음. 다운 타이머 만료 시 자동 부활. `BeginDowned()` 직후 `CheckGameOver()` 호출 — `IsDowned == true`인 플레이어가 전원이면 즉시 GameOver. 다운 중이 아닌 플레이어가 한 명이라도 있으면 GameOver 아님)

**결과 화면 (최소)**
- [ ] 승리/패배 `[ClientRpc]` 동기화
- [ ] 결과 UI (Phase 8에서 정식화, 여기서는 텍스트 표시만)

**Phase 7 정리**
- [x] Phase 7 임시 Editor 세팅 스크립트 제거 (`SetupPhase7Assets`, `SetupBossMissilePrefab`, `SetupBossAnimator`)
- [ ] 스테이지 CSV 원본 정책 확정
  - 런타임 로드는 `Assets/Resources/Data/StageTable.csv` 기준
  - 편집 원본은 `Assets/Data/Stages/StageTable.csv`로 유지하되, 변경 시 Resources CSV와 동기화 필요
  - 장기적으로는 CSV 이중 관리를 줄이기 위해 DataTableSO 또는 단일 Import 흐름으로 통합

예상 기간: 6~9일

---

### Phase 8. UI, 이펙트, 오디오, 최적화, 밸런스

Done when: HUD/다운·부활/결과/메인 메뉴 UI가 이벤트 기반으로 동작하고, 이펙트·오디오·최적화·밸런스가 붙어 4인 루프가 에러 없이 완성된다.

완료 검증:
- 2인 이상에서 HUD, 레벨업, 상자, 보스, 부활, 결과 UI가 중복 표시 없이 동작
- 씬 전환, 재시작, 호스트 재시작 후 이벤트가 중복 수신되지 않음
- 다운/완전 사망 플레이어의 UI 상태와 선택 가능 여부가 정상 반영됨
- 보스 등장/처치, 상자 오픈, 레벨업, 결과 화면이 이벤트 기반으로 갱신됨
- 콘솔에 `MissingReferenceException`, `NullReferenceException`, 이벤트 중복 구독 경고 없음

#### Phase 8.0 이벤트 기반 구조 리팩터링

**이벤트 채널 혼용 전략 (확정)**

| 채널 | 용도 | 특성 |
|---|---|---|
| `UIEventHub` (MonoBehaviour, DontDestroyOnLoad) | UI 상태 이벤트 — HP·레벨·보스 HP·GameFlow 등 ViewModel이 구조화된 payload를 소비하는 상태 변화 | 코드 구독, 타입 안전, payload 필수 |
| `GameEventSO` (ScriptableObject 채널) | 연출 트리거 — VFX·SFX·카메라 쉐이크처럼 Inspector 연결 기반 fire-and-forget | 에디터 연결, payload 없거나 최소화, 여러 리스너 독립 구독 |
| 기존 `static event` | Phase 8 마이그레이션 임시 경유지 — 기존 구독자 보호용. **새 기능에는 static event 추가 금지** | |

**경계 규칙**: 하나의 게임 사건이 UI 갱신 + 연출을 동시에 유발할 때, UIEventHub와 GameEventSO를 각각 발행한다.
예) 보스 사망 → `StageUIEvents.BossStatusChanged(isVisible=false)` (UIEventHub) + `BossDeathEvent` (GameEventSO).

- [ ] 기존 직접 참조/정적 이벤트/Manager 직접 구독 경로 목록화
- [ ] 참조 허용 경계 확정
  - UI View는 자신의 자식 UI 오브젝트(`Text`, `Image`, `Button`, `Slider`, 슬롯 프리팹 등)를 `[SerializeField]`로 직접 참조해도 된다.
  - UI View는 Manager, NetworkObject, 다른 플레이어, 다른 시스템 상태를 직접 찾거나 구독하지 않는다.
  - UI가 아닌 시스템 간 통신(플레이어 간 상태 전달, 스킬/아이템/스테이지/보스 상태 전파, VFX 트리거)은 이벤트 허브 또는 명시적 이벤트 채널을 기본 규칙으로 한다.
- [ ] 이벤트 남발 금지 기준 확정
  - 같은 컴포넌트 내부, 같은 View의 부모-자식 UI, 단순 렌더링 참조는 직접 참조한다.
  - 소유 경계를 넘는 상태 전달, 여러 시스템이 관찰해야 하는 상태 변화, 씬 전환 후에도 구조가 바뀔 수 있는 참조만 이벤트화한다.
- [ ] 이벤트 허브 범위 분리
  - `PlayerUIEvents`: 플레이어 HP, 다운/부활, 팀원 상태
  - `StageUIEvents`: 스테이지 시간, 보스 페이즈, 보스 HP, 결과 상태
  - `SkillUIEvents`: 스킬 슬롯, 수동 스킬/궁극기 쿨다운
  - `RewardUIEvents`: 레벨업 카드, 상자 카드, 획득 로그
  - `FlowUIEvents`: 씬 로딩, 메뉴 전환, 네트워크 연결 상태
- [ ] 이벤트 payload 정의
  - `PlayerStatusChanged`: `clientId`, `displayName`, `hp`, `maxHp`, `isDowned`, `isAlive`, `downedTimeRemaining`
  - `SharedLevelChanged`: `level`, `xp`, `xpRequired`, `normalizedXp`
  - `SkillSlotsChanged`: `names`, `levels`
  - `UltimateCooldownChanged`: `remaining`, `duration`, `isReady`
  - `BossStatusChanged`: `isVisible`, `hp`, `maxHp`, `normalizedHp`
  - `StageTimerChanged`: `elapsedSeconds`, `bossTimeSeconds`, `remainingToBossSeconds`, `isBossPhase`
  - `GameFlowChanged`: `previous`, `next`
  - `RewardOptionsReceived`: `rewardKind`, `optionIndices` 또는 `choices`, `currentLevels`
  - `AcquisitionLogRequested`: `message`, `icon`, `color`, `duration`
- [ ] 이벤트 허브 수명 주기 확정
  - 전역 이벤트 허브는 Bootstrap의 `DontDestroyOnLoad` 오브젝트에서 1회 생성한다.
  - 스테이지 전용 이벤트/Adapter/Binder는 Stage 씬 진입 시 생성하고 씬 이탈 시 반드시 해제한다.
  - 정적 이벤트는 씬 전환 전 `Clear()` 또는 명시적 `Reset` 경로를 제공해 이전 구독자를 제거한다.
  - ViewModel, Binder, Adapter는 `Dispose`/`Unbind`를 구현하고 `OnDisable`/`OnDestroy`에서 반드시 호출한다.
  - 구독 메서드와 해제 메서드는 한 클래스 안에서 쌍으로 배치한다.
  - 같은 인스턴스가 중복 구독되지 않도록 Bind 전에 Unbind를 먼저 호출하거나 `_isBound` 가드를 둔다.
- [ ] 런타임 상태 → 이벤트 발행 Adapter/Binder 계층 추가
- [ ] 레벨업/상자/부활/스킬 동기화 정적 이벤트를 이벤트 허브 또는 명시적 채널로 래핑
- [ ] 기존 UI의 Manager/NetworkObject 직접 참조 제거. 단, 같은 View 하위의 UI 자식 객체 직접 참조는 허용
- [ ] VFX는 게임 로직 직접 참조 없이 이벤트 수신 후 로컬 재생하도록 기준 확정
- [ ] 이벤트 구독 해제 규칙 통일 (`OnEnable/OnDisable`, `Dispose`, `Unbind`)
- [ ] 기존 이벤트 마이그레이션 표 기준으로 교체
  - `LevelUpManager.OnOptionsReceived` → `RewardUIEvents.LevelUpOptionsReceived`
  - `LevelUpManager.OnLevelUpCompleted` → `RewardUIEvents.LevelUpCompleted`
  - `ChestRewardManager.OnOptionsReceived` → `RewardUIEvents.ChestOptionsReceived`
  - `ChestRewardManager.OnChestRewardCompleted` → `RewardUIEvents.ChestRewardCompleted`
  - `SkillManager.OnSkillsSynced` → `SkillUIEvents.SkillSlotsChanged`
  - `PlayerReviveHandler.OnReviveProgressUpdated` → `PlayerUIEvents.ReviveProgressChanged`
  - `PlayerReviveHandler.OnRevived` → `PlayerUIEvents.PlayerRevived`
- [ ] 리팩터링 순서
  - 이벤트 payload 구조체 정의
  - `UIEventHub` 또는 이벤트 채널 구현
  - `StageResultUI`
  - `LevelUpUI`
  - `ChestRewardUI`
  - `SkillManager.OnSkillsSynced`
  - `PlayerReviveHandler`
  - 신규 HUD MVVM 구현
- [ ] 2인 이상 플레이에서 레벨업, 상자, 보스, 부활 이벤트가 중복/누락 없이 동작하는지 검증

#### Phase 8.1 MVVM UI 기반

- [ ] UI MVVM 기본 구조 확정
  - View: 자신의 자식 Unity UI 컴포넌트 참조와 렌더링만 담당 (`HUDView`, `CoopStatusView`, `ResultView`)
  - ViewModel: 표시용 상태, 포맷팅, UI 이벤트 변환 담당 (`HUDViewModel`, `PlayerStatusViewModel`)
  - Model/Source: `PlayerNetworkStats`, `SharedLevelSystem`, `SkillManager`, `GameFlowCoordinator` 등 기존 런타임 상태
  - View는 `NetworkVariable`이나 Manager를 직접 구독하지 않고 ViewModel 이벤트에만 바인딩
  - ViewModel은 런타임 Source를 직접 찾지 않고 `UIEventHub` 또는 이벤트 채널을 통해 상태 변경을 수신
  - ViewModel은 `Dispose`/`Unbind`로 이벤트 구독을 반드시 해제
  - 버튼 입력은 View → ViewModel → 기존 Manager/RPC 호출 순서로 전달
- [ ] UI Prefab 규칙 확정
  - View 스크립트는 UI 프리팹/패널 루트에만 둔다.
  - 하위 자식은 표시용 컴포넌트와 버튼/슬롯 단위 View만 둔다.
  - View는 Inspector에 연결된 자식 컴포넌트만 렌더링하고, 씬 탐색(`FindObjectOfType`, 태그 검색 등)을 하지 않는다.
  - 버튼 이벤트는 View에서 수신하고 ViewModel 명령 메서드로 전달한다.
- [ ] UI 이벤트 기반 참조 구조 구현
  - `PlayerNetworkStats` 변경 → `PlayerStatusChanged` 이벤트 발행
  - `SharedLevelSystem` 변경 → `SharedLevelChanged` 이벤트 발행
  - `SkillManager.OnSkillsSynced` → `SkillSlotsChanged` 이벤트로 변환
  - `GameFlowCoordinator.CurrentFlow` 변경 → `GameFlowChanged` 이벤트 발행
  - View/ViewModel은 씬 오브젝트 직접 탐색 대신 이벤트 구독으로 상태 수신

#### Phase 8.2 HUD/전투 UI ✅

- [x] HUD MVVM 1차 구현
  - 로컬 플레이어 탐색은 전용 Binder/Adapter에서만 수행
  - `PlayerNetworkStats` 변경 이벤트를 HUDViewModel 상태로 변환
  - HP/MaxHP fill, 수치 텍스트 갱신 (바 위에 텍스트 겹침)
  - `SharedLevelChanged` 이벤트를 HUDViewModel 상태로 변환
  - XP fill, 레벨 텍스트 갱신 (바 위에 텍스트 겹침)
  - `SkillSlotsChanged` 이벤트 기반 스킬 슬롯 이름/레벨 표시
  - 로컬 플레이어 교체/씬 전환 시 재바인딩
- [x] 보스 페이즈 타이머 UI 구현 (보스 등장 전: 경과 시간 MM:SS, 보스 페이즈 중: BOSS 표시)
- [x] 보스 HP 바 구현 — 세그먼트형 (MapleStory/Lost Ark 스타일)
  - 화면 최상단 풀 폭 배치, 플레이어 HP UI와 겹침 없음
  - 10덩어리 세그먼트: 한 덩어리 소진 시 바 리셋 + Gradient 색 변경
  - Ghost bar: fill 뒤로 다음 덩어리 색 미리 표시
  - 패널 우측 "보스명  ×N" 텍스트 + 상단 pip 카운터
  - BossStatusAdapter를 Enemy D.prefab(실제 보스)에 올바르게 배치
- [x] SkillHUDUI / SkillHUDCellUI 구현 완료. ItemSlotUI 불필요 — 아이템 전량 1회용 즉시효과
- [x] 궁극기 타이머 UI 구현 (UltimateCooldownUI — Radial360 fill, 키힌트, readyGlow, 하단 중앙 배치)
- [x] 획득 로그 UI 구현 (AcquisitionLogUI — fire-and-forget, 좌측 스택, fadein/out)

#### Phase 8.3 Co-op/다운/부활 UI ✅

- [x] Co-op HUD 추가 (팀원 HP 미니 표시)
- [x] 팀원 다운 표시 — 팀원이 `IsDowned=true`일 때 Co-op HUD 해당 슬롯에 아이콘/색상 강조 (죽은 것처럼 보이지 않도록 구분)
- [x] 다운 상태 HUD — 자신이 다운됐을 때 `DownedTimeRemaining` 타이머 표시 (자력 부활 불가 안내 포함)
- [x] 부활 진행도 바 — 팀원이 범위 안에 들어왔을 때 화면에 진행도 표시 (`PlayerReviveHandler.OnReviveProgressUpdated` 이벤트 구독)

#### Phase 8.4 메뉴/결과 UI

---

##### Phase 8.4a 결과 통계 / Result UI

`Clear` / `GameOver` 진입 시 플레이어별 전투 결과를 서버 권한으로 확정하고, 모든 클라이언트에 같은 결과 화면을 표시한다.

결과 통계는 네트워크 실시간 상태인 `PlayerNetworkStats`와 분리한다. `PlayerNetworkStats`는 HP, 다운, 행동 가능 여부처럼 전투 중 계속 변하는 값만 맡고, `PlayerMatchStats`는 한 판이 끝났을 때 보여줄 누적 통계만 맡는다.

##### PlayerMatchStats — 처치수 / 데미지 / 생존 시간

개인 전투 통계를 서버 권한으로 누적하는 전담 컴포넌트. `PlayerNetworkStats`(실시간 HP·상태)와 역할 분리.

**컴포넌트 배치**: `NetworkedPlayer` 프리팹에 `PlayerMatchStats : NetworkBehaviour` 추가.

```csharp
public class PlayerMatchStats : NetworkBehaviour
{
    // Owner만 읽으면 되므로 ReadPermission = Owner
    public NetworkVariable<int>   KillCount     = new(0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> TotalDamage   = new(0f, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SurvivalTime  = new(0f, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public NetworkVariable<int>   Level         = new(1, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    // 서버에서 외부 호출
    public void AddKill()                     { if (IsServer) KillCount.Value++; }
    public void AddDamage(float amount)       { if (IsServer) TotalDamage.Value += amount; }
    public void SetSurvivalTime(float time)   { if (IsServer) SurvivalTime.Value = time; }
    public void SetLevel(int level)           { if (IsServer) Level.Value = level; }
}
```

**처치·데미지 귀속 — `EnemyNetworkBase.TakeDamage` 서명 변경**

```csharp
// Before
public void TakeDamage(float amount)

// After
public void TakeDamage(float amount, ulong attackerClientId = ulong.MaxValue, int sourceSkillId = -1)
```

귀속 규칙:
- **플레이어 귀속**: `attackerClientId = OwnerClientId` 전달 → `AddDamage` / kill 시 `AddKill` 호출
- **비귀속**: `attackerClientId = ulong.MaxValue` (환경 데미지, 자폭, 디버그 커맨드) → 통계 누적 없음
- **스킬별 귀속**: `sourceSkillId`는 가능하면 카탈로그 인덱스/고정 ID를 사용한다. 문자열 이름은 UI 표시용으로만 로컬 카탈로그에서 조회한다.

`attackerClientId` 전달이 필요한 전체 호출처:

| 클래스 | 방식 |
|---|---|
| `NetworkProjectile` | 생성 시 `attackerClientId` 필드 저장 |
| `OrbitingProjectileMode` | 생성 시 저장 |
| `AuraNetworkSkill` | 틱마다 `ownerClientId` 전달 |
| `BlackHoleNetworkSkill` | 틱마다 `ownerClientId` 전달 |
| `ClusterGrenadeNetworkSkill` | 생성 시 저장 |
| `GrenadeNetworkSkill` | 생성 시 저장 |
| `MeleeNetworkSkill` | 틱마다 `ownerClientId` 전달 |
| `OrbitalNetworkSkill` | 틱마다 `ownerClientId` 전달 |
| `ChestRewardApplier` | `ulong.MaxValue` (아이템 비귀속) |
| `DebugEnemyCommands` | `ulong.MaxValue` (디버그 비귀속) |

**처치 판정 — `lastAttackerClientId` 방식**

- `EnemyNetworkBase`에 `lastAttackerClientId`, `lastSourceSkillId` 필드 유지 (플레이어 귀속 데미지 입을 때마다 갱신)
- HP=0 시 `lastAttackerClientId`로 kill 귀속
- 도트/오라/블랙홀처럼 지속 피해도 소유자 유지되므로 자연스럽게 처리
- 자폭·환경 데미지(`ulong.MaxValue`)는 kill 미귀속

**스킬별 데미지 — ID 기반**

- `TakeDamage(amount, attackerId, sourceSkillId)` 서명으로 스킬/아이템 식별자 전달
- `PlayerMatchStats`에 `Dictionary<int, float> DamagePerSource` 누적
- 결과 UI는 `sourceSkillId`를 로컬 `SkillDataSO`/카탈로그에서 이름으로 변환
- MVP에서는 플레이어별 총 데미지만 먼저 표시하고, 스킬별 데미지 표는 2차 작업으로 미뤄도 된다.

**생존 시간 기준**

- 스테이지 종료 시 `CanAct == true`이면 `StageRuntime.ElapsedTime` 그대로 사용
- 최종적으로 `CanAct == false`인 채 Clear/GameOver를 맞은 경우만 "마지막 행동 가능 시각" 사용
  - (다운 후 부활하면 생존 시간이 끊기지 않도록, `BeginDowned` 시각이 아닌 최종 `CanAct=false` 전환 시각 기준)

**통계 수집·전송 — `GameFlowCoordinator`에서 일원화**

실제 종료 상태 전환은 `GameFlowCoordinator.ForceTransition(Clear/GameOver)`가 담당하므로 여기서 한 번만 브로드캐스트해 중복 전송 방지.

```
GameFlowCoordinator.ForceTransition(Clear/GameOver)
  └─ StageResultBroadcaster.Broadcast()   // 서버 전용 헬퍼
       ├─ BuildEntries()                   // 각 플레이어 PlayerMatchStats 수집
       └─ SendMatchResultClientRpc(entries)
```

**결과 payload 직렬화 규칙**

기본 타입 RPC는 NGO 자동 직렬화를 그대로 사용한다. 단, `MatchResultEntry[]`처럼 커스텀 struct 배열을 RPC로 보낼 때는 `ChestChoiceData`와 같은 방식으로 `INetworkSerializable`을 명시한다.

결과 entry 안에 일반 `string`이나 가변 길이 배열을 직접 넣지 않는다. 표시 이름은 `FixedString32Bytes` 같은 고정 길이 문자열로 제한하고, 스킬별 상세 데미지는 별도 payload 또는 후속 RPC로 평탄화해서 보낸다.

```csharp
using Unity.Collections;
using Unity.Netcode;

public struct MatchResultEntry : INetworkSerializable
{
    public ulong              clientId;
    public FixedString32Bytes displayName;
    public int                level;
    public int                kills;
    public float              totalDamage;
    public float              survivalTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref displayName);
        serializer.SerializeValue(ref level);
        serializer.SerializeValue(ref kills);
        serializer.SerializeValue(ref totalDamage);
        serializer.SerializeValue(ref survivalTime);
    }
}

public struct SourceDamageEntry : INetworkSerializable
{
    public ulong clientId;
    public int   sourceSkillId;
    public float damage;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref sourceSkillId);
        serializer.SerializeValue(ref damage);
    }
}
```

**Result UI — 기존 `StageResultUI` 확장**

현재 `StageResultUI`는 Clear/GameOver 텍스트만 보여주는 뼈대가 있으므로 새 UI를 별도 생성하지 않고 확장한다.

```
StageResultViewModel.OnResultReceived(MatchResultEntry[])
StageResultUI
  ├─ Clear / GameOver 헤더
  └─ 플레이어별 행 (row prefab): 이름 / 레벨 / 처치수 / 총 데미지 / 생존 시간
       └─ 펼치면: 스킬별 데미지 기여 목록 (SourceDamageEntry[] 기반)
```

---

##### Phase 8.4a 구현 항목

- [ ] `PlayerMatchStats` NetworkBehaviour 구현 및 `NetworkedPlayer` 프리팹에 추가
  - `KillCount`, `TotalDamage`, `SurvivalTime`, `Level`, `DamagePerSource` 필드
- [ ] `EnemyNetworkBase.TakeDamage(float amount, ulong attackerClientId, int sourceSkillId)` 서명 변경
  - `lastAttackerClientId`, `lastSourceSkillId` 갱신 (비귀속 `ulong.MaxValue` 제외)
  - 데미지 적용 후 공격자 `PlayerMatchStats.AddDamage(actual, sourceSkillId)` 호출
  - HP=0 시 `lastAttackerClientId`로 `AddKill()` 호출
- [ ] 전체 호출처에 `attackerClientId` + `sourceSkillId` 전달 (표 참조: NetworkProjectile / OrbitingProjectileMode / AuraNetworkSkill / BlackHoleNetworkSkill / ClusterGrenadeNetworkSkill / GrenadeNetworkSkill / MeleeNetworkSkill / OrbitalNetworkSkill / ChestRewardApplier / DebugEnemyCommands)
- [ ] `PlayerMatchStats`에 레벨 필드 추가 — `SharedLevelSystem`에서 종료 시 `SetLevel(int)` 호출
- [ ] `StageRuntime`에 "최종 CanAct=false 전환 시각" 기록 로직 추가 (`SetSurvivalTime` 판단 기준)
- [ ] `MatchResultEntry : INetworkSerializable` 구현 (`FixedString32Bytes displayName`)
- [ ] `SourceDamageEntry : INetworkSerializable` 구현 (스킬별 데미지 상세가 필요할 때 사용)
- [ ] `StageResultBroadcaster` 구현 — `GameFlowCoordinator.ForceTransition` 호출 시 `BuildEntries()` + `SendMatchResultClientRpc(entries)` 일원화
- [ ] `StageResultViewModel`에 `OnResultReceived(MatchResultEntry[])` 추가
- [ ] `StageResultUI` 확장 — row prefab 연결, 스킬별 데미지 기여 펼침 표시

##### Phase 8.4b 메뉴 / 로딩 / 설정

결과 통계와 별도 작업 단위로 진행한다. 8.4a 완료 후 독립적으로 다듬는다.

**아키텍처: 전용 서버 (Dedicated Server)**

- 서버는 별도 헤드리스 빌드로 실행 (`-server` 플래그 또는 `UNITY_SERVER`)
- 클라이언트는 서버 IP:PORT를 직접 입력해 접속 (`StartAsClient`)
- Host-Client / Relay 세션 방식 미사용 (`StartAsHost`, `StartAsRelayHost` Obsolete 처리됨)
- "방장"은 먼저 접속한 클라이언트가 담당 (`LobbyHostService`)

**씬 전환 흐름**

```
Bootstrap → MainMenu(접속) → Stage_01(로딩 화면) → 게임 → 결과 화면 → MainMenu
```

---

**① MainMenu**

- 플레이어 이름 입력 (`PlayerPrefs`로 저장/복원)
- 서버 IP:PORT 입력 + 접속 버튼
- 연결 상태 표시 (접속 중 / 실패 / 재시도)
- 서버 모드(`IsServerBuild`)일 때 UI 전체 비활성

**② LobbyUI (접속 후 대기실 — MainMenu 씬 내 패널 전환)**

- 참여 플레이어 슬롯 4개 (이름 + 준비 상태)
- 서버 IP 표시 + 복사 버튼
- 방장(첫 접속 클라이언트)만 "게임 시작" 버튼 활성화 (1~4인 모두 시작 가능)
- 비방장: "대기 중…" 표시
- 접속 해제 버튼

**③ LoadingScreen**

- `NetworkManager.SceneManager.OnSceneEvent` 구독
- 씬 로드 완료 후 NGO 스폰 대기 구간도 커버 (로딩 바 or 스피너)
- 클라이언트 측: 씬 전환 승인 후 자동 표시 → 스폰 완료 시 자동 숨김

**④ 결과 화면 버튼**

- 방장: "재시작" (동일 씬 재로드) / "메인 메뉴" (접속 해제 후 MainMenu 복귀)
- 비방장: "메인 메뉴"
- 버튼 클릭 → `GameFlowCoordinator`를 통해 씬 전환 요청

**⑤ 네트워크 에러 다이얼로그**

- 서버 접속 끊김 / 타임아웃 → 오버레이 다이얼로그 표시
- "확인" 클릭 시 MainMenu로 강제 복귀 (접속 상태 초기화)
- `GameNetworkManager.OnClientDisconnected` 이벤트 수신

**⑥ 설정 UI**

- 음량 슬라이더 (Master / SFX / BGM)
- 해상도 드롭다운 + 전체화면 토글
- 프레임 캡 (60 / 120 / 무제한)
- 설정값은 `PlayerPrefs`로 로컬 저장

**⑦ 개발용 네트워크 오버레이**

- Server / Client 모드 표시
- Ping (RTT ms), 서버 연결 상태
- 씬 이름, 접속 인원수
- `#if DEVELOPMENT_BUILD || UNITY_EDITOR` 조건부 표시

---

##### Phase 8.4b 구현 항목

- [x] MainMenu 구현 (이름 입력 / 서버 IP 접속 / 연결 상태 표시)
- [ ] LobbyUI 구현 (플레이어 슬롯 4개, 서버 IP 표시, 방장 시작 버튼)
- [ ] LoadingScreen 구현 (`SceneManager.OnSceneEvent` 구독, 스폰 대기 커버)
- [ ] 결과 화면 재시작 / 메인 메뉴 복귀 버튼 (`StageResultUI` 확장)
- [ ] 네트워크 에러 다이얼로그 (서버 끊김 / 타임아웃 → MainMenu 강제 복귀)
- [ ] 설정 UI (음량 3채널, 해상도, 프레임 캡, PlayerPrefs 저장)
- [ ] 개발용 네트워크 오버레이 (`DEVELOPMENT_BUILD` 조건부)

#### Phase 8.5 이펙트

- [ ] GameEventSO, EventListener 구현
- [ ] 피격 이펙트 구현 (적/보스 피격 시 짧은 플래시, 히트 스파크, 데미지 텍스트 연동)
- [ ] 사망 이펙트 구현 (적/보스 사망 VFX, 보스 사망 연출)
- [ ] 스킬 이펙트 보강 (투사체, 수류탄, 근접, 오라, 진화 스킬별 식별 가능한 VFX)
- [ ] 아이템/XP 픽업 이펙트 구현 (흡수, 획득, 상자 오픈 피드백)
- [ ] 보스 패턴 이펙트 보강 (텔레그래프, 미사일 발사, BigShot 연출)
- [ ] 카메라 쉐이크 (클라이언트 로컬)

#### Phase 8.6 오디오

- [ ] AudioManager 구현 (후순위, 클라이언트 로컬)
- [ ] SFX 이벤트 채널 정의 (피격, 사망, 스킬, 아이템/XP 획득, 상자 오픈, 보스 패턴)
- [ ] BGM 전환 구현 (메뉴, 일반 스테이지, 보스 페이즈, 결과 화면)
- [ ] 설정 UI 음량 슬라이더와 연동

#### Phase 8.7 최적화/밸런스

- [ ] GPU Instancing 적용 (Enemy 대량 렌더링)
- [ ] Network Profiler + CPU Profiler로 병목 확인
- [ ] Object Visibility 튜닝 (Phase 3 구현 기반, 가시 범위 수치 조정)
- [ ] XP, 스폰, 스킬 수치 Co-op 밸런싱

예상 기간: 1~2주

---

### Phase 9. 로컬 서버 빌드 안정화

Done when: Windows Server Build를 별도 실행해 서버 역할만 담당하고, 원격 친구가 Relay 코드로 접속해 4인 게임이 안정적으로 돌아간다.

- [ ] Windows Server Build 타깃 설정 (`UNITY_SERVER` 심볼 등록)
- [ ] 서버 전용 컴포넌트 스트립 (`#if !UNITY_SERVER` 로 렌더링·오디오·Cinemachine 제외)
- [ ] 서버 시작 시 자동으로 Relay Allocation → 코드 콘솔 출력
- [ ] 서버 실행 배치 파일 작성 (클릭 한 번으로 서버 시작)
- [ ] 서버 로그 파일 출력 (`Application.logMessageReceived` → txt 저장)
- [ ] SaveManager 구현 (로컬 세이브: 설정, 통계)
- [ ] LAN 직접 IP 접속 지원 (같은 네트워크면 Relay 없이 연결)
- [ ] 4인 원격 플레이 안정성 테스트 (30분 생존 스테이지 기준 크래시 없음)
- [ ] 서버 치트 방지 기초 (속도 검증, 데미지 서버 내부 계산 재확인)

예상 기간: 1~2주

---

## 12. 우선순위 로드맵

```text
Phase 0: 프로젝트 세팅 + 패키지
  ↓
Phase 1: 네트워크 파운데이션 (연결, Lobby, Relay)
  ↓
Phase 2: 네트워크 플레이어 (이동, 스탯, 카메라)
  ↓
Phase 3: 네트워크 적 + 스폰 + XP
  ↓
Phase 4: 스킬 시스템 (서버 판정, 클라이언트 VFX)
  ↓
Phase 5: 레벨업 & 업그레이드 (공유 XP / 동시 선택)
  ↓
Phase 6: 아이템 & 조합
  ↓
Phase 7: 스테이지 & 보스
  ↓
Phase 8: UI / 이펙트 / 오디오 / 최적화 / 밸런스
  ↓
Phase 9: 로컬 서버 빌드 안정화 (Windows, Relay 코드 공유)
```

---

## 13. 주요 리스크

| 리스크 | 영향 | 대응 |
|---|---|---|
| NGO 학습 곡선 | 매우 큼 | Phase 1~2에 충분히 투자, 공식 샘플(BossRoom) 참고 |
| 네트워크 디버깅 복잡도 | 큼 | Multiplayer Play Mode + Network Profiler + 로컬 Headless 서버 병행 |
| 이동 지연감 (입력 ServerRpc 왕복) | 큼 | 서버 틱 30Hz + NetworkTransform 보간으로 완화. 심하면 Phase 2에서 클라이언트 예측 레이어 추가 검토 |
| 200마리 적 동기화 성능 | 매우 큼 | Phase 3에서 `CheckObjectVisibility` 거리 기반 Visibility 조기 검증 필수. XP 오브는 NetworkObject 제외 |
| 공유 XP 레벨업 전체 정지 UX | 중간 | GameState.LevelingUp → 서버/클라이언트 전체 Time.timeScale=0 → 각자 선택 UI → 전원 완료 후 재개. UI는 unscaledDeltaTime 사용 |
| 클라이언트가 데미지 값 전송 (치트 구멍) | 큼 | TakeDamage는 서버 내부 메서드. 클라이언트 RPC는 의도(intent)만 전달 |
| UGS Relay 비용 | 낮음 | Free Tier(월 50GB 데이터)로 소규모 테스트 충분. 초과 시 직접 IP 접속으로 대체 |
| 서버 PC 방화벽/포트 | 중간 | LAN 직접 접속 시 방화벽 포트 개방 필요. Relay 사용 시 해당 없음 |

---

## 14. 개발 원칙

1. **서버가 진실이다.** 데미지, 스폰, 드랍, 레벨업 결과는 서버에서만 결정한다.
2. **클라이언트는 의도만 보낸다.** ServerRpc에는 데미지 값, 아이템 획득 결과 같은 게임 상태를 넣지 않는다. 방향, 요청, ID만 전송한다.
3. **클라이언트는 표현만 한다.** VFX, SFX, 카메라, 로컬 UI는 클라이언트 몫이다.
4. **UI와 VFX는 이벤트 기반으로 연결한다.** View나 이펙트 컴포넌트가 Manager/NetworkObject를 직접 찾아 참조하지 않도록 하고, `UIEventHub`, `GameEventSO`, C# event 같은 채널을 통해 상태 변경을 수신한다. 오디오는 후순위로 구현하되 같은 이벤트 기반 규칙을 따른다.
5. **UI View는 자식 객체 직접 참조를 허용한다.** 같은 UI 프리팹/Canvas 하위의 `Text`, `Image`, `Button`, `Slider`, 슬롯 프리팹 같은 표시용 컴포넌트는 `[SerializeField]`로 직접 연결해도 된다. 단, View는 Manager, NetworkObject, 다른 플레이어, 다른 시스템 상태를 직접 찾지 않는다.
6. **UI가 아닌 시스템 간 통신은 이벤트 기반을 기본으로 한다.** 플레이어 간 상태 전달, 스킬/아이템/스테이지/보스 상태 전파, VFX 트리거처럼 소유 경계를 넘는 흐름은 이벤트 허브, 명시적 이벤트 채널, Facade/Adapter를 통해 접근한다.
7. **이벤트 수명 주기를 명시한다.** 구독과 해제는 한 클래스 안에 쌍으로 두고, ViewModel/Binder/Adapter는 `Dispose` 또는 `Unbind`를 제공한다. 씬 전환, 재시작, 호스트 재시작 후 이전 구독자가 남지 않아야 한다.
8. **IsServer / IsOwner 가드를 빠뜨리지 않는다.** 모든 NetworkBehaviour에 명시적으로 작성한다.
9. **NGO RPC 문법은 Mirror와 다르다.** `[TargetRpc]`·`NetworkConnection`은 Mirror 용어다. NGO 2.x는 `[Rpc(SendTo.SpecificClients)]`, NGO 1.x는 `ClientRpcParams`를 사용한다.
10. **Time.timeScale은 전원 참여 선택 화면(LevelingUp·ChestOpening)에만 허용한다.** 두 상태 모두 모든 플레이어가 동시에 멈추고 선택하므로, `GameState.LevelingUp` / `GameState.ChestOpening` 진입/퇴장 시 서버와 전체 클라이언트에서 `Time.timeScale = 0/1` 처리한다. UI 애니메이션은 `unscaledDeltaTime` 사용. 그 외 개인 일시정지 용도로는 사용 금지.
11. **NetworkObject 수를 최소화한다.** XP 오브처럼 수백 개가 필요한 것은 서버 데이터 + 클라이언트 비주얼 프록시로 처리한다.
12. **ScriptableObject로 데이터를 관리한다.** 수치는 코드가 아니라 Inspector에서 조정한다. 동종 데이터가 여러 개 필요한 경우(스테이지, 웨이브, 난이도 스케일링 등)는 개별 `.asset` 파일 대신 `DataTableSO<TRow>` 패턴으로 단일 테이블 에셋에 행 단위로 관리한다.
13. **매 Phase 끝마다 멀티플레이 가능한 상태를 만든다.** Phase 완료 기준은 항상 2인 이상 동작 확인이다.
14. **Host 모드로 빠르게 반복하되, Server Build 경로를 Phase 1에 smoke test한다.** Windows Server Build 안정화는 Phase 9까지 미룬다. Linux/클라우드 배포는 장기 확장으로 별도 Phase에서 다룬다.
