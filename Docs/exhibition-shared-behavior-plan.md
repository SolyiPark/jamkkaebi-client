# 홈·친구 방문 전시관 공용 동작과 화면 정책 정리

설계 기준: 2026-11-05, 클라이언트 `fd6385b`(PR #25). 아래 제안은 작업 전 설계 기록이다. 같은 날 사용자 승인 후 화면 정책·공용 선택·공용 카메라와 선택 포커스 기반을 구현했다. 현재 상태는 마지막 구현 기록과 [검증 기록](exhibition-shared-behavior-validation.md)을 따른다. 여기서 방문은 서버/모의 스냅샷을 표시하는 친구 전시관을 뜻한다.

## 목적과 공유 여부의 기준

홈에 기능을 추가했다고 방문에 자동 적용되지는 않는다. 두 화면이 동일한 구현을 호출하고 필요한 컴포넌트·자산을 연결했을 때 공유된다. 동일한 플래그 이름이나 복사한 코드는 공유 구현이 아니다.

권장 구조는 **공용 기능 구현 + 화면별 기능 정책 + 화면별 데이터 어댑터**다. 기능별로 구현 파일, 홈/방문 호출 지점, 정책 자산, 테스트를 명시한다. 각 화면은 별도 카메라와 런타임 상태를 가지며 코드만 공유한다. 공유 ScriptableObject에는 설정만 두고 선택 대상·카메라 위치·요청 세대 등 실행 중 상태를 저장하지 않는다.

## 현재 구현과 유지할 경계

| 영역 | 현재 구조 | 후속 처리 |
| --- | --- | --- |
| 좌표 | 공용 `ExhibitionGrid.asset`, `ExhibitionGrid`, `ExhibitionSurface` | 유지. 상판 보정과 7×7 기준 변경 금지 |
| 깊이 | 공용 `ExhibitionDepth` | 유지. 바닥 접점으로 정렬 |
| 배경 | `ExhibitionLayer`, `ExhibitionArtworkSetup` 공유, 홈 씬과 방문 프리팹은 별개 | 공용 코드 변경과 개별 자산 변경의 적용 범위를 구분 |
| 카메라 | 홈 `ExhibitionView`, 방문 `FriendExhibitionRebuilder`에서 각각 매 프레임 설정 | 공용 카메라 컴포넌트로 계산·갱신 추출 권장 |
| 선택 | 홈 `HousingSurfaceInput`/`ExhibitionHousing`, 방문 `FriendExhibitionInput`/재구축기 | 화면별 판정 결과를 공용 선택 표현에 전달. 판정 자체의 통합은 후속 단계 |
| 이동 | 홈 `DokkaebiWanderer`/`HousingLayout`, 방문 `ExhibitionVisitScene.Walk`/재구축기 보간 | 당장은 분리 유지. 점유·시간 기준·저장 앵커가 다름 |
| 편집 | 홈 `ExhibitionHousing` | 홈 전용. 방문에 붙이지 않음 |
| 서버·소셜 | 방문 세션·backend·`FriendsPresenter` | 화면 전용으로 유지. 표현 기능과 분리 |

현재 홈의 짧은 터치는 셀 처리이며, 길게 누르기는 편집 메뉴다. 방문의 클릭 선택과 동일한 의미의 선택 이벤트가 홈에 이미 있는 것으로 가정하면 안 된다. 홈 NPC도 현재 하우징 전시물과 같은 선택 대상으로 자동 제공되지 않는다.

## 제안하는 화면별 정책

신규 후보: `Scripts/UI/Exhibition/ExhibitionBehaviorPolicy.cs`(ScriptableObject), `Configuration/HomeExhibitionBehavior.asset`, `Configuration/VisitExhibitionBehavior.asset`. 이름은 제안이며 새 코드·자산에는 `.meta`를 포함한다. 아래 bool은 명명 예시이며 `[Flags]` 비트마스크 사용을 요구하는 것은 아니다.

| 정책 예시 | 홈 | 방문 | 비고 |
| --- | --- | --- | --- |
| `AllowObjectSelection` | 후속 도입 시 켬 | 켬 | 홈의 짧은 터치 의미를 먼저 정함 |
| `EnableSelectionFocus` | 초기 끔 | 초기 끔 | 카메라 확대는 대화의 예시. 구현·승인 후 켤 수 있음 |
| `AllowLayoutEditing` | 켬 | 끔 | 입력과 실제 편집 명령 양쪽에서 확인 |
| `PlayIntroduction` | 켬 | 끔 | 홈 최초 등장 수명 유지 |
| `EnableTabParallax` | 켬 | 끔 | 방문은 시차 없음 |
| `EnableAmbientMovement` | 켬 | 켬 | 허용 여부만 정책화. 같은 이동 구현이라는 뜻은 아님 |
| `LockNavigationDuringVisit` | 해당 없음 | 켬 | 방문 진입~이탈 수명에서 적용 |

정책이 기능 구현을 대신하지 않는다. 방문은 정책이 잘못 켜져도 편집 서비스 자체를 연결하지 않으며 `ExhibitionVisitSession.CanEdit == false`를 유지한다. 보상·선물 가능 여부는 서버/일일 상태에서 결정하므로 정적 정책으로 승인하지 않는다. 모의/HTTP 여부도 별도 데이터 공급 경로이며 조작 모드 플래그와 섞지 않는다.

## 파일과 수정할 코드

경로는 `Assets/Jamkkaebi/` 기준이며 메서드명은 현재 코드 기준이다.

| 파일 | 수정 후보와 책임 |
| --- | --- |
| 신규 `Scripts/UI/Exhibition/ExhibitionCameraController.cs` | 화면비 기반 기본 크기, 중심·줌·포커스 전환, 복귀·취소를 한 곳에서 처리. 카메라 Transform/orthographicSize의 단일 갱신자 |
| `Scripts/UI/Exhibition/ExhibitionView.cs` | `SetView`의 중심·줌 요청과 `LateUpdate`의 카메라 계산을 공용 카메라로 위임. `NotifyVisible`, `SetTransitionOffset` 및 홈 등장·시차 수명은 유지 |
| `Scripts/UI/Friends/FriendExhibitionRebuilder.cs` | `LateUpdate`의 카메라 직접 설정을 제거·위임. `Visit`/`Leave`에 포커스 초기화·취소·기존 시점 복구를 연결. 기존 조회·세대·객체 수명은 유지 |
| 신규 `Scripts/UI/Exhibition/ExhibitionSelectionController.cs` | 선택 대상 수명·변경/해제 이벤트, 강조와 포커스 요청을 공용 처리. `VisitObject`나 `HousingPlacement`에 종속시키지 않음 |
| 선택 표현 계약(필요하면 신규 별도 파일) | 화면별 객체를 Transform/접점/선택 영역 등 표현 정보로 전달. 종류 ID와 인스턴스 ID를 혼용하지 않음. 이동 중 대상 추적·파괴 시 해제 규칙 포함 |
| `Scripts/UI/Exhibition/HousingSurfaceInput.cs` | `OnPointerClick`에 홈 선택 경로를 연결하되, 배치/이동 대기에서는 기존 `SelectCell`을 우선. 길게 누르기·드래그 뒤 클릭 취소와 `OwnsGesture` 유지 |
| `Scripts/UI/Exhibition/ExhibitionHousing.cs` | `ItemAtTexturePoint` 결과를 표현 대상으로 바꾸는 어댑터 제공. `SelectCell`/`ExecuteAction`/`BeginPlacement`/회수 경로에 편집 정책 검사. `_selectedId`는 편집 상태이므로 공용 화면 선택과 무조건 합치지 않음 |
| `Scripts/UI/Friends/FriendExhibitionRebuilder.cs` | `SelectAtTexturePoint`의 판정 결과를 공용 선택기로 전달. 현재 알파 강조를 추출할 경우 여기서 중복 실행하지 않음. `ObjectSelected` 호환 이벤트와 로딩 중 선택 금지 유지 |
| `Scripts/UI/Friends/FriendExhibitionInput.cs` | 기존 RT 좌표 전달 유지. 정책 검사 위치를 공용 선택기와 중복·불일치 없이 정함 |
| `Scripts/UI/Friends/FriendsPresenter.cs` | `OnObjectSelected`의 하단 정보 갱신 유지. 선택 해제·스냅샷 교체 때 상세 상태를 정리. 카메라 제어를 Presenter에 추가하지 않음 |
| `Scenes/Home.unity`, `Scenes/Friends.unity` | 각 화면 카메라·정책·선택기의 참조 연결. 같은 구현을 쓰되 각각 독립 인스턴스 사용 |
| `Prefabs/Friends/FriendExhibitionStage.prefab` | 방문 표면·콘텐츠·입력 연결 필요 범위만 직접 수정 |
| `Editor/FriendsSetup.cs` | `ValidateVisitStage`에 신규 필수 참조·정책 검증 추가. 일회성 이관 코드의 미래 생성 결과도 필요 시 맞추되 Apply/Prepare 재실행 금지 |
| `Scripts/UI/Shell/TabTransition.cs` | 현재 `ExhibitionView.SetTransitionOffset` 호출을 확인하되 초기에는 유지. 정책은 홈 표현 계층에서 반영하고 셸에 친구 전용 분기 추가 금지 |

**카메라 덮어쓰기 주의:** 현재 `ExhibitionView.LateUpdate`는 줌/중심을 적용하고, `FriendExhibitionRebuilder.LateUpdate`는 기본 크기와 `(0,0,-10)`을 매번 설정한다. 선택 이벤트에서 일회성 확대만 추가하면 다음 프레임에 덮어써진다. 반드시 기존 두 경로의 직접 쓰기를 공용 카메라로 위임한 뒤 포커스를 연결한다. 배경 레이어 적용은 갱신된 카메라 상태 이후 실행하도록 순서를 명시한다.

홈에는 `ExhibitionIntro` 의존성이 있는 `ExhibitionView`가 있다. 이 클래스를 그대로 방문에 붙여 공통화하지 말고, 홈 등장과 분리할 수 있는 카메라 계산부터 추출한다. 방문 카메라 복귀는 현재 `Leave`의 보존 계약과 중복되지 않도록 소유자를 하나로 정한다.

## 권장 구현 순서와 확인 사항

1. 기능별 공유 목록·화면 정책을 정의한다. 홈의 짧은 터치, 빈 공간 터치, 재선택 시 복귀, 이동 NPC 추적 여부는 아직 확정되지 않은 UX이므로 구현 전에 정한다.
2. 카메라 계산을 공용화하되 현재 화면 결과를 유지한다. 현재 `ExhibitionView.SetView` 호출자가 깨지지 않도록 위임 API를 유지한다.
3. 화면별 객체 판정은 유지하면서 선택 결과·해제·강조를 공용화한다. 홈 편집 선택과 화면 선택은 분리한다.
4. 선택 확대를 실제 도입할 때 양쪽 선택 이벤트를 같은 카메라 기능에 연결하고 정책으로 활성화한다. 새 플래그가 꺼진 상태에서도 기존 기능이 유지되는지 확인한다.
5. 이동 공통화는 별도 범위로 검토한다. 홈 NPC 현재/목적지 예약, 고정 PlacementCell, 방문의 다중 객체 예약과 불변 Anchor를 먼저 동일한 계약으로 설명할 수 있어야 한다.

정책/순수 계산은 관련 EditMode 테스트로 확인한다. 카메라·입력·씬 연결 변경 시에는 Player에서 양쪽 화면의 선택/확대/복귀, 앞쪽 객체 선택, 줌 후 RT→월드 입력, 홈 편집·길게 누르기·스와이프, 방문 중 탭 잠금, 로딩·오류·재시도·이탈 및 늦은 응답을 확인한다. 480×1040·768×1024에서 HUD·배경·카메라 범위를 확인하고 내 홈·원본 스냅샷의 불변을 유지한다. UI 표시 정책을 Gameplay 모델에 참조시키지 않는다.

위 설계안 작성 당시에는 문서 작성과 코드 확인만 수행했다. 이후 실제 구현·검증은 아래 기록과 구분한다.

## 2026-11-05 공용 동작 구현

사용자 승인 UX는 홈·방문 짧은 터치로 전시물/NPC 선택, 같은 대상 재터치·빈 공간 터치로 해제다. 홈의 배치/이동 목적지 대기에는 기존 셀 선택을 우선하며 길게 누르기·드래그 뒤 클릭은 취소한다. 홈 편집 선택과 화면 선택은 분리하고 편집 진입 때 화면 선택을 해제한다.

- `ExhibitionBehaviorPolicy`와 `HomeExhibitionBehavior.asset`/`VisitExhibitionBehavior.asset`는 설정만 보관한다. 홈은 편집·등장·탭 시차를 허용하고, 방문은 금지한다. 이동·객체 선택은 양쪽 허용, 선택 포커스는 양쪽 기본 꺼짐이다. 방문 중 탭 잠금은 기존 방문 수명이 소유한다.
- `ExhibitionCameraController`는 카메라 위치·크기·포커스·방문 전 시점 보존/복구를 소유한다. `ExhibitionView.SetView` API는 유지했다. 홈의 Update 단계 NPC 이동 이후 LateUpdate에서 카메라→배경을 적용하고, 방문은 이동 보간→카메라→배경 순서다.
- 포커스는 켰을 때 선택 대상 바닥 접점을 추적하며 해제하면 기준 중심·줌으로 복귀한다. 기본 포커스 줌 배율은 1.5, 전체 줌 제한은 .75~2, 전환 시간은 .15초이며 컨트롤러 설정이다. `ExhibitionLayer`의 종이 배경은 이동한 시점을 포함해 화면 덮임을 계산한다.
- `ExhibitionSelectionTarget`는 표현 인스턴스 ID/Transform 계약이고, `ExhibitionSelectionController`가 선택 변경/해제·원래 알파 보존·포커스 요청을 담당한다. 홈은 하우징 배치 ID/NPC 런타임 ID를, 방문은 기존 Placement.Key를 어댑터에서 전달한다. 종류 ID나 서버 ID를 새로 추정하지 않는다.
- 홈의 실제 편집 명령에도 정책 검사를 추가했고, 방문에는 편집 서비스를 연결하지 않았다. `ExhibitionVisitSession.CanEdit`는 계속 false다. 이동 구현은 분리한 채 허용 여부만 반영하고 예약/고정 저장 앵커를 보존한다.
- `FriendsPresenter`의 자동 첫 객체 상세 표시는 제거했다. `ObjectSelected`는 선택 해제 시 null을 전달하며 로딩/스냅샷 교체/이탈 때 상세도 정리한다. GET/보상 POST 분리, 세대 검사, 인증 수명은 유지했다.
- `Home.unity`/`Friends.unity`에 각각 독립 컴포넌트를 직접 연결했다. 기존 방문 프리팹 입력 연결은 충분해 변경하지 않았다. `FriendsSetup.ValidateVisitStage`가 신규 참조와 `ExhibitionBehaviorChecks`를 검사한다. 일회성 생성 코드의 향후 결과도 맞췄으나 Prepare/Apply는 실행하지 않았다.

단계별 EditMode와 씬·회귀 검사 후 모의 Player 377개, 마지막 보강 후 실제 HTTP EditMode 291개·Player 402개가 통과했다. 종료 로그와 범위는 [검증 기록](exhibition-shared-behavior-validation.md)을 확인한다. 커밋·푸시·PR 갱신은 수행하지 않았다.
