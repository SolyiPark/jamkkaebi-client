# 씬 개발 공통 규격

작성 기준: 2026-09-18 · 대상: Mobile2DUIPrototype

이 문서는 다른 채팅에서 개별 씬을 개발할 때 기존 UI 셸에 연결하기 위한 공통 계약이다. 각 씬의 기능 목록이나 구현 지시서는 포함하지 않는다. 이 문서 작성으로 게임 기능이 추가된 것은 아니다.

**구현됨**은 현재 프로젝트 코드에서 사용할 수 있다는 뜻이다. **설계 규약**은 Jamkkaebi_AppScaffold의 방향이며, 해당 서비스나 API가 이미 존재한다는 뜻이 아니다. 이후 변경된 코드와 이 문서가 다르면 차이를 먼저 확인하고 문서도 함께 갱신한다.

## 1. 작업 위치와 기준

| 항목 | 기준 |
|---|---|
| 실행 프로젝트 | `D:\프로젝트\c3\Mobile2DUIPrototype` |
| 기능 명세 원본 | `D:\프로젝트\c3\Jamkkaebi_AppScaffold` |
| Unity | 6000.3.14f1 |
| 렌더링 / UI | Built-in / uGUI 2.0.0 |
| 입력 | 기존 Input Manager + StandaloneInputModule |
| 현재 연결 코드 namespace | `MobilePrototype` |
| 화면 방향 | 모바일 세로 화면 |
| 시작 씬 | `Assets/Scenes/UIPrototype.unity` |
| 탭 설정 | `Assets/Configuration/MainTabs.asset` |

Scaffold의 `.cs.md`는 명세와 의사 코드다. 확장자만 `.cs`로 바꾸지 않는다. 원본의 Unity 버전·Input System·Canvas 예시 설정보다 현재 프로젝트의 연결 규격을 우선한다. 공통 환경 변경은 개별 씬 개발에 포함하지 않는다.

기능 구현 전 Scaffold에서 다음을 읽는다.

- `README.md`: 명세의 상태와 해석 방법.
- `Registry/scope-and-sources.md`, `Registry/decisions.md`: 범위와 결정 사항.
- `Registry/g5-type-owners.md`: 공통 타입의 소유 파일.
- `WorkOrders/AI-conversion.md`: 실제 코드로 변환하는 규칙.
- 맡은 기능의 `.cs.md`와 해당 단계의 UI·Composition 작업 명세.

## 2. 셸과 콘텐츠 씬의 책임

```text
UIPrototype (항상 유지되는 셸)
 ├─ 상단 프로필 표시: 홈에서만 노출
 ├─ 중앙 RawImage: 선택된 씬 카메라의 RenderTexture 표시
 ├─ MirrorInput: 중앙 영역의 입력을 선택된 씬에 전달
 └─ 하단 탭: TabHost + MainTabs

콘텐츠 씬들 (Additive 로드, 각자 Local Physics2D)
 └─ 최상위 MirrorSceneRoot
     ├─ 전용 Camera
     ├─ 콘텐츠 UI Canvas
     └─ 월드 오브젝트 / 씬 전용 동작
```

콘텐츠 씬은 중앙 영역만 만든다. 공통 하단 탭, 홈 프로필바, 전역 EventSystem, Safe Area 처리를 복제하지 않는다. 씬마다 전체 앱을 초기화하는 Bootstrap이나 전역 데이터 저장소를 만들지 않는다.

현재 연결은 다음과 같다. ID의 대소문자와 기존 자산 GUID를 유지한다.

| 탭 ID | 표시 이름 | 콘텐츠 씬 | 공통 프로필바 |
|---|---|---|---|
| `Home` | 홈 | `Assets/Scenes/Home.unity` | 표시 |
| `Workshop` | 작업대 | `Assets/Scenes/Workshop.unity` | 숨김 |
| `Collection` | 도감 | `Assets/Scenes/Collection.unity` | 숨김 |
| `Friends` | 친구 | `Assets/Scenes/Friends.unity` | 숨김 |

네 씬은 현재 더미 콘텐츠를 가진다. 친구 탭의 존재는 소셜 기능 구현 지시가 아니다. Scaffold에서 제외한 로그인·친구·선물·방문 기능을 임의로 추가하거나 기존 탭을 제거하지 않는다.

## 3. 씬의 필수 구조와 등록

