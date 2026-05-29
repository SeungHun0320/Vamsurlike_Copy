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
| 게임 상태 | 서버 | Playing / LevelingUp / BossPhase / Clear / GameOver를 NetworkVariable로 동기화 |

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

### ItemDataSO / CombineRecipeSO / WaveDataSO / StageDataSO

구조는 기존 계획과 동일. WaveDataSO에 `int basePlayerCount` 추가 (Co-op 밸런싱 기준).

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
- [ ] 로컬 Cinemachine 설정 (OnNetworkSpawn에서 IsLocalPlayer 기준으로 카메라 활성화)
- [ ] NetworkPlayerSpawner 구현 (서버가 플레이어 스폰 위치 지정)
- [ ] CharacterDataSO 연결 (baseHP, baseMoveSpeed 초기화)
- [ ] 더미 적 배치 테스트 (서버 내부에서 TakeDamage 호출 → NetworkVariable HP 감소 → 클라이언트 HUD 확인)

예상 기간: 3~5일

---

### Phase 3. 적과 스폰

Done when: 서버가 적 3종을 스폰하고 NavMesh로 플레이어를 추적하며, 공격·사망·XP 드랍이 모든 클라이언트에 동기화된다.

- [ ] EnemyDataSO 3종 작성
- [ ] WaveDataSO, WaveEntryData 구현 (Co-op 플레이어 수 배율 포함)
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
- [ ] GameState에 `LevelingUp` 추가 (`NetworkVariable<GameState>` in StageNetworkManager)
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

### Phase 6. 아이템과 조합

Done when: 적이 아이템을 드랍하고 픽업 후 조합 가능 상태가 되면 CombinationUI가 뜨며 스킬이 진화한다.

- [ ] ItemDataSO, ItemLevelData, DropTableSO 구현
- [ ] CombineRecipeSO 구현
- [ ] NetworkedItemPickup 구현 (NetworkObject — 드랍 빈도 낮아 개별 동기화 허용)
  - 서버 드랍 시 `NetworkObject.Spawn()`, 클라이언트 범위 진입 → `PlayerPickupController.RequestItemPickupServerRpc(ulong networkObjectId)` → 서버 검증 후 `Despawn()`
- [ ] ItemManager 구현 (플레이어별 보유 아이템, 서버 관리)
- [ ] CombinationSystem 구현 (서버 검증 후 스킬 진화)
- [ ] CombinationUI 구현 (`ShowCombinationRpc` — NGO 2.x: `[Rpc(SendTo.SpecificClients)]`, NGO 1.x: `ClientRpcParams`로 해당 클라이언트에만 표시)
- [ ] SummonSkill 구현
- [ ] 아이템 6~8종, 조합식 3~4종 작성

예상 기간: 4~7일

---

### Phase 7. 스테이지와 보스

Done when: Stage_01에서 5분 생존 후 보스가 등장하고, 보스 처치/전멸 결과가 전원에게 동기화된다.

- [ ] StageDataSO 구현
- [ ] StageNetworkManager 구현
  - `NetworkVariable<GameState>` Playing / LevelingUp / BossPhase / Clear / GameOver
  - 생존 타이머 서버에서만 실행
- [ ] MapManager 구현
- [ ] BossNetworkBase 구현 (EnemyNetworkBase 상속, 페이즈 전환 로직)
- [ ] BossHealthBar 구현 (`NetworkVariable<float>` HP → 모든 클라이언트 HUD)
- [ ] Stage_01 웨이브 데이터 작성
- [ ] 보스 1종 데이터 작성
- [ ] 승리/패배 `[ClientRpc]` 동기화 → 결과 화면 전환

예상 기간: 4~7일

---

### Phase 8. UI, 저장, 최적화, 밸런스

Done when: 메인 메뉴 → 방 생성 → 스테이지 → 결과 → 메인 메뉴 루프가 4인 모두 에러 없이 완성된다.

- [ ] GameEventSO, EventListener 구현
- [ ] AudioManager 구현 (클라이언트 로컬)
- [ ] SaveManager 구현 (로컬 세이브: 설정, 통계)
- [ ] HUDController 구현 (IsLocalPlayer 기준 HP, SharedXP/SharedLevel, 스킬 슬롯)
- [ ] Co-op HUD 추가 (팀원 HP 미니 표시)
- [ ] SkillSlotUI, ItemSlotUI 구현
- [ ] ResultUI 구현 (개인 통계: 처치수, 데미지, 생존 시간)
- [ ] LoadingScreen 구현 (NetworkManager.SceneManager 로딩 이벤트 연동)
- [ ] MainMenu 구현 (방 만들기/참여 UI 완성)
- [ ] 설정 UI 구현 (음량, 해상도)
- [ ] 카메라 쉐이크 (클라이언트 로컬)
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
Phase 8: UI / 저장 / 최적화 / 밸런스
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
4. **IsServer / IsOwner 가드를 빠뜨리지 않는다.** 모든 NetworkBehaviour에 명시적으로 작성한다.
5. **NGO RPC 문법은 Mirror와 다르다.** `[TargetRpc]`·`NetworkConnection`은 Mirror 용어다. NGO 2.x는 `[Rpc(SendTo.SpecificClients)]`, NGO 1.x는 `ClientRpcParams`를 사용한다.
6. **Time.timeScale은 레벨업 전체 정지에만 허용한다.** 공유 XP 레벨업 시 모든 플레이어가 동시에 선택하므로 `GameState.LevelingUp` 진입/퇴장 시 서버와 전체 클라이언트에서 `Time.timeScale = 0/1` 처리한다. UI 애니메이션은 `unscaledDeltaTime` 사용. 그 외 개인 일시정지 용도로는 사용 금지.
7. **NetworkObject 수를 최소화한다.** XP 오브처럼 수백 개가 필요한 것은 서버 데이터 + 클라이언트 비주얼 프록시로 처리한다.
8. **ScriptableObject로 데이터를 관리한다.** 수치는 코드가 아니라 Inspector에서 조정한다.
9. **매 Phase 끝마다 멀티플레이 가능한 상태를 만든다.** Phase 완료 기준은 항상 2인 이상 동작 확인이다.
10. **Host 모드로 빠르게 반복하되, Server Build 경로를 Phase 1에 smoke test한다.** Windows Server Build 안정화는 Phase 9까지 미룬다. Linux/클라우드 배포는 장기 확장으로 별도 Phase에서 다룬다.
