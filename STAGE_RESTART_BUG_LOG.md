# Stage Restart Bug Investigation Log

정리일: 2026-07-10 / 갱신: 2026-07-13 (증상4 원인 특정 + 수정)

스테이지 재시작(로비 복귀 → 재입장, 또는 재시작 플로우) 시 발생하던 네 가지 증상을 조사하고 수정한 기록.

## 현재 상태 (2026-07-13 기준)

증상 1~4 전부 원인 특정, 수정, 검증까지 완료.

증상 1~3은 `SceneMigrationSynchronization` 수정과 `IsWaveSystemReady` 게이트로 해결. 증상 4는 아래
"증상 4" 절의 진짜 원인(풀 반환 시점 오브젝트 상태 미검증)을 찾아 수정했고, 2단계 A/B 검증으로
최소한의 수정만 남겼다:

1. **1차 검증**: 후보 가드(`gameObject.scene.isLoaded` 4곳 + `NetworkObjectPool.Get()`의 `IsSpawned`
   체크, 총 5곳) 전부를 주석 처리한 채로 재현 → "이미 스폰됨" 에러와 총알 순간이동이 그대로 재현됨을
   확인. 이 5곳이 진짜 원인이 맞다는 확증을 얻음.
2. **2차 검증(격리)**: `NetworkObjectPool.Get()`의 `IsSpawned` 체크만 남기고 나머지 4곳
   (`EnemyNetworkBase`/`NetworkProjectile`/`NetworkedItemPickup`/`BossMissile`의 `scene.isLoaded`
   조건)을 다시 주석 처리한 채로 재현 → 문제없이 정상 동작 확인. **즉 `Get()` 쪽 방어 하나만으로
   충분했다** — 4곳의 `scene.isLoaded` 조건은 삭제.

**최종 수정은 `NetworkObjectPool.Get()`의 `IsSpawned` 체크 한 곳뿐이다.** 나머지 4개 파일은 원래
코드(무조건 `ReturnNetworkObject` 호출)로 복원했다 — 그래도 문제가 안 생기는 이유는 아래 "적용한 수정"
절 참고.

## 증상 1: 콘솔 에러 — Object Scene Migration

```
[Netcode] [Object Scene Migration] Trying to synchronize NetworkObjectId (608) but it was not spawned or no longer exists!!
```

**원인**: 풀링되는 전투용 프리팹(적/아이템/투사체)의 `NetworkObject.SceneMigrationSynchronization`이
`1`(true)로 잘못 설정되어 있었다. 이 플래그는 원래 "씬이 바뀌어도 계속 살아있어야 하는 오브젝트"(플레이어 등)를
위한 것인데, 재사용/재활용되는 전투 오브젝트에도 켜져 있었다.

재시작으로 `NetworkManager.SceneManager.LoadScene(..., LoadSceneMode.Single)`이 호출되는 순간, NGO는
그 시점에 스폰돼 있고 `SceneMigrationSynchronization=1`인 오브젝트 ID 목록을 스냅샷 떠서 동기화 메시지에
담는다. 실제 씬 언로드/파괴와 클라이언트의 메시지 역직렬화는 그 직후가 아니라 비동기로 일어나므로, 그 좁은
시간 창 사이에 적이 죽어서 despawn + 풀 반환되면 이미 스냅샷에 박제된 ID가 클라이언트 쪽에선 "스폰된 적
없음" 취급을 받는다.

**수정**: 아래 9개 프리팹의 `SceneMigrationSynchronization`을 `0`으로 변경 (플레이어 프리팹이 이미 쓰고
있던 값과 동일하게 통일).

- `Assets/Prefabs/Enemies/Enemy_A.prefab`, `Enemy B.prefab`, `Enemy C.prefab`, `Enemy D.prefab`(보스 모델), `Missile Boss.prefab`
- `Assets/Prefabs/Items/Chest.prefab`, `Item Ammo.prefab`, `Item Heart.prefab`
- `Assets/Prefabs/Skills/BasicProjectile.prefab`

이 값은 코드에서 런타임 참조하는 곳이 없는 순수 인스펙터 설정이라, 로직 변경 없이 값만 바꾼 안전한 수정.

## 증상 2: 재시작 후 몬스터/총알이 멈춤