1. 씬의 **최상위 GameObject**에 `MirrorSceneRoot`를 둔다. 호스트는 최상위 오브젝트에서 이 컴포넌트를 찾으므로 자식에만 배치하면 안 된다.
2. 콘텐츠용 최상위 루트는 하나로 통일하고, 카메라·Canvas·월드 오브젝트·씬 전용 실행 컴포넌트는 그 아래에 둔다. 루트 밖의 실행 오브젝트는 일시 정지 제어에서 빠진다.
3. `MirrorSceneRoot.sceneCamera`에 전용 카메라를 연결한다. 기존 더미 씬의 카메라와 Canvas 연결을 출발점으로 삼는다.
4. 콘텐츠 Canvas는 전용 카메라를 통해 RenderTexture에 렌더링되도록 구성한다. Screen Space Overlay로 셸 바깥에 직접 그리지 않는다.
5. 콘텐츠 씬에 별도 EventSystem이나 AudioListener를 추가하지 않는다. 루트 설정 시 자식 AudioListener는 비활성화된다.
6. 기존 씬을 수정하면 경로와 `.meta`를 유지한다. 새 탭 씬을 추가하는 경우에만 공통 담당 작업으로 Catalog와 빌드 목록을 함께 변경한다.

`MainTabs`의 각 항목은 고유한 `id`와 고유한 `scenePath`를 가져야 하며, 씬은 빌드 목록에 등록되어 있어야 한다. 현재 호스트는 1~24개 탭을 지원하고, 탭 순서에 따라 레이어 8~31을 할당한다.

`Prototype > Sync Build Scenes From Catalog`는 빌드 목록을 **셸 + Catalog의 씬들로 다시 작성한다.** Catalog에 없는 상세 화면용 씬을 별도로 추가했다면 이 메뉴로 등록이 사라질 수 있다. 상세 화면마다 새로운 Unity Scene을 만드는 구조는 현재 계약에 포함되지 않으므로 먼저 통합 설계가 필요하다.

**`PrototypeSetup.Create`를 재실행하지 않는다.** 더미 씬과 Catalog를 재생성하여 다른 작업의 변경을 덮어쓸 수 있다.

## 4. 화면 크기·레이어·동적 생성

셸의 Canvas 기준은 1080×1920, 너비 기준 스케일이다. 상단 높이 178, 하단 높이 124는 셸의 UI 단위이며 실제 기기 픽셀 수가 아니다. 홈 외 탭은 상단 프로필 영역까지 콘텐츠가 확장된다. Safe Area는 셸이 적용한다.

기존 콘텐츠 Canvas는 1080×1600, 너비 기준 스케일을 사용한다. 이것을 고정 출력 해상도로 가정하지 않는다. 중앙 영역의 비율에 따라 RenderTexture 크기가 바뀌므로 Anchor·Layout으로 대응한다. 입력 도중 크기가 바뀌면 현재 입력은 취소될 수 있다.

카메라의 `targetTexture`, `cullingMask`, `enabled`와 RenderTexture 생성·해제는 호스트 소유다. 콘텐츠가 덮어쓰거나 해제하지 않는다. 레이어 번호도 콘텐츠에서 고정하지 않고 `MirrorSceneRoot.RenderLayer`를 사용한다.

동적 월드 오브젝트는 다음 규칙을 따른다.

- `Configure`가 완료된 후 생성·등록한다. `Awake`에서 `RenderLayer`가 이미 설정되었다고 가정하지 않는다. 현재 별도의 준비 완료 이벤트는 없다.
- 인스턴스가 다른 씬에 생성되었다면, 부모가 없는 상태에서 `SceneManager.MoveGameObjectToScene(obj, root.gameObject.scene)`으로 소속을 옮긴다.
- 이어서 `root.RegisterSpawnedObject(obj)`를 호출하여 루트 아래에 배치하고 레이어를 맞춘다. 이 메서드는 씬 소속 이전이나 Raycaster 재수집까지 해주지는 않는다.
- 기존 Canvas 아래에 UI 항목을 생성하는 방식은 사용할 수 있다. 새로운 Canvas/GraphicRaycaster를 실행 중 추가하면 입력 목록에 자동 등록되지 않는다. 필요한 경우 공통 등록 API부터 확장해야 한다.

현재 2D 월드 입력은 XY 평면의 z=0을 기준으로 한다. Scaffold의 공간 예시를 근거로 XZ 또는 3D 입력 구조로 임의 전환하지 않는다.

## 5. 입력 계약

