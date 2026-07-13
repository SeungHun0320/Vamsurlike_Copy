# Project Folder Structure Audit

작성일: 2026-07-13

## 결론

지금 폴더 구조는 큰 방향은 이미 잡혀 있지만, `Resources`, `Data`, `Materials`, 루트 에셋 쪽에 런타임 경로와 작업용/외부 에셋이 섞여 있다.

바로 파일을 옮기기보다는 먼저 체크포인트를 만든 뒤, Unity Editor/AssetDatabase 기준으로 이동하는 것이 안전하다. 특히 `.meta` GUID와 `Resources.Load(...)` 경로가 엮인 파일은 단순 폴더 이동으로 끝나지 않는다.

## 현재 핵심 문제

### 1. `Assets/Resources`가 너무 넓음

현재 `Assets/Resources` 안에는 실제 런타임 로드 대상과 외부 에셋팩/일반 아트 리소스가 같이 들어 있다.

코드에서 직접 로드하는 `Resources` 경로는 다음 정도로 제한되어 있다.

| 코드 | 경로 |
| --- | --- |
| `SceneConfigSO` | `Resources/Configs/SceneConfig` |
| `NetworkConfigSO` | `Resources/Configs/NetworkConfig` |
| `DataManager` | `Resources/Data/EnemyScalingTable`, `StageTable`, `WaveTable`, `PermanentUpgradeTable` |
| `ChestFallbackRewardCatalog` | `Resources/Catalogs/ChestFallbackRewardCatalog` |
| `CombineRecipeCatalog` | `Resources/Catalogs/CombineRecipeCatalog` |
| `UpgradeCatalog` | `Resources/Catalogs/UpgradeCatalog` |
| `WorldReviveProgressUI` | `Resources/Sprites/UI/WhiteSquare` |
| `LineDefaultMaterial` | `Resources/Materials/M_LineDefault` |

그 외 `Resources/QuarterView 3D Action BE5`, 다수의 모델/프리팹/머티리얼/텍스처는 코드상 직접 로드 근거가 없다. 이 상태면 빌드 포함 범위가 불필요하게 커질 수 있다.

### 2. CSV 정본이 둘로 갈라져 있음

`DataManager`는 현재 `Assets/Resources/Data/*.csv`만 읽는다. 그런데 같은 이름의 CSV가 `Assets/Data/Stages`에도 있다.

| 파일 | 상태 |
| --- | --- |
| `EnemyScalingTable.csv` | 양쪽 존재, 내용 다름 |
| `PermanentUpgradeTable.csv` | 양쪽 존재, 내용 동일 |
| `StageTable.csv` | 양쪽 존재, 내용 다름 |
| `WaveTable.csv` | 양쪽 존재, 내용 다름 |

즉 `Assets/Data/Stages`는 현재 런타임 미사용 가능성이 높지만, 내용이 더 길거나 다르기 때문에 바로 삭제하면 위험하다. 어느 쪽을 정본으로 둘지 먼저 결정해야 한다.

### 3. 머티리얼 위치가 갈라져 있음

프로젝트 머티리얼이 아래처럼 나뉘어 있다.

| 위치 | 역할 |
| --- | --- |
| `Assets/Materials` | 적 머티리얼 등 일반 프로젝트 에셋 |
| `Assets/Resources/Materials` | 일부 런타임 로드 대상 + 일반 머티리얼이 섞임 |
| `Assets/Resources/QuarterView 3D Action BE5/Materials` | 외부 에셋팩 머티리얼 |

현재 코드상 `Resources/Materials`에 반드시 남아야 하는 것은 `M_LineDefault` 정도로 보인다. 나머지는 프리팹/씬 직접 참조라면 `Assets/Materials` 또는 `Assets/Art/Materials`로 옮기는 편이 낫다.

### 4. 루트 `Assets` 파일이 정리되지 않음

`Assets` 루트에 아래 파일이 있다.

| 파일 | 권장 위치 |
| --- | --- |
| `DefaultNetworkPrefabs.asset` | `Assets/Data/Network` 또는 `Assets/Settings/Network` |
| `InputSystem_Actions.inputactions` | `Assets/Settings/Input` |
| `UserChoices.choices` | Unity Multiplayer Play Mode 사용자 설정이면 이동/삭제 전 확인 필요 |

루트는 폴더만 두고 실제 에셋은 성격별 하위 폴더로 보내는 편이 관리하기 쉽다.

### 5. 개발/복구 산출물이 `Assets` 안에 있음

`Assets/Screenshots`, `Assets/_Recovery`는 런타임 에셋이 아니라면 `Assets` 밖으로 빼는 것이 좋다.

권장 위치:

| 현재 | 권장 |
| --- | --- |
| `Assets/Screenshots` | `Reports/Screenshots` 또는 `Documentation/Screenshots` |
| `Assets/_Recovery` | `Backup/_Recovery` 또는 삭제 후보 검토 |

## 권장 목표 구조

아래는 지금 프로젝트 흐름을 크게 흔들지 않는 기준안이다.

