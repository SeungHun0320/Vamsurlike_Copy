# 프로젝트 전체 검토 메모

검토일: 2026-07-08

범위:
- 최근 변경분만이 아니라 Unity 프로젝트 전체 기준으로 정적 검색, 빌드, Unity Editor 콘솔을 확인했다.
- `dotnet build Assembly-CSharp.csproj` 결과: 경고 0개, 오류 0개.
- 씬/프리팹의 명확한 Missing Script 패턴(`m_Script: {fileID: 0}`)은 검색상 발견하지 못했다.
- Build Settings 씬: `Bootstrap`, `MainMenu`, `Stage_01`.

## 우선 처리 이슈

### 1. NetworkErrorDialog DontDestroyOnLoad 에러

- 위치: `Assets/Scripts/UI/NetworkErrorDialog.cs:33`
- Unity Editor Console 실제 에러:
  - `DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.`
- 원인:
  - `NetworkErrorDialog`가 Bootstrap Canvas 하위 오브젝트에 붙어 있을 경우 `DontDestroyOnLoad(gameObject)`가 루트가 아닌 오브젝트에 호출된다.
- 권장:
  - 루트 전용 오브젝트로 분리하거나 `transform.root.gameObject`를 무작정 영속화하지 말고, 영속 UI 루트를 명시적으로 구성한다.
  - `LoadingScreen`, `NetworkErrorDialog`, `SettingsManager`, `AudioManager`, `UIEventHub` 등 영속 객체 수명주기를 Bootstrap 전용 루트에서 관리한다.

### 2. 네트워크/씬 설정값 하드코딩 중복

- 위치:
  - `Assets/Scripts/Network/GameNetworkManager.cs:13`
  - `Assets/Scripts/Network/NetworkBootstrapper.cs:57`
  - `Assets/Scripts/UI/MainMenuUI.cs:21`
  - `Assets/Scripts/UI/ViewModels/LobbyViewModel.cs:11`
  - `Assets/Scripts/Audio/AudioManager.cs:15`
  - `Assets/Scripts/Core/BootstrapLoader.cs:9`
  - `Assets/Scripts/Network/NetworkPlayerSpawner.cs:11`
- 문제:
  - `127.0.0.1`, `0.0.0.0`, `7777`, `MainMenu`, `Stage_01`이 여러 파일에 분산돼 있다.
  - 한 곳만 바꾸면 클라이언트 UI, 서버 부트스트랩, 네트워크 매니저, 오디오 씬 분기 로직이 서로 어긋날 수 있다.
- 권장:
  - `NetworkConfigSO`, `SceneConfigSO` 같은 단일 설정 자산으로 중앙화한다.
  - UI 문구에 들어가는 포트 예시도 같은 설정에서 포맷하도록 맞춘다.

### 3. ScriptableObject 데이터 규칙 검증 부족

- 위치:
  - `Assets/Scripts/Data/SkillDataSO.cs`
  - `Assets/Scripts/Upgrades/UpgradeOptionSO.cs`
  - `Assets/Scripts/Items/CombineRecipeSO.cs`
  - `Assets/Scripts/Core/StartupValidator.cs`
- 문제:
  - 현재 룰이 사람 기억에 의존한다.
  - 예: 조합 스킬은 `maxLevel = 1`, 조합 스킬 레벨 스탯은 1개, 투사체 수 증가는 업그레이드 카드에서만, 조합에 필요한 패시브는 상점 영구 패시브와 섞이면 안 됨.
- 권장:
  - `OnValidate` 또는 Editor validation 메뉴를 추가한다.
  - `StartupValidator`가 카탈로그 존재 여부뿐 아니라 내부 참조 무결성까지 검사하게 확장한다.
  - 검사 대상:
    - `SkillDataSO.maxLevel`과 `levelStats.Length` 일치 여부
    - 조합 스킬 폴더의 스킬이 전부 `maxLevel = 1`인지
    - 조합 레시피의 `sourceSkill`, `evolvedSkill` 누락 여부
    - `requiredPassiveType`이 의도한 패시브 타입인지
    - `UpgradeCatalog` 안 옵션의 `skillData` 누락 여부
    - 순수 패시브 카드의 `maxLevel` 유효성