입력 경로는 `셸 RawImage → MirrorInput → 선택된 씬의 UI 또는 Collider2D`다. 숨겨진 씬은 기본 정책에서 계속 실행되므로, 개별 씬이 `Input`을 직접 폴링하면 비선택 씬까지 반응할 수 있다. 중앙 상호작용은 전달된 이벤트를 사용한다.

| 항목 | 현재 지원 범위 |
|---|---|
| 입력 장치 | 단일 터치 또는 마우스 왼쪽 버튼 |
| UI 대상 | 루트 설정 시 수집된 GraphicRaycaster의 UI |
| 월드 대상 | UI에 맞지 않았을 때 해당 씬의 Collider2D |
| 이벤트 | PointerDown / PointerUp / PointerClick / InitializePotentialDrag / BeginDrag / Drag / EndDrag |
| 좌표 | 전달된 `PointerEventData.position`은 RenderTexture 픽셀 좌표 |
| 입력 취소 | 탭 변경, 브리지 비활성화, 포커스 상실, RenderTexture 크기 변경 등 |

월드 좌표로 변환할 때는 해당 씬의 명시적 카메라 참조를 사용한다. `Camera.main`이나 원래 전체 화면 좌표를 전제로 계산하지 않는다. 월드 Collider 대상 이벤트의 Raycast module은 없을 수 있으므로 `eventCamera` 의존도 피한다.

장식용 UI의 Raycast Target은 끄고, 입력을 받아야 하는 요소와 의도적으로 뒤를 막는 영역만 켠다. 겹친 월드 Collider의 세밀한 선택 우선순위는 아직 별도 계약이 없다.

멀티터치·핀치·휠·PointerEnter/Exit·Drop·키보드/텍스트 입력은 현재 보장하지 않는다. ScrollRect, InputField 등도 자동으로 완전 지원된다고 가정하지 않는다. 필요한 기능을 맡은 작업에서 공통 입력 확장 의존성으로 명시하고 검증한다. 드래그 상태는 정상 종료뿐 아니라 취소·비활성화에도 정리해야 한다.

## 6. 실행 수명과 탭 이동

시작 시 네 씬을 모두 순서대로 Additive 로드한다. 선택된 카메라만 렌더링하고, 입력도 선택된 씬에만 전달한다. 씬 로드는 선택될 때마다 발생하는 것이 아니다.

| `BackgroundPolicy` | 숨겨졌을 때 | 다시 선택했을 때 |
|---|---|---|
| `ContinueRunning` — 기본 | 루트 활성, 동작·로컬 2D 물리 유지 | 기존 상태 그대로 표시 |
| `PauseWhileHidden` | 루트 비활성 | 루트 재활성, 메모리 상태 유지 |
| `RestartOnReturn` | 루트 비활성 | 이미 방문한 씬을 언로드·재로드 |

`PauseWhileHidden`은 모든 외부 실행을 중지하는 기능이 아니다. 앱 공통 서비스·외부 Task 등은 별도 수명을 가진다. 루트 비활성화로 중단되는 코루틴 등은 재활성화 시 필요한 재개 처리를 설계한다.

기본 정책에서는 탭이 숨겨져도 `OnDisable`이 호출되지 않는다. 화면 노출 여부와 GameObject 활성 여부를 같은 것으로 취급하지 않는다. 현재 공개된 탭 표시/숨김 이벤트나 전환 전 저장 확인 API는 없다.

Local Physics2D는 호스트가 `FixedUpdate`에서 루트를 통해 수동 진행한다. 개별 씬에서 중복 `Simulate` 호출, 전역 `Time.timeScale` 변경, Single 방식 `LoadScene`으로 앱 전체 교체를 하지 않는다.

Inspector의 현재 씬 재시작은 콘텐츠 씬을 다시 로드한다. 앱 데이터 초기화·재화 초기화와는 별개다. 씬 언로드 시 이벤트 구독과 씬 소유 자원을 해제하고, 완료가 늦은 비동기 작업이 파괴된 View에 결과를 적용하지 않도록 한다.

앱이 OS 백그라운드로 이동하는 것은 탭이 숨겨지는 것과 다르다. `ContinueRunning`은 모바일 OS 백그라운드 실행 보장이 아니다.

## 7. 현재 사용할 수 있는 공통 API