```text
Assets/
  Data/
    Audio/
    Characters/
    Enemies/
    Items/
    Network/
    Player/
    Skills/
    Stages/
    Upgrades/
    VFX/
  Prefabs/
    Enemies/
    Items/
    Network/
    Player/
    Skills/
    UI/
    VFX/
  Resources/
    Catalogs/
    Configs/
    Data/
    Materials/
    Sprites/UI/
  Materials/
    Enemies/
    Items/
    Player/
    Skills/
    VFX/
  Shaders/
  Scenes/
  Scripts/
    Audio/
    Core/
    Data/
    Enemy/
    Items/
    Network/
    Player/
    Skills/
    Stage/
    UI/
    Upgrades/
    VFX/
  Settings/
    Input/
    Network/
  ThirdParty/
    QuarterView3DActionBE5/
```

루트 문서/분석 파일은 `Assets` 밖에 둔다.

```text
BalanceReports/
Reports/
Documentation/
```

## 이동 우선순위

### 1단계: 체크포인트

현재 변경 파일이 많기 때문에 폴더 이동 전에 커밋 또는 별도 백업 체크포인트가 필요하다. 이동 작업은 변경 diff가 크게 나오므로, 밸런스/로직 수정과 섞이면 추적이 어려워진다.

### 2단계: CSV 정본 결정

현재 런타임은 `Resources/Data`를 읽는다. 따라서 빠른 안정 기준은 다음이다.

1. `Assets/Resources/Data`를 현재 런타임 정본으로 둔다.
2. `Assets/Data/Stages`의 다른 내용을 비교한다.
3. 필요한 값이 있으면 `Resources/Data`로 병합한다.
4. 병합 후 `Assets/Data/Stages` CSV는 삭제 또는 `Documentation/ArchivedTables`로 이동한다.

장기적으로는 `DataManager`를 `Resources.Load`에서 Addressables/serialized catalog/StreamingAssets 중 하나로 바꾸는 것도 가능하지만, 지금 단계에서는 범위가 커진다.

### 3단계: `Resources` 다이어트

`Resources`에는 코드가 직접 로드하는 파일만 남기는 방향이 좋다.

유지 후보:

- `Resources/Catalogs/*`
- `Resources/Configs/*`
- `Resources/Data/*.csv`
- `Resources/Sprites/UI/WhiteSquare`
- `Resources/Materials/M_LineDefault`

이동 후보:

- `Resources/QuarterView 3D Action BE5` -> `Assets/ThirdParty/QuarterView3DActionBE5`
- `Resources/Models` -> `Assets/Art/Models`
- `Resources/Textures` -> `Assets/Art/Textures`
- `Resources/Animations` -> `Assets/Art/Animations`
- `Resources/Materials` 중 런타임 로드가 아닌 것 -> `Assets/Materials/...`

### 4단계: 루트 에셋 정리

| 현재 | 이동 후보 |
| --- | --- |
| `Assets/DefaultNetworkPrefabs.asset` | `Assets/Settings/Network/DefaultNetworkPrefabs.asset` |
| `Assets/InputSystem_Actions.inputactions` | `Assets/Settings/Input/InputSystem_Actions.inputactions` |

`UserChoices.choices`는 Unity Multiplayer Play Mode 관련 파일일 수 있으니, 이동 전 패키지 참조 여부 확인이 필요하다.

### 5단계: 개발 산출물 분리

`Screenshots`, `_Recovery`가 실제 런타임 참조가 아니라면 `Assets` 밖으로 빼서 import/build 대상에서 제외한다.

## 안전 규칙

1. Unity 에셋 이동은 가능하면 Unity Editor/AssetDatabase로 한다.
2. 파일 시스템으로 이동해야 한다면 원본과 `.meta`를 반드시 같이 이동한다.
3. `Resources` 하위 파일은 이동 전 코드의 로드 경로를 먼저 바꾼다.
4. 프리팹/씬/스크립터블 오브젝트가 많이 바뀐 상태에서는 폴더 이동을 섞지 않는다.
5. 외부 에셋팩은 `Assets/ThirdParty`로 격리하되, 프리팹 참조가 깨지는지 Unity에서 확인한다.

## 바로 진행 가능한 작업

실제 이동 전, 안전하게 할 수 있는 작업은 다음이다.

1. `Resources.Load` 경로 목록을 `GAME_PLAN.md` 또는 별도 문서에 고정한다.
2. `Resources/Data`와 `Data/Stages` CSV 차이를 비교해서 정본을 정한다.
3. `Resources/QuarterView 3D Action BE5`가 런타임 로드되지 않는지 Unity 검색으로 한 번 더 확인한다.
4. 체크포인트 후 `ThirdParty`, `Settings`, `Reports` 폴더 기준으로 이동한다.

## 추천 진행안

지금은 Phase 9 들어가기 전이므로, 폴더 이동을 한 번에 크게 하지 말고 아래 순서로 쪼개는 게 좋다.

1. 현재 변경사항 저장/커밋
2. CSV 정본 병합
3. `Resources` 다이어트
4. 루트 에셋 정리
5. 외부 에셋팩 `ThirdParty` 이동
6. Unity 재임포트 후 콘솔/씬/프리팹 확인

이 순서가 가장 문제 추적이 쉽다.
