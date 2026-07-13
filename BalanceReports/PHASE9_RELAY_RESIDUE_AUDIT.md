# Phase 9 Relay Residue Audit

Generated: 2026-07-13

기준: 운영 접속은 Relay/Host 없이 Windows Dedicated Server + 직접 IP/LAN으로 고정한다. 이 문서는 Relay 제거 범위를 확정하기 위한 조사 기록이다.

## 결론

현재 실제 운영 경로는 이미 직접 IP 기반이다.

- 서버: `NetworkBootstrapper`가 `UNITY_SERVER` 또는 `-server`를 감지하고 `GameNetworkManager.StartAsServer(ip, port)` 호출
- 클라이언트: MainMenu/Lobby 입력값 `ip[:port]` → `LobbyViewModel.ConnectAsync` → `GameNetworkManager.StartAsClient(ip, port, nickname)`
- 접속 검증: `NetworkSessionService`가 `CatalogVersionUtility.GetHash()`를 ConnectionData에 넣고, 서버 approval에서 hash mismatch를 거부

Relay 잔재는 운영 경로에 직접 사용되지 않는 레거시 코드/씬 컴포넌트로 남아 있다.

## 삭제 후보

| 대상 | 위치 | 판단 | 처리 |
|---|---|---|---|
| `RelayManager.cs` | `Assets/Scripts/Network/RelayManager.cs` | `Unity.Services.Multiplayer`, `WithRelayNetwork()`, join code 기반 세션 생성/참여 전용 | Phase 9.0 또는 9.1에서 삭제 후보 |
| Bootstrap 씬 RelayManager 컴포넌트 | `Assets/Scenes/Bootstrap.unity` | `Assembly-CSharp::Vamsurlike.Network.RelayManager` 컴포넌트 참조 존재 | 스크립트 삭제 전 씬에서 컴포넌트 제거 필요 |
| `GameNetworkManager.StartAsRelayHost()` | `Assets/Scripts/Network/GameNetworkManager.cs` | Obsolete, 에러 로그만 출력 | 삭제 가능. API 참조 검색 후 제거 |
| `GameNetworkManager.StartAsRelayClient()` | `Assets/Scripts/Network/GameNetworkManager.cs` | `sessionService.StartRelayClient()`로 이어지는 레거시 경로 | 삭제 가능. UI 참조 없음 확인 필요 |
| `INetworkSessionService.StartRelayClient()` | `Assets/Scripts/Network/INetworkSessionService.cs` | Relay 전용 인터페이스 메서드 | 구현부와 함께 삭제 가능 |
| `NetworkSessionService.StartRelayClient()` | `Assets/Scripts/Network/NetworkSessionService.cs` | transport endpoint 설정 없이 `StartClient()` 호출하는 레거시 경로 | 삭제 가능 |

## 보류/별도 판단

| 대상 | 위치 | 판단 |
|---|---|---|
| UGS Authentication | `NetworkBootstrapper.InitializeUgsAsync()` | 현재 실패해도 로컬 전용 모드로 진행된다. Relay 삭제와 직접 관련은 약함. 향후 완전 로컬화 시 제거 검토 |
| UGS SDK/패키지 | `Packages/manifest.json` 확인 필요 | RelayManager 삭제 후 컴파일 의존성이 사라지면 패키지 제거 검토 가능. 단 다른 서비스가 쓰는지 먼저 확인 |
| `IpOrCodeInput` 이름 | `Assets/Scenes/MainMenu.unity` | 오브젝트명이 code 잔재처럼 보임. UI 표시 텍스트가 IP 기준이면 기능 문제는 없음. 나중에 씬 정리 때 이름 변경 후보 |
| `GAME_PLAN.md`의 Relay 언급 | 문서 | 완료 기준 제외/범위 밖/잔재 정리 문맥으로만 남기는 것은 의도적 |

## 제거 순서 제안

1. `rg -n "StartAsRelay|StartRelay|RelayManager|WithRelayNetwork|Unity.Services.Multiplayer" Assets/Scripts Assets/Scenes`로 참조 재확인.
2. Bootstrap 씬에서 `RelayManager` 컴포넌트 제거.
3. `RelayManager.cs` 삭제.
4. `GameNetworkManager`, `INetworkSessionService`, `NetworkSessionService`에서 Relay 메서드 삭제.
5. `dotnet build Assembly-CSharp.csproj` 및 Unity Console 확인.
6. MainMenu 직접 IP 접속 테스트: `127.0.0.1:7777`.
7. 패키지 의존성 확인 후 `Unity.Services.Multiplayer`/Relay 관련 패키지 제거 여부 결정.

## 주의

- Bootstrap 씬 컴포넌트 참조를 먼저 제거하지 않고 스크립트를 삭제하면 Missing Script가 남을 수 있다.
- Relay 관련 패키지를 먼저 제거하면 `RelayManager.cs`의 using 때문에 컴파일이 깨질 수 있다.
- Phase 9의 목표는 직접 IP 서버 안정화이므로, Relay 삭제는 기능 확장 작업이 아니라 빌드/운영 경로 단순화 작업으로 취급한다.

## 2026-07-13 재검색 결과

검색 기준:

- `Relay|relay|StartAsRelay|RelayManager|JoinAllocation|CreateAllocation|Allocation|RelayServerData`
- `Unity.Services|AuthenticationService|LobbyService|UGS|UnityTransport|SetRelayServerData`
- `StartAsRelayClient|StartAsRelayHost|StartRelayClient|CreateSessionAsync|JoinSessionAsync|RelayManager.Instance`
- UI/씬 이름의 `IpOrCode|CodeInput|Relay|세션 코드|코드`