| 소유자 | 현재 API / 데이터 | 용도 |
|---|---|---|
| `TabHost` | `SelectTab(int index)` | 탭 선택 요청. 준비 전에는 무시될 수 있음 |
| `TabHost` | `RestartCurrent()` | 준비 완료·비처리 상태에서 현재 씬 재시작 |
| `TabHost` | `ActiveIndex`, `ActiveRoot`, `IsReady`, `IsBusy`, `GetRoot(int)` | 현재 호스트 상태 조회 |
| `MirrorSceneRoot` | `RenderLayer`, `IsPaused`, `RegisterSpawnedObject(GameObject)` | 콘텐츠의 레이어·동적 자식 관리 |
| `ProfileDataSource` | `Current`, `Changed`, `SetProfile(ProfileData)` | 프로필 표시 데이터 공급 |
| `ProfileData` | `nickname`, `consecutiveDays`, `avatar` | 닉네임·연속 일수·프로필 이미지 |
| `TabDefinition` | `id`, `title`, `scenePath`, `showProfile`, 각 Sprite/Color, `backgroundPolicy` | 탭 구성과 표시 정책 |

프로필바는 공급된 데이터를 표시한다. 연속 일수 계산, 로그인, 서버 사용자 정보 조회를 구현한 것은 아니다. 콘텐츠 씬이 헤더 Text를 직접 찾아 수정하지 않는다.

`Configure`, `SetPaused`, `Simulate`, `MirrorInput.Bind` 등은 연결 계층의 책임이다. 개별 씬 기능에서 직접 호출해 호스트 상태와 어긋나게 만들지 않는다.

## 8. 데이터와 서비스의 설계 규약 — 아직 공통 구현이 아님

Scaffold의 `AppNavigator`, `PlayerSnapshotStore`, `CommandCoordinator`, `AppCompositionRoot` 등을 현재 프로젝트에서 호출 가능한 서비스로 취급하지 않는다. 실제 코드 존재 여부와 생성·주입 위치를 확인한 뒤 사용한다.

향후 기능을 구현할 때 지킬 방향은 다음과 같다.

- 재화·보유품·성장·배치 등 공유 데이터는 앱 수명의 단일 기준 상태에서 읽는다. 각 씬에 서로 독립적인 진짜 잔액이나 인벤토리를 만들지 않는다.
- View는 표시와 사용자 의도 전달을 맡고, 요청 처리·상태 반영은 서비스/Presenter 등 기능 명세가 지정한 계층에서 담당한다.
- 공통 DTO·enum은 `g5-type-owners.md`의 소유권을 확인한다. 같은 의미의 타입을 씬마다 중복 정의하지 않는다.
- 서비스를 아직 연결할 수 없다면 명시적인 개발용 Fake 또는 주입 가능한 인터페이스로 경계를 만든다. 임시 데이터임을 표시하고 실제 연동 지점을 인계한다.
- 로딩·빈 결과·오류·정상 상태를 구분한다. 미확정 수치나 조회 실패를 0원·무료·보유 없음으로 해석하지 않는다. 미정 비용은 `--` 등으로 표시하고 해당 실행을 이유와 함께 비활성화한다.
- 보상·소비는 확정 결과를 기준으로 반영한다. 버튼 클릭만으로 씬이 보상을 직접 지급하지 않는다.
- 결과가 불명확한 요청은 확정 실패와 구분한다. Scaffold의 요청 ID 재사용·전송 전 기록·앱 수명 처리 규칙을 적용해야 하는 기능은 해당 조정 계층까지 구현 범위를 명시한다. 화면을 떠났다는 이유로 거래 처리 자체를 취소하지 않는다.
- View의 구독·화면용 비동기 작업과 앱 공통 요청의 수명을 분리한다. 씬 재시작 후에는 공통 상태를 다시 읽어 표시한다.

Scaffold 코드 변환 결과는 지정된 `Assets/Jamkkaebi/...` 위치와 namespace를 따른다. 기존 `MobilePrototype` 연결 계층과의 어댑터가 필요하면 그 경계를 명시한다. 기존 연결 계층을 한꺼번에 이름 변경하거나 대체하지 않는다.

## 9. 공통 확장이 필요한 경우

다음은 현재 제공되는 기능이 아니다. 필요한 씬에서 독자적인 전역 구현을 만들지 말고, 공통 의존성으로 기록하여 통합한다.