**원인**: 증상 1과 같은 뿌리. `SceneMigrationSynchronization=1`이던 오브젝트 중 재시작 순간 죽지 않고
살아남은 것들은 파괴되는 게 아니라 그대로 새 스테이지 인스턴스로 "이주"된다. 이렇게 이주된 오브젝트는
`NetworkObject.Spawn()`이 다시 호출되는 게 아니므로 `EnemyAI.OnServerSpawned()`(NavMeshAgent 재활성화,
타이머 리셋 등)가 재실행되지 않는다. 특히 씬이 재로드되며 NavMesh 데이터도 다시 빌드되는데, 이주해서
살아남은 NavMeshAgent가 새로 빌드된 NavMesh에 다시 연결(재-warp)된다는 보장이 없어 `isOnNavMesh`가
false로 굳으면 그 몬스터는 이동 명령을 영원히 못 받고 멈춘다.

**수정**: 증상 1과 동일 (프리팹 플래그 수정으로 애초에 이주 자체가 안 일어나게 함).

## 증상 3: 특정 클라이언트에 하트가 안 보임 / 재시작 직후 투사체 튐

```
[Netcode] [Deferred OnSpawn] Messages were received for a trigger of type NetworkTransformMessage
associated with id (1426), but the NetworkObject was not received within the timeout period 10 second(s).
```

**원인**: `StageRuntime.OnNetworkSpawn()`이 서버 자신의 씬 로드가 끝나자마자 바로
`waveController.Begin()`을 호출했다 — 클라이언트들이 씬 동기화(handshake)를 끝냈는지는 전혀 확인하지
않았다. 재시작 시 전원이 동시에 씬을 다시 로드하는데, 그중 한 명의 로딩이 남들보다 느리면, 그 클라이언트가
아직 동기화 중인 상태에서 서버는 이미 적을 스폰하고 하트를 드랍하고 투사체를 날리기 시작한다. 그 클라이언트
입장에선 "스폰 메시지도 안 왔는데 위치 갱신 메시지(NetworkTransformMessage)만 계속 날아오는" 상황이 되고,
NGO는 이걸 최대 10초까지 보류하다가 버린다 — 하트는 그 클라에서 생성 자체가 안 되고, 투사체는 뒤늦게
처리되며 위치가 튀어 보인다.

같은 문제가 플레이어 스킬 캐스팅(자동 공격 포함) 쪽에도 있었다 — `SkillManager.Update()`는
`GameFlowCoordinator.IsGameplayActive`만 확인했는데, 이 값은 스테이지 로드 직후 거의 즉시 true가 되므로
씬 로딩이 빨리 끝난 플레이어는 다른 클라가 아직 동기화 중이어도 곧바로 투사체를 쏠 수 있었다.

**수정**:
- `StageRuntime.cs`: `waveController.Begin()`을 즉시 호출하는 대신
  `NetworkManager.SceneManager.OnLoadEventCompleted`(모든 클라이언트의 씬 동기화가 끝났다는 서버 이벤트,
  `NetworkPlayerSpawner`가 플레이어 스폰에 이미 쓰던 것과 동일한 패턴)를 기다렸다가 호출하도록 변경.
  이 시점에 `true`가 되는 `StageRuntime.IsWaveSystemReady` 플래그를 새로 추가.
- `SkillManager.cs`: `Update()`(자동 스킬 틱)와 `ActivateFirstManualSkillServerRpc`(수동 스킬) 양쪽 다
  `StageRuntime.Instance.IsWaveSystemReady`를 확인하도록 추가 — 웨이브가 시작되는 시점까지 스킬
  캐스팅(투사체 스폰)도 함께 대기한다.

## 증상 4: 재시작 직후 총알(및 궤도 스킬)이 한순간 비정상적으로 빠르게 움직임

사용자 확인: "속도가 엄청 빠르게 발사돼서 어디를 중심으로 공전하는 느낌. 그러다 정상속도로 돌아옴" /
"공전총알만 그런 게 아니고 다른 총알들도 다 그럼" — 타겟팅 로직(사거리 안에 적이 없으면 `CasterForward`로
발사하는 폴백)은 의도된 동작이 맞음. 문제는 발사 방향이 아니라 **속도** 자체.

### 1차 가설 (틀림): deltaTime 미클램프

`NetworkProjectile.Update()`가 `Time.deltaTime`을 클램프 없이 써서, 어느 한 프레임이 비정상적으로
길어지면(`PoolManager.WarmupDeferredPools()`가 적 프리팹 60개를 한 프레임에 동기 `Instantiate()`하는
것으로 추정) 그 프레임에 이동/회전이 과도하게 진행되는 것이라 판단했었다. 하지만 **사용자가 "혼자
플레이할 땐 안 그렇고 2명 이상 접속해야 재현된다"고 확인** — `WarmupDeferredPools()`는 인원수와
무관하게 항상 동일하게 실행되므로 이 가설은 근본 원인이 될 수 없다(솔로에서도 재현됐어야 함).