### 4. CatalogVersionUtility 해시 범위 부족

- 위치: `Assets/Scripts/Network/CatalogVersionUtility.cs`
- 문제:
  - 업그레이드 옵션은 `name`, `effectType`, `value` 정도만 해시에 들어간다.
  - `skillData`, `maxLevel`, 스킬 레벨별 스탯, 조합 스킬 스탯 변경이 해시에 빠질 수 있다.
  - 멀티에서 서버/클라이언트 데이터가 다르게 들어갔는데도 같은 카탈로그 버전으로 통과할 위험이 있다.
- 권장:
  - 스킬 카탈로그를 명시적으로 만들거나, 레시피/업그레이드에서 참조하는 `SkillDataSO`의 핵심 필드를 재귀적으로 해시에 포함한다.
  - 적어도 `maxLevel`, `castType`, `levelStats` 핵심 값, `skillData.name`은 포함한다.

### 5. WaveController 맵 전체 스폰 범위가 Stage_01에 묶여 있음

- 위치: `Assets/Scripts/Stage/WaveController.cs`
- 현재 변경 내용:
  - 기본 웨이브 스폰이 플레이어 근처가 아니라 맵 전체 NavMesh 샘플링으로 바뀌었다.
  - `mapSpawnHalfExtent = 220f`가 `Stage_01 Ground(500x500)` 기준이다.
- 문제:
  - 스테이지 추가나 맵 크기 변경 시 스폰 범위가 틀어질 수 있다.
  - NavMesh 샘플 실패 시 원점으로 폴백해서 특정 상황에서 몬스터가 중앙에 몰릴 수 있다.
- 권장:
  - 씬에 `SpawnBounds` 컴포넌트를 두고 `WaveController`가 참조하게 한다.
  - 또는 `StageTable`/`StageConfigSO`에 스폰 영역을 데이터화한다.

### 6. 에디터 자동화 스크립트가 씬 구조에 강하게 의존

- 위치:
  - `Assets/Editor/BuildSettingsUI.cs`
  - `Assets/Editor/BuildPermanentUpgradeShopUI.cs`
  - `Assets/Editor/FixCanvasScalers.cs`
  - `Assets/Editor/PreviewUI.cs`
  - `Assets/Editor/PreviewCardUI.cs`
- 문제:
  - `GameObject.Find("Canvas")`, 고정 경로 문자열, `DestroyImmediate`, `SaveOpenScenes` 사용이 많다.
  - 씬 구조가 바뀌면 엉뚱한 오브젝트를 수정하거나 저장할 수 있다.
- 권장:
  - 대상 씬 이름 검증, Undo 등록, 실행 전 확인창, 변경 요약 로그를 넣는다.
  - 재생성형 UI는 가능하면 Prefab 기반으로 옮긴다.

### 7. 런타임 카메라 바인딩이 이름 문자열에 의존

- 위치: `Assets/Scripts/Player/LocalPlayerCameraBinder.cs`
- 문제:
  - `"CM_FollowCam"` 이름으로 찾고 실패하면 첫 `CinemachineCamera`를 잡는다.
  - 카메라가 여러 개가 되거나 이름이 바뀌면 잘못 바인딩될 수 있다.
- 권장:
  - `CameraRig` 전용 컴포넌트를 만들고 씬 서비스/태그/참조로 찾는다.
  - 이름 기반 fallback은 디버그 안전망 정도로만 둔다.

### 8. Phase 8 임시 이벤트 브릿지 잔존

- 위치:
  - `Assets/Scripts/Items/ChestRewardManager.cs`
  - `Assets/Scripts/Upgrades/LevelUpManager.cs`
  - `Assets/Scripts/Skills/SkillManager.cs`
  - `Assets/Scripts/Player/PlayerReviveHandler.cs`