| 필요 기능 | 통합 시 정할 내용 |
|---|---|
| 상세 화면 / 뒤로 가기 | 화면 경로, 스택, 탭 유지 여부, 시스템 뒤로 가기 |
| 편집 중 이탈 확인 | 비동기 전환 허용/거부, 저장 결과 불명 상태의 처리 |
| 공통 팝업 | 표시 소유자, 입력 차단, 중복 열기와 닫기 규칙 |
| 탭 노출에 따른 기능 정지 | 표시/숨김 신호와 기능별 정지 이유 |
| 추가 입력 | 제스처, 텍스트 입력, 동적 Raycaster 등록 등 |
| 앱 서비스 연결 | 단일 생성 위치, 인터페이스 소유자, Fake 교체 방식 |

특히 Scaffold의 배회 동작 정지 사유(Hidden/Modal/Edit/Background/InvalidLayout)를 그대로 적용하면 사용자가 지정한 기본 계속 실행 정책과 차이가 생길 수 있다. 씬 전체 수명과 특정 기능의 정지 정책을 분리하여 결정한다.

## 10. 다른 채팅의 작업 경계와 인계

개별 작업은 지정받은 콘텐츠 씬, 그 씬의 Prefab·아트·기능 코드에 집중한다. `Assets/Scripts`의 공통 연결 코드, `MainTabs.asset`, 패키지, ProjectSettings, 빌드 목록은 공통 통합 영역이다. 수정이 꼭 필요하면 변경 이유와 영향받는 씬을 명시한다.

공유 폴더에서 다른 채팅도 작업할 수 있으므로 시작 시 기존 변경을 확인한다. 담당 외 씬을 저장하거나 타인의 변경을 덮어쓰지 않는다. `.meta`를 보존하고 `Library`, `Temp`, 빌드 산출물을 소스처럼 복사하지 않는다.

완료 시 다음을 검증·인계한다.

- [ ] 시작 씬에서 실행하여 해당 탭이 렌더링되고 다른 탭도 정상 진입한다.
- [ ] 중앙 영역 안에서 터치/마우스 클릭과 드래그가 동작하고, 숨겨진 씬에는 입력이 전달되지 않는다.
- [ ] 홈 프로필바와 공통 하단 UI를 중복 생성하지 않았으며 화면 비율 변화에도 레이아웃이 유지된다.
- [ ] 기본 계속 실행, 숨김 일시 정지, 복귀 재시작 각각의 상태와 이벤트 정리가 의도대로다.
- [ ] 동적 오브젝트가 올바른 Scene·루트·레이어에 있고 UI를 가로채는 장식 Raycast Target이 없다.
- [ ] 씬 재시작·탭 이동 후 중복 구독, 중복 보상, 오래된 비동기 결과의 화면 적용이 없다.
- [ ] 필요한 Fake, 미정 수치, 미연결 서비스, 공통 확장 요구를 명시했다.
- [ ] 변경 파일, Inspector 연결, 검증 방법·결과, 검증하지 못한 항목을 기록했다.

기존 `PrototypeVerification`은 원래 네 더미 씬의 구성과 컴포넌트를 전제로 한다. 콘텐츠 교체 후 기존 검증 결과를 새 씬의 성공 근거로 재사용하지 않는다. 변경된 요구에 맞는 검증을 적용하고 실제 모바일 확인 여부를 구분한다.

다른 채팅에 전달할 작업 지시 예시:

> 프로젝트 루트의 SCENE_DEVELOPMENT_CONTRACT.md와 현재 코드를 확인한 뒤, 지정한 씬의 기능을 Jamkkaebi_AppScaffold 명세에 따라 구현해줘. 기존 셸 연결을 유지하고 맡은 기능 범위만 구현해줘. 미구현 공통 서비스는 존재한다고 가정하지 말고, 필요한 인터페이스·개발용 Fake·통합 의존성을 구분해줘. 완료 시 변경 파일, 연결 방법, 검증 결과와 남은 의존성을 정리해줘.

## 근거 파일

프로젝트 내부 경로는 이 문서가 있는 프로젝트 루트를 기준으로 한다.

- [TabCatalog.cs](Assets/Scripts/TabCatalog.cs)
- [TabHost.cs](Assets/Scripts/TabHost.cs)
- [MirrorSceneRoot.cs](Assets/Scripts/MirrorSceneRoot.cs)
- [MirrorInput.cs](Assets/Scripts/MirrorInput.cs)
- [Scaffold README](../Jamkkaebi_AppScaffold/README.md)
- [Scaffold 공통 타입 소유권](../Jamkkaebi_AppScaffold/Registry/g5-type-owners.md)
- [Scaffold 코드 변환 규약](../Jamkkaebi_AppScaffold/WorkOrders/AI-conversion.md)