### 2차 가설 (부분적으로 틀림): NetworkTransform 보간 캐치업 + MPM CPU 경쟁

NGO `NetworkTransform`의 보간(interpolation) 캐치업 메커니즘 자체는 실제로 존재한다(NGO 2.11.2 소스
`BufferedLinearInterpolator.cs`로 확인 — 클라이언트 버퍼에 갱신이 밀려서 쌓이면 밀린 만큼을
`PositionMaxInterpolationTime`(`BasicProjectile.prefab`에 `0.1`초로 설정) 안에 압축 재생한다). 이
메커니즘은 **호스트에는 적용되지 않고 원격 클라이언트에만 적용**되므로 "2명 이상에서만 재현"과
방향은 맞았다.

다만 이걸 "MPM 한 PC에서 CPU 경쟁 때문에 클라이언트 프로세스가 밀린다"는 원인으로 단정하고
`WarmupOverFrames`/`SpawnPlayersOverFrames`로 프레임 분산 완화책까지 적용했지만, 사용자가 재현 후
"이런 게 원인이 아닌 것 같다"고 확인 — 즉 이 프레임 분산 수정은 **증상을 없애지 못했다**. 원인의 방향
(보간 캐치업이 원격 클라이언트에서만 보인다는 것 자체)은 맞았지만, "왜 그만큼 밀리는가"의 진짜 트리거를
못 찾은 상태였다. (프레임 분산 수정 자체는 근거가 있는 별개의 개선이라 되돌리지 않고 남겨뒀다.)

### 재조사: 계측 로그 + 실제 재현으로 확정

사용자가 "게임 종료 후 로비로 갔다가 다시 시작하는 과정에 뭔가 있을 것 같다"는 힌트를 줘서, 추측 대신
아래를 임시로 계측(이후 전부 제거):
- `StageRuntime`: `IsWaveSystemReady`가 `true`가 되는 시점(틱/벽시계)
- `SkillManager`: 각 플레이어의 첫 캐스트 시점 — 전원이 같은 프레임에 몰리는지 확인용
- `NetworkObjectPool.Get/Return`: 재사용인지 신규 생성인지, 재사용이면 풀에서 대기한 시간
- `NetworkProjectile`(원격 클라이언트 전용): 프레임간 이동거리가 최근 평균 대비 비정상적으로 크면 경고

재현 중 실제 Unity 콘솔에서 다음 **런타임 Netcode 에러**(컴파일 에러 아님)가 서버 쪽에 찍히는 걸 확인:

```
[Netcode] Cannot process spawn of Enemy B(Clone) as it is already spawned!
  ...
  Vamsurlike.Network.EnemySpawnManager:SpawnEnemy (...)
  Vamsurlike.Network.EnemySpawnManager:SpawnEnemyByName (...)
  Vamsurlike.Stage.WaveController:SpawnAnywhereOnMap (...)
  Vamsurlike.Stage.WaveController/<SpawnEntry>d__22:MoveNext ()
```

이게 결정적 증거였다 — `PoolManager.GetOrInstantiateNetworkObject()`가 이미 `IsSpawned=true`인
인스턴스를 풀에서 꺼내줬다는 뜻이고, 총알 쪽 SpeedAnomaly 로그와 **같은 결함**일 가능성이 높다고
판단했다: 몬스터는 NGO가 재스폰을 거부해서 에러로 드러나고, 총알은 `NetworkObject.Spawn()`이 조용히
실패해도(리턴값 없음) 호출부가 감지 못해 위치만 옮겨진 채 진행 → 클라이언트에서 순간이동/속도폭주로
보이는 것.

### 실제 원인 (NGO 2.11.2 소스로 확정)

`EnemyNetworkBase`, `NetworkProjectile`, `NetworkedItemPickup`, `BossMissile` 전부
`OnNetworkDespawn()`에서 **무조건** `PoolManager.ReturnNetworkObject()`를 호출한다.

`NetworkObject.OnDestroy()`(NGO 소스, `Runtime/Core/NetworkObject.cs:1728`)를 보면: **씬이 언로드될
때 아직 명시적으로 Despawn되지 않은 채 살아있던 오브젝트도, Unity가 그 GameObject를 파괴하면서 NGO가
자동으로 `SpawnManager.OnDespawnObject(this, destroyGameObject:false)`를 호출**하고, 이게 우리
`OnNetworkDespawn()` 콜백을 그대로 실행시킨다 — 게임 로직이 명시적으로 죽인 게 전혀 아닌데도.