- 문제:
  - 기존 static event와 `UIEventHub` 발행이 같이 남아 있다.
  - 새 UI와 구 UI가 동시에 구독하면 중복 호출/잔존 구독 버그가 생길 수 있다.
- 권장:
  - UI가 `UIEventHub`로 완전히 이동했다면 구 static event를 제거한다.
  - 제거 전에는 구독자를 검색해서 남은 참조를 정리한다.

### 9. 현재 워크트리의 큰 폰트 SDF 변경

- 위치: `Assets/Resources/Fonts/MalgunGothic SDF.asset`
- 문제:
  - diff가 수천 줄이라 실제 코드/데이터 변경 리뷰를 흐린다.
  - 의도한 폰트 재생성이 아니면 커밋에서 제외하는 게 좋다.
- 권장:
  - 의도한 변경인지 확인.
  - 의도하지 않은 변경이면 별도 처리 또는 제외.

## 참고로 문제 없음으로 확인한 것

- 현재 C# 빌드는 성공한다.
- 빌드 경고도 현재는 0개다.
- 명확한 Missing Script 패턴은 씬/프리팹에서 검색상 나오지 않았다.
- Build Settings에는 `Bootstrap`, `MainMenu`, `Stage_01`이 활성화되어 있다.
- 픽업 거절 처리 쪽은 `NetworkedItemPickup.OnPickupRejected`와 `PlayerPickupController.pendingPickupRequests` 정리가 들어가 있어, 예전에 의심했던 pending 고착 문제는 현재 코드 기준으로는 완화되어 있다.

## 추천 처리 순서

1. `NetworkErrorDialog`의 `DontDestroyOnLoad` 에러 제거.
2. 영속 Bootstrap 오브젝트 구조 정리.
3. 네트워크/씬 설정 중앙화.
4. `SkillDataSO`, `UpgradeOptionSO`, `CombineRecipeSO`, 카탈로그 검증기 추가.
5. `CatalogVersionUtility` 해시 범위 확장.
6. Wave 스폰 영역 데이터화.
7. Phase 8 임시 이벤트 브릿지 제거.
8. 에디터 자동화 스크립트 안전장치 추가.

## 2차 추가 검토 메모

추가 검토일: 2026-07-10

### 10. PlayerColorSync 이벤트 해제 버그

- 위치: `Assets/Scripts/Player/PlayerColorSync.cs:37`, `Assets/Scripts/Player/PlayerColorSync.cs:43`
- 문제:
  - `ColorIndex.OnValueChanged += (_, next) => ApplyColor(next);`
  - `ColorIndex.OnValueChanged -= (_, next) => ApplyColor(next);`
  - 위 두 람다는 서로 다른 delegate 인스턴스라 해제가 되지 않는다.
- 영향:
  - 네트워크 오브젝트가 despawn/spawn을 반복하면 콜백이 남아 중복 적용될 수 있다.
- 권장:
  - `private void OnColorChanged(int previous, int next) => ApplyColor(next);` 같은 메서드로 구독/해제한다.

### 11. CSVParser.ParseEntries가 잘못된 웨이브 데이터를 조용히 무시함

- 위치: `Assets/Scripts/Data/Runtime/CSVParser.cs:57`
- 문제:
  - `entries` 파싱에서 `seg.Length < 3`, `count` 변환 실패, `interval` 변환 실패 시 `continue`로 넘긴다.
  - CSV 오타가 있어도 에러가 아니라 "스폰이 줄어드는" 식으로 조용히 반영될 수 있다.
- 권장:
  - 라인 번호와 원본 문자열을 포함해 `FormatException`을 던지거나 `DataValidator`에서 명시적으로 오류 처리한다.

### 12. AudioManager가 SettingsManager 생성 타이밍을 놓칠 수 있음

- 위치: `Assets/Scripts/Audio/AudioManager.cs:51`, `Assets/Scripts/Audio/AudioManager.cs:76`
- 문제:
  - `AudioManager`는 `BeforeSceneLoad`에서 자동 생성된다.
  - `OnEnable` 시점에 `SettingsManager.Instance`가 아직 없으면 `OnVolumeChanged`를 구독하지 않고, 이후 재시도 로직이 없다.