### 실제 Relay 전용 코드

| 위치 | 내용 | 판단 |
| --- | --- | --- |
| `Assets/Scripts/Network/RelayManager.cs` | `Unity.Services.Multiplayer`, `WithRelayNetwork()`, `CreateSessionAsync`, `JoinSessionByCodeAsync`, `LeaveAsync` | 삭제 1순위 |
| `Assets/Scenes/Bootstrap.unity` | `Vamsurlike.Network.RelayManager` 컴포넌트가 Bootstrap 씬 오브젝트에 붙어 있음 | 스크립트 삭제 전 씬 컴포넌트 제거 필요 |
| `Assets/Scripts/Network/GameNetworkManager.cs` | `StartAsRelayHost()`, `StartAsRelayClient()` | 호출처 없음. 삭제 가능 |
| `Assets/Scripts/Network/INetworkSessionService.cs` | `StartRelayClient()` 인터페이스 메서드 | 구현부와 같이 삭제 가능 |
| `Assets/Scripts/Network/NetworkSessionService.cs` | `StartRelayClient()`가 endpoint 설정 없이 `StartClient()` 호출 | 레거시 경로. 삭제 가능 |
| `Packages/manifest.json` | `com.unity.services.multiplayer`: `1.0.0` | 위 코드 삭제 후 패키지 제거 후보 |

### Relay는 아니지만 UGS 잔재

| 위치 | 내용 | 판단 |
| --- | --- | --- |
| `Assets/Scripts/Network/NetworkBootstrapper.cs` | `UnityServices.InitializeAsync()`, `AuthenticationService.SignInAnonymouslyAsync()`, `IsUgsReady`, `OnUgsReady` | Relay와 직접 연결되지는 않음. 완전 로컬화할 때 별도 삭제 판단 |
| `Packages/manifest.json` | `com.unity.services.authentication`: `3.6.1` | `NetworkBootstrapper`의 Auth 제거 후 패키지 제거 후보 |
| `Assets/Resources/UnityPlayerAccountSettings.asset` | Authentication PlayerAccounts 설정 에셋 | Auth 패키지 제거 시 같이 정리 후보 |

### UI/씬 이름 잔재

| 위치 | 내용 | 판단 |
| --- | --- | --- |
| `Assets/Scenes/MainMenu.unity` | `IpOrCodeInput` 오브젝트 이름 | 실제 Relay 코드 입력 플로우는 아님. IP 입력칸 이름만 과거 흔적. `IpInput` 또는 `ServerAddressInput`로 rename 후보 |
| `Assets/_Recovery/0.unity` | `RelayManager` 컴포넌트 참조 | 복구 씬/백업 산출물. `Assets/_Recovery` 자체가 정리 후보 |

### 현재 호출 관계

현재 검색 기준으로 `RelayManager.Instance`, `CreateSessionAsync()`, `JoinSessionAsync()`를 호출하는 UI/게임플레이 코드는 발견되지 않았다. 즉 운영 경로는 이미 직접 IP 접속이고, Relay 관련 코드는 씬에 붙은 미사용 컴포넌트와 네트워크 래퍼의 레거시 API로 남은 상태다.

### 추천 삭제 순서

1. Bootstrap 씬에서 `RelayManager` 컴포넌트 제거.
2. `Assets/Scripts/Network/RelayManager.cs` 삭제.
3. `GameNetworkManager.StartAsRelayHost/StartAsRelayClient` 삭제.
4. `INetworkSessionService.StartRelayClient`, `NetworkSessionService.StartRelayClient` 삭제.
5. `GameNetworkManager.StartAsRelayHost`의 obsolete 메시지에서 Relay 언급도 제거.
6. 컴파일 확인.
7. 컴파일이 깨지지 않으면 `com.unity.services.multiplayer` 패키지 제거 검토.
8. UGS Authentication 제거 여부는 별도 결정. 제거한다면 `NetworkBootstrapper.InitializeUgsAsync`, `IsUgsReady`, `OnUgsReady`, `com.unity.services.authentication`, `UnityPlayerAccountSettings.asset`까지 함께 정리한다.


## 2026-07-13 삭제 완료

삭제/정리 완료:

- `Assets/Scripts/Network/RelayManager.cs` 및 `.meta` 삭제
- Bootstrap 씬의 `RelayManager` 컴포넌트 제거
- `Assets/_Recovery/0.unity`의 `RelayManager` 컴포넌트 제거
- `GameNetworkManager.StartAsRelayHost()`, `GameNetworkManager.StartAsRelayClient()` 삭제
- `INetworkSessionService.StartRelayClient()`, `NetworkSessionService.StartRelayClient()` 삭제
- `Packages/manifest.json`에서 `com.unity.services.multiplayer` 제거
- `Packages/packages-lock.json`에서 `com.unity.services.multiplayer`와 단독 전이 의존성(`qos`, `wire`, `deployment`, `deployment.api`) 제거

검증:

- `rg -n "Unity\.Services\.Multiplayer|RelayManager|StartAsRelay|StartRelayClient|WithRelayNetwork|JoinSessionByCodeAsync|CreateSessionAsync|com\.unity\.services\.multiplayer" Assets/Scripts Assets/Scenes Assets/_Recovery Packages/manifest.json Packages/packages-lock.json` 결과 없음
- `dotnet build Assembly-CSharp.csproj` 성공, 경고 0 / 오류 0

보류:

- `NetworkBootstrapper`의 UGS Authentication 초기화는 Relay 전용이 아니므로 이번 삭제 범위에서 제외
- `com.unity.services.authentication`, `UnityPlayerAccountSettings.asset`는 완전 로컬화 결정 후 별도 정리