문제: 이 시점에 `ReturnNetworkObject()`가 호출되면 `NetworkObjectPool.Return()`이 검증 없이 그대로
풀에 다시 넣는다. 하지만 이 GameObject는 **Unity 엔진이 scene 언로드의 일부로 이미 파괴를 확정한
상태**(우리는 지금 그 오브젝트 자신의 `OnDestroy()` 콜백 안에 있다)라, `SetActive`/`MoveGameObjectToScene`로
"되살리는" 시도는 무의미하다 — 좀비 인스턴스가 풀 스택에 들어간 셈. 다음 `Get()` 호출이 이걸 재사용하려
하면 몬스터는 NGO의 "이미 스폰됨" 거부로, 총알은 조용한 실패 후 위치만 옮겨진 채 남아 클라이언트에서
순간이동으로 보인다.

`gameObject.scene.isLoaded`가 이 두 경우(정상 게임플레이 중 소멸 vs 씬 언로드로 인한 파괴)를 구분하는
신뢰할 수 있는 신호라는 것도 NGO 자신의 코드에서 확인했다 — `NetworkObject.OnDestroy()` 자체가 정확히
같은 방식(`gameObject.scene.IsValid() && gameObject.scene.isLoaded`)으로 "유효한 파괴인지" 판별한다.

**시도했다가 되돌린 오답**: 처음엔 `NetworkObjectPool.Return()` 쪽에 `if (instance.IsSpawned) return;`
가드를 넣었었다. 그런데 NGO 소스(`NetworkSpawnManager.OnDespawnObject`)를 다시 확인해보니
`InvokeBehaviourNetworkDespawn()`(우리 `OnNetworkDespawn` 콜백을 실행하는 지점)은 `ResetOnDespawn()`
(`IsSpawned = false`로 리셋하는 지점)보다 **먼저** 실행된다 — 즉 `Return()`이 호출되는 시점엔 정상적인
사망/소멸 반환이든 씬 언로드로 인한 반환이든 **항상** `IsSpawned == true`다. 이 가드를 그대로 뒀으면
정상적인 풀 반환까지 전부 막혀서 풀링이 사실상 동작을 멈췄을 뻔했다 — 컴파일은 통과하지만 조용히
전체 재사용 흐름을 깨는 실수였다. 검증 후 즉시 되돌렸다.

### 적용한 수정 (최종)

`NetworkObjectPool.Get()`: 풀에서 꺼낸 인스턴스가 `IsSpawned == true`(씬 언로드 도중 자동 반환된 좀비
상태)면 폐기하고 다음 항목을 시도(없으면 `Create()`로 새로 생성). **딱 이 한 곳만 수정.**

`EnemyNetworkBase`/`NetworkProjectile`/`NetworkedItemPickup`/`BossMissile`의 `OnNetworkDespawn()`은
원래대로(무조건 `ReturnNetworkObject` 호출) 되돌렸다 — 좀비 인스턴스가 여전히 풀에 들어가는 건
막지 않지만, 그걸 실제로 다시 꺼내 쓰려는 유일한 지점(`Get()`)에서 걸러지므로 피해가 발생하지 않는다.
"애초에 못 들어가게 막기"(`scene.isLoaded` 조건, 4곳)와 "꺼낼 때 걸러내기"(`Get()`의 `IsSpawned` 체크,
1곳) 중 후자 하나로 충분하다는 걸 2차 격리 검증으로 확인했다 — 코드 변경 지점을 최소화하기 위해 후자만
남겼다.

계측 로그(`[RestartDebug]` 태그, `NetworkObjectPool`의 `debugReturnedAtRealtime` 등)는 전부 제거 완료.

## 변경 파일 요약

- `Assets/Prefabs/Enemies/Enemy_A.prefab`, `Enemy B.prefab`, `Enemy C.prefab`, `Enemy D.prefab`, `Missile Boss.prefab`
- `Assets/Prefabs/Items/Chest.prefab`, `Item Ammo.prefab`, `Item Heart.prefab`
- `Assets/Prefabs/Skills/BasicProjectile.prefab`
- `Assets/Scripts/Stage/StageRuntime.cs` (`IsWaveSystemReady`, `OnLoadEventCompleted` 대기)
- `Assets/Scripts/Skills/SkillManager.cs` (`IsWaveSystemReady` 게이트 추가)
- `Assets/Scripts/Skills/NetworkProjectile.cs` (deltaTime 클램프)
- `Assets/Scripts/Network/PoolManager.cs`, `NetworkObjectPool.cs`, `INetworkObjectPool.cs` (프레임 분산 워밍업, `Get()`의 `IsSpawned` 가드 — 증상4 최종 수정)
- `Assets/Scripts/Network/NetworkPlayerSpawner.cs` (플레이어 스폰 프레임 분산)