- 영향:
  - 초기 볼륨 적용 또는 설정 UI에서 볼륨 변경 반영이 누락될 수 있다.
- 권장:
  - Bootstrap 초기화 순서를 명확히 하거나, `SettingsManager` 쪽에서 현재 설정을 AudioManager에 push한다.
  - 또는 AudioManager가 SettingsManager 등장 전까지 짧게 재시도한다.

### 13. PlayerStatusAdapter 필수 컴포넌트 방어가 약함

- 위치: `Assets/Scripts/Player/PlayerStatusAdapter.cs:22`, `Assets/Scripts/Player/PlayerStatusAdapter.cs:31`
- 현재 `NetworkedPlayer.prefab`에는 `PlayerNetworkStats`, `PlayerReviveHandler`, `PlayerMatchStats`, `PlayerStatusAdapter`가 모두 붙어 있다.
- 문제:
  - 코드에는 `[RequireComponent]`나 null 방어가 없다.
  - 테스트/변형 프리팹에서 하나라도 빠지면 `OnNetworkSpawn` 구독 시 NullReference가 난다.
- 권장:
  - `[RequireComponent]`를 추가하거나, 누락 시 명확한 에러 로그 후 비활성화한다.

### 14. NetworkErrorDialog의 현재 수정 방향은 에러 회피지만 루트 영속화 리스크가 있음

- 위치: `Assets/Scripts/UI/NetworkErrorDialog.cs:36`
- 현재 상태:
  - `DontDestroyOnLoad(gameObject)`에서 `DontDestroyOnLoad(transform.root.gameObject)`로 바뀌어 있다.
- 문제:
  - Unity 콘솔에는 아직 이전 `DontDestroyOnLoad only works for root...` 에러가 남아 있다.
  - 현재 코드 기준으로는 에러는 피할 수 있지만, Bootstrap Canvas 전체가 영속화될 수 있다.
- 권장:
  - `NetworkErrorDialogRoot` 같은 별도 루트 오브젝트로 분리한다.
  - `ServerAdminUI`도 같은 패턴이라 같이 정리하는 게 좋다.

### 15. NGO ServerRpc 구식 API 경고

- 위치:
  - `Assets/Scripts/Items/ChestRewardManager.cs:119`
  - `Assets/Scripts/Upgrades/LevelUpManager.cs:149`
- 빌드 결과:
  - 오류 0개, 경고 2개.
  - `ServerRpcAttribute.RequireOwnership` obsolete 경고.
- 권장:
  - 이미 `NetworkedItemPickup`에서 쓰는 방식처럼 `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]`로 이관한다.

### 16. 이동 입력 ServerRpc가 FixedUpdate마다 전송됨

- 위치: `Assets/Scripts/Player/PlayerNetworkInput.cs:49`, `Assets/Scripts/Player/PlayerNetworkController.cs:56`
- 문제:
  - 로컬 플레이어가 매 FixedUpdate마다 `SubmitMoveInputServerRpc`를 호출한다.
  - 4인 플레이나 저사양/고틱레이트 환경에서 불필요한 네트워크 트래픽이 커질 수 있다.
- 권장:
  - 입력 변화량이 작으면 전송 생략, 일정 tick rate로 throttle, 마지막 입력 캐시를 두는 식으로 줄인다.

### 17. TextMesh Pro Examples & Extras가 프로젝트에 포함되어 있음

- 위치: `Assets/TextMesh Pro/Examples & Extras`
- 문제:
  - 빌드 씬에는 안 들어가더라도 임포트/검색/리뷰 노이즈가 크다.
  - 실제 프로젝트 자산과 예제 자산이 섞여 누락 참조 검색 결과가 지저분해진다.
- 권장:
  - 필요 없으면 제거하거나, 최소한 리뷰/검증 스크립트에서 제외 경로로 둔다.

