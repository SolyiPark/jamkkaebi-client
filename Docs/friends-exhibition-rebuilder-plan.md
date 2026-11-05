# 친구 전시관 재구축기 개선 계획

작성일: 2026-11-05 (같은 날 사용자 결정 반영, 방문 종료 경로 보완). 상태: **계획만 작성, 구현 시작 전**. 이 문서만 읽고 이전 대화 없이 작업을 시작할 수 있도록 썼다.

기준: 클라이언트 `main` `c5cbe57`(PR #22), 서버 `be72ae8`. 아래 내용은 코드와 자산을 읽어 확인했으며 **Unity에서 실행해 본 결과가 아니다**. 줄 번호는 작성 시점 값이므로 구현 전에 다시 확인한다.

## 1. 목표

서버 데이터로 친구 전시관을 재구축하는 흐름은 유지하고, 다음 세 가지를 수행한다.

1. **실제 전시관처럼 표시:** 흰 패널 위의 버튼을 홈과 같은 배경 10장, 7×7 상판, 도깨비·건물 표현으로 바꾼다.
2. **디버깅:** 4장의 문제를 고치고 테스트를 추가한다.
3. **하나의 컴포넌트로 묶기:** 조회, 재구축, 표시를 `FriendExhibitionRebuilder` 하나를 통해 다루고, `FriendsPresenter`에서 방문 표시 코드를 걷어 낸다.

### 완료 기준

- 친구 프로필을 누르면 홈과 같은 배경·상판 위에, 서버 좌표대로 객체가 놓인다.
- 같은 좌표의 전시물이 홈과 방문 화면에서 상판의 같은 위치에 놓인다.
- 앞뒤 가려짐이 바닥 접점 기준으로 맞고, 도깨비는 칸 사이를 부드럽게 걷는다.
- 객체를 누르면 정보가 표시되고, 편집은 불가능하다.
- 방문 중에는 나가기 전까지 다른 탭으로 이동할 수 없다. 스와이프와 하단 탭 모두 막히고, 로딩·오류 상태에서도 나가기 버튼은 항상 동작한다.
- 방문 보상·선물 후에도 방문 화면이 유지된다. 요청이 실패해도 유지되며, 4.1절의 경로만 방문을 끝낸다.
- 기존 예외 시나리오(빈 전시, 미지원, 잘못된 좌표, 중복, 격자 불일치, 거부, 오류, 지연)가 새 화면에서 올바르게 표시된다.
- 내 홈의 배치와 최초 등장 연출이 영향받지 않는다.
- `FriendsPresenter`에 격자 좌표 계산, 스프라이트 생성, 이동 보간 코드가 남지 않는다.
- `Tools/VerifyIntegration.ps1` 통과. 가능하면 `Tools/VerifyServerContract.ps1`도 통과.

### 범위 밖

로그인·OAuth, 홈 저장 UI 연결, 작업대의 서버 연동, 다칸·회전 배치, 실시간 동기화, 홈 씬 구조 변경, 서버 코드 변경. 필요해 보여도 자동으로 포함하지 않는다.

## 2. 유지할 계약

| 계약 | 근거 |
| --- | --- |
| 전시 GET과 보상 POST 분리. 서버 실패 시 mock으로 전환하지 않음 | `AGENTS.md`, `Docs/friends-server-verification.md` |
| 요청 세대 번호로 늦은 응답 무시, 다른 친구로 전환 시 기존 전시 제거 | `ExhibitionVisitSession` |
| 방문 이동은 `VisitObject.Cell`만 변경. 원본 Anchor와 내 홈은 수정 금지 | `VisitObject`, `SnapshotPlacement` |
| 7×7 이외 격자 거부, 좌표 축소·보정 금지 | `ExhibitionVisitScene.cs:26` |
| 화면 → RT 변환은 `MirrorInput`에서 한 번만. 씬·레이어·raycaster 유지 | `Docs/mobile-ui-scene-contract.md` |
| 레이어는 카메라 격리, 앞뒤는 Sorting Order로 처리 | `Docs/exhibition-design.md` |
| 모델과 MonoBehaviour·UI 표현 분리, 의존 방향 `Jamkkaebi.UI` → `Jamkkaebi.Gameplay` | `AGENTS.md` |
| 토큰을 자산·로그·문서에 기록 금지 | `AGENTS.md` |

## 3. 현재 구조

```text
FriendsPresenter.Visit (FriendsPresenter.cs:225)
  → IExhibitionBackend.GetExhibition
      서버: GET /api/friends/{code}/exhibition → ServerExhibitionBackend.ParseSnapshot
      모의: MockExhibitionBackend.CreateSnapshot
  → ExhibitionVisitSession.Complete (세대·소유자 검사)
  → new ExhibitionVisitScene (격자·범위·중복 검증, VisitObject 생성)
  → FriendsPresenter.BuildVisit / PositionVisitObjects (uGUI 버튼 배치)
  → FriendsPresenter.Update → Scene.Walk (1초마다 칸 이동)
```

서버 응답 형식은 클라이언트 파싱과 일치함을 소스로 확인했다. 재구축의 앞부분(조회·파싱·세션·장면 모델)은 유지하고, 마지막 표시 단계와 소유 구조를 바꾸는 작업이다.

### 홈 전시관의 구성 (복제 기준)

- `Home Exhibition`: `MirrorSceneRoot`, `ExhibitionIntro`, `ExhibitionView`, `ExhibitionHousing`.
- `Artwork space` 아래 `Artwork 01`~`10`: `SpriteRenderer` + `ExhibitionLayer`, 재질 `Assets/Jamkkaebi/Materials/SpriteUnlit.mat`.
- `Artwork 05`: 상판. `ExhibitionSurface`(격자 `Configuration/ExhibitionGrid.asset`)가 있고, 자식 `Housing Content`에 객체를 넣는다.
- 객체: 부모에 `ExhibitionDepth`(SortingGroup, 정렬 200~1800), 자식에 표현물. 위치는 `ExhibitionSurface.CellWorldPosition(cell)`.
- 카메라: 직교, z=-10, 크기 `max(3.84, 2.32 / aspect) / zoom` (`ExhibitionView.cs:47`).

| 레이어 | 01 | 02 | 03 | 04 | 05 | 06 | 07 | 08 | 09 | 10 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Sorting Order | 0 | 10 | 20 | 30 | 100 | 2000 | 2010 | 2020 | 2100 | 2110 |
| 불투명도 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | .22 | .075 |
| 크기 모드 | cover | 폭 채움 | - | 폭 채움 | - | 폭 채움 | 폭 채움 | 폭 채움 | - | - |
| 시차 | .015 | .055 | .025 | .11 | .18 | .27 | .32 | .36 | .01 | 0 |

값의 원본은 `Editor/ExhibitionSetup.cs:89-100`이고, 시차와 정렬은 `Home.unity`와 일치함을 확인했다.

### 친구 씬의 현재 조건

- `Friends Root`: `MirrorSceneRoot`, `FriendsPresenter`. 레이어 11, `Friends Camera`는 직교 크기 5.5, z=-10.
- UI는 런타임 생성 Canvas(Screen Space Camera, planeDistance 5). `Render()`는 Canvas의 0번 자식 `Paper`만 남기고 모두 지운다.
- 탭 정책은 ContinueRunning이고, 공통 프로필은 숨김이다.

## 4. 확인된 문제

코드 확인 = 코드를 읽어 원인을 확인함. 실행 확인 = Unity에서 재현 필요.

| # | 문제 | 위치 | 상태 | 조치 |
| --- | --- | --- | --- | --- |
| P1 | 서버 모드에서 방문 보상·선물 **성공** 시 방문 화면이 닫힌다. 실패 분기(`:142`)는 닫지 않으므로 대상이 아니다 | `FriendsPresenter.cs:137`, `:258`, `:586` | 코드 확인 | 4.1절. 성공 시 동작을 호출 측에서 지정하고, 차단만 나감 |
| P2 | 재구축물이 Canvas UI라서 배경·정렬을 쓸 수 없다. `Paper`와 전체 크기 스크롤 영역이 월드 표현과 입력을 가린다 | `FriendsPresenter.cs:75`, `:284`, `:308` | 코드 확인 | 월드 스테이지로 분리, 방문 HUD는 스크롤 없이 구성 |
| P3 | 좌표를 하드코딩한 배율로 UI에 옮겨 실제 상판과 무관하다 | `FriendsPresenter.cs:556` | 코드 확인 | `CellWorldPosition` 사용 |
| P4 | 서버 객체는 전부 Unknown이고, 모의는 `Id > 3`이 Unknown이라 유물 4~9가 미지원이 된다 | `VisitObject.cs:13`, `ServerExhibitionBackend.cs:45-47` | 코드 확인 | 카탈로그 등록 여부로 판정 |
| P5 | 도깨비가 1초마다 순간 이동하고, 방향이 오른쪽→위→왼쪽→아래 순으로 돈다 | `FriendsPresenter.cs:199-203`, `ExhibitionVisitScene.cs:44-52` | 코드 확인 | 뷰에서 보간, 방향은 시드 난수. 앵커 불변 유지 |
| P6 | `capturedAt`이 `JObject.Parse`에서 날짜 토큰으로 변환된 뒤 문자열로 표시되어 형식이 깨질 수 있다 | `ServerApiResult.cs:18`, `ServerExhibitionBackend.cs:51`, `FriendsPresenter.cs:501` | 실행 확인 | 파싱에서 표시 형식으로 변환, 실패 시 값 없음 처리 |
| P7 | 선택 기본값이 모의 이름 `"달이"`이다 (다른 곳은 Key 형식) | `FriendsPresenter.cs:53` | 코드 확인 | 빈 값으로 시작 |
| P8 | 서버가 같은 칸의 겹침을 막지 않는다. 같은 칸의 객체가 있으면 `Walk`의 점유 Set이 먼저 지워져 다른 객체의 칸을 비운 것으로 판단할 수 있다 | `ExhibitionVisitScene.cs:42-56` | 코드 확인 | 객체별 점유를 계산, 겹침은 표시 유지 + 경고. 보정 금지 |
| P9 | 한 행의 필드 누락으로 전시 전체가 실패한다. 범위 밖 좌표는 해당 행만 생략하는 정책과 다르다 | `ServerExhibitionBackend.cs:21-22`, `:44-47` | 코드 확인 | 행 단위로 생략하고 경고. 최상위 필드 오류는 기존처럼 실패 |
| P10 | 모의 데이터의 ID·이름이 서버 시드와 다르다 (건물 불국사 다보탑 등) | `MockExhibitionBackend.cs:37-41` | 코드 확인 | 서버 시드 이름·ID로 교체해 아트 확인에 사용 |
| P11 | 10월 29일 대규모 이동 이후 Unity 실행 기록이 없다 | `project-context/VALIDATION.md` | 실행 확인 | 0단계에서 기준선 확보 |
| P12 | `VerifyServerContract.ps1`의 Java 실행기가 현재 서버와 컴파일되는지 모른다 (`relic` 이름 변경 등) | `Tools/UnityContractHarnessTest.java` | 실행 확인 | 실패 시 실행기 수정 여부를 사용자에게 보고 |
| P13 | **P1을 고치면 드러나는 문제.** POST 성공 후 재조회가 실패하면 `State`와 스냅샷의 `VisitRewardAvailable`·`GiftState`가 이전 값이라 보상·선물 버튼이 다시 켜진다. 재조회 실패 알림도 `:140`의 성공 알림에 덮인다 | `FriendsPresenter.cs:115`, `:139-140`, `:538`, `:540` | 코드 확인 | 재조회 전에 성공을 `State`에 표시. 알림은 두 결과를 함께 표시 |
| P14 | 차단 POST 중 뒤로 나가 다른 친구를 방문하면, 늦은 성공 응답이 `Page == "Visit"`만 보고 새 방문을 닫는다. `Visit`은 소셜 코루틴을 취소하지 않고, 확인 창도 응답 전까지 남아 있어 이 조작이 가능하다 | `FriendsPresenter.cs:137`, `:225-235`, `:599` | 코드 확인 | 차단 대상과 현재 방문 소유자의 코드가 같을 때만 나감 |

### 4.1 방문 종료·전환·유지 경로

실패는 이탈이 아니다. 현재 코드도 `ChangeServerRoutine`의 실패 분기(`:142`)에서는 화면을 유지하며, 닫는 것은 성공 분기(`:137`)뿐이다. 구현 단계에서는 아래 표를 기준으로 삼고, 방문 관련 호출을 추가하면 표에도 추가한다.

| 경로 | 위치 | 현재 | 목표 | 탭 잠금 |
| --- | --- | --- | --- | --- |
| `VisitBack` | `:496` | 종료 | 종료 (`Leave`) | 해제 |
| 차단 성공, 현재 방문 소유자가 대상 | `:165` → `:137` | 종료 | 종료 (`Leave`) | 해제 |
| 차단 성공, 다른 친구를 방문 중 (늦은 응답) | `:165` → `:137` | **새 방문 종료 (P14)** | 유지 | 유지 |
| 모의 차단 | `:166` | 종료 | 종료 (`Leave`) | 해제 |
| `ConnectServer` | `:87`, `:91` | 종료 | 종료 (`Leave`) | 해제 |
| `OnDestroy` | `:222` | 취소 | 취소 (`Leave`). 새 루트는 잠기지 않은 상태로 시작 | 해당 없음 |
| 서버 보상·선물 성공 | `:258`, `:586` → `:137` | **종료 (P1)** | 유지 | 유지 |
| 서버 보상·선물 성공 후 재조회 실패 | `:139`, `:115` | 종료 후 목록에 이전 값 표시 | 유지, 버튼 비활성 (P13) | 유지 |
| 서버 보상·선물·차단 실패 | `:142` | 유지, 오류 알림 | 같음. 자동 재시도 없음 | 유지 |
| 모의 보상·선물 | `:259`, `:587` | 유지 | 같음 | 유지 |
| 전시 GET 실패, 격자 불일치 | `:250`, `ExhibitionVisitSession.Complete` | 유지, 재시도 표시 | 같음. `VisitBack`과 `VisitRetry` 사용 가능 | 유지 |
| `VisitRetry`, 방문 중 `Visit` 재호출 | `:529`, `:225` | 전환 (세대 증가) | 전환. `Leave`를 지나지 않으므로 이전 표현물을 재구축기가 직접 파괴 | 유지 |

구현 규칙:

1. **잠금 해제는 `Leave` 한 곳에서 한다.** `Page`가 `Visit`에서 벗어나는 경로는 `Show`와 `ConnectServer`뿐이고 둘 다 `CancelVisit`을 지나므로, 1단계에서는 이 함수를 유지하고 3단계에서 재구축기의 `Leave`를 호출하게 한다. `Visit` 재호출의 잠금은 카운터가 아닌 bool로 둔다.
2. **성공 시 동작은 호출 측이 정한다.** `ChangeServer(method, path, body, Action succeeded = null)`로 바꾸고 `:137`을 제거한다. `succeeded`는 재조회 **전에** 실행한다.
   - 보상: `State.MarkServerVisitClaimed()` 후 `Render()`.
   - 선물: `State.MarkServerGiftSent()` 후 `Render()`. `GiftRecipient`를 기존 값 `"SERVER_SENT"`로 둔다.
   - 차단: 현재 페이지가 `Visit`이고 `NormalizeCode(_visitor.Code)`가 대상과 같을 때만 `Show("Friends")`.
   - 방문 세대가 아니라 **코드**로 비교한다. 차단 응답 전에 같은 친구를 다시 방문한 경우에도 그 방문은 닫혀야 한다.
3. **표시 값은 임시 값이다.** 두 표시 메서드는 `ServerOwned`일 때만 쓰고, 재조회가 성공하면 `ApplyServerSummary` 값으로 교체된다. `VisitClaimed`와 `GiftRecipient`는 하루 전체의 값이므로 친구별로 두지 않는다. 다른 친구의 방문 중에 늦은 응답이 와도 그 화면의 버튼이 함께 꺼지는 것이 맞다.
4. **스냅샷을 수정하거나 다시 받지 않는다.** 보상·선물 후 전시 GET을 추가하지 않는다. 전시 GET과 보상 POST 분리 계약을 유지하고, `VisitRewardAvailable`·`GiftState`는 방문 시작 시점 값으로 둔다.
5. **`:140`의 알림은 재조회 결과를 반영한다.** `ReloadServer`가 실패를 알 수 있게 하고, 실패 시 "요청은 완료됐지만 목록을 갱신하지 못했어요" 형식으로 알린다. 변경 요청은 다시 보내지 않는다.
6. **재조회 후 프로필을 코드로 다시 찾는다.** `FriendProfile`은 값 비교가 없고 `FriendsServerMapper`가 재조회 때 새 객체를 만들어서, `:227`의 `State.Friends.Contains(_visitor)`가 거짓이 되고 `VisitRetry`가 아무 표시 없이 무시된다. 재조회 성공 시 `_visitor`를 코드로 다시 잡고, 목록에서 사라졌으면 기존 객체로 표시만 유지한다. 재시도는 코드 기준으로 판정하고, 친구가 아니면 알림을 표시한다.
7. **처리 중 입력을 조용히 버리지 않는다.** `:126`의 `_serverBusy` 반환 때문에 재조회 중 누른 선물·차단이 아무 반응 없이 사라진다. 이 경로는 방문을 유지하면서 처음 도달할 수 있게 되므로, 반환 전에 "이전 요청을 처리 중이에요."를 알린다. `_serverBusy`는 `RunSocialRequest`의 `finally`에서 풀리므로 버튼 `interactable`에 묶지 않는다. 마지막 `Render` 뒤에 풀려 버튼이 꺼진 채 남기 때문이다.
8. **`VisitBack`은 POST를 취소하지 않는다.** 이미 보낸 변경 요청의 결과는 소셜 코루틴에서 끝까지 반영한다. `ConnectServer`의 취소와 세대 처리는 그대로 둔다.

## 5. 목표 구조

```text
Friends Root (MirrorSceneRoot, FriendsPresenter, FriendExhibitionRebuilder)
├─ Friends Camera
├─ Friends UI (런타임 Canvas, 방문 중에는 HUD만)
└─ Visit Stage (프리팹 인스턴스, 기본 비활성)
   ├─ Artwork 01 ~ 10 (SpriteRenderer, ExhibitionLayer)
   │  └─ Artwork 05 (ExhibitionSurface)
   │     └─ Visit Content (재구축한 객체의 부모)
   └─ Visit Input (전체 크기 BoxCollider2D, 클릭 수신)
```

컴포넌트를 항상 활성인 `Friends Root`에 두는 이유는 코루틴 때문이다. 비활성 오브젝트에서는 `StartCoroutine`이 실패한다.

### `FriendExhibitionRebuilder` (`Scripts/UI/Friends/`, MonoBehaviour)

방문의 유일한 진입점이다. 다음을 소유한다: 조회 코루틴, `ExhibitionVisitSession`, 스테이지 표시, 표현물 생성·파괴, 이동 보간, 객체 선택.

```csharp
public ExhibitionVisitSession Session { get; }          // 기존 검증 코드가 그대로 읽을 수 있게 노출
public VisitObject Selected { get; }
public event Action Changed;                            // 로딩 완료·실패·선택 변경 시 HUD 갱신

public void Visit(string friendCode, IExhibitionBackend backend);
public void Leave();                                    // 코루틴 취소, 세대 무효화, 표현물 파괴, 카메라 복원
```

직렬화 필드: `_stage`, `_surface`, `_content`, `_camera`, `_catalog`, `_material`.

`FriendsPresenter`에 남기는 것: 어떤 backend를 쓸지 결정, HUD(뒤로·제목·상세·보상·선물·차단), 보상·선물 명령, 알림. `public ExhibitionVisitSession VisitSession => _rebuilder.Session;`을 유지해 `IntegratedVerification` 수정을 줄인다.

새 데이터 구조나 난수 정책 같은 규칙은 순수 C# 모델(`ExhibitionVisitScene` 등)에 두어 EditMode로 검증한다.

### `ExhibitionVisualCatalog` (ScriptableObject, `Configuration/ExhibitionVisualCatalog.asset`)

서버 ID를 표현물로 잇는 **유일한 대응표**다.

```text
relics[]    : relicId, prefab, scale, footOffset
buildings[] : buildingId, prefab, scale, footOffset
```

- ID 중복, 음수 scale을 `Validate()`로 거부한다.
- 등록된 ID인데 prefab이 없으면 `ExhibitionDummyVisuals`(홈과 같은 더미)를 쓴다.
- **미등록 ID만 Unknown**이다. 더미 표현 + HUD 경고로 표시한다.
- 프리팹 계약은 `HousingItemDefinition`과 같다: 바닥 접점이 원점인 표현용 자식, Camera·EventSystem·AudioListener 없음.

`ExhibitionVisitScene`은 UI 자산을 모르도록 `Func<SnapshotPlacement, bool> isKnown`을 생성자로 받는다. `SnapshotPlacement.Placeholder`와 `Id > 3` 규칙은 제거한다.

## 6. 자산 현황

| 자산 | 위치 | 상태 |
| --- | --- | --- |
| 배경 10장 | `Assets/Jamkkaebi/Art/Exhibition/` | main에 있음. 2048 제한, 1000 PPU. 홈과 공유 |
| 격자 | `Configuration/ExhibitionGrid.asset` | main에 있음 |
| 재질 | `Materials/SpriteUnlit.mat` | main에 있음 |
| 더미 전시물·도깨비 | `ExhibitionDummyVisuals` | main에 있음. 코드로 그림 |
| 도깨비 `Dokkaebi_A2` (향로깨비) | `origin/art/dokkaebi-rig-animation` | **미병합**. relicId 2 금동대향로. 앞모습 한정 |
| 도깨비 `Dokkaebi_C2` (마패깨비) | 같은 브랜치 | **미병합**. relicId 8 마패. 앞모습 한정 |
| 공용 애니메이션 | 같은 브랜치 | 컨트롤러 `DokkaebiCommon`, Bool 파라미터 `IsMoving`, Idle·Walk·Blink·HornGlow |
| 도깨비 초상화 3종 | `origin/feat/minigame-ui-assets` | 미병합. 02 향로, 05 청자, 08 마패. 월드 표현에는 쓰지 않음 |
| 건물 아트 | **미완성, 더미로 처리** | 사용자 확인. 아래 검색 범위 참고 |
| 나머지 도깨비 7종 | 없음 | 상감청자(B2)는 초상화만 있고 리깅 프리팹 없음 |

서버 ID: 유물 1~9, 건물 1 석굴암, 2 경천사지 십층석탑, 3 경복궁. 전체 표는 `Docs/collection-integration.md`.

**도깨비 이름 규칙:** 알파벳은 시대(A 삼국, B 고려, C 조선), 숫자는 시대 안의 유물 순서로 본다. 커밋 메시지(`1e95c81` 향로깨비 = A2, `545de37` 마패깨비 = C2)와 초상화 번호(02 향로, 08 마패), `Relic{relicId:00}` 명명 규칙이 모두 일치한다. `relicId = (시대 번호 - 1) × 3 + 순번`이다.

**건물 아트 검색 범위 (2026-11-05):** 아래에서 건물 그림을 찾지 못했다. 팀원 로컬이나 외부 드라이브처럼 이 PC에서 볼 수 없는 곳은 확인하지 못했다.

- `C:\2026` 전체의 이미지·PSD·PSB 파일과 파일명 (`building`, `건물`, 건물 이름 등)
- 원격·로컬 모든 브랜치의 현재 파일과, 전체 Git 이력에 있었던 이미지·프리팹
- 배경 레이어 10장: 직접 확인한 결과 종이·산·달·상판·구름·질감뿐이며 건물은 없음
- 서버 저장소, 발표 자료, `C:\2026\Capstone`(기획 문서만 있음. `context/기획 정리.md`에 "건물 아트 3세트"가 제작 대상으로 적혀 있음)

아트 브랜치 가져오기 (커밋하지 않으므로 병합하지 않고 필요한 경로만 작업 트리로 가져온다):

```bash
git checkout origin/art/dokkaebi-rig-animation -- Assets/Jamkkaebi/Art/Characters Assets/Jamkkaebi/Art/Characters.meta Assets/Jamkkaebi/Prefabs/Characters Assets/Jamkkaebi/Prefabs/Characters.meta
```

`remote.origin.fetch`가 `main`만 받도록 설정된 클론에는 이 원격 참조가 없다. 먼저 아래 명령으로 해당 브랜치만 받는다.

```bash
git fetch origin refs/heads/art/dokkaebi-rig-animation:refs/remotes/origin/art/dokkaebi-rig-animation
```

- `Assets/Jamkkaebi/Art.meta`는 GUID가 main(`d2c746…`)과 다르므로(`ce4f58…`) **가져오지 않는다.**
- `Scenes/Sandbox_Dokkaebi.unity`는 가져오지 않는다. 다른 파일이 이 씬에 의존하지 않는다.
- 분기 시점이 2026-09-23으로 오래되었으므로, Unity에서 열어 참조가 살아 있는지 확인한다.
- PSB는 LFS 대상이 아니라 일반 Git 파일이다.
- 패키지 `com.unity.2d.psdimporter`, `2d.animation`은 양쪽 버전이 같다.
- 리깅 프리팹은 종료 시 GPU 버퍼 우회와 관련될 수 있으므로 `Docs/gpu-buffer-shutdown-validation.md`를 읽고 종료 로그를 확인한다.

## 7. 사용자 결정 (2026-11-05 확정)

| # | 결정 | 내용 |
| --- | --- | --- |
| D1 | 도깨비 대응 | A2 → relicId 2, C2 → relicId 8로 확정. 나머지 유물은 프리팹 없이 등록해 더미로 표시 |
| D2 | 건물 아트 | **아직 완성되지 않아 올라오지 않았다. 더미로 처리한다.** 홈과 같은 더미 전시대로 표시하고, 아트가 올라오면 카탈로그만 교체 |
| D3 | 방문 화면 형태 | 전체 화면 전시관 + 위쪽 정보, 아래쪽 상세·보상·선물 HUD |
| D4 | 방문 중 탭 이동 | **불가능.** 나가기 전까지 전시관과 소셜 기능(나가기, 친구 보상·선물, 차단, NPC 상호작용)만 사용. 시차 연출도 없음 |
| D5 | 커밋 | **사용자가 요청할 때만 커밋한다.** 처음의 "전부 한 번에 커밋" 결정은 이전 작업을 PR #22로 먼저 올리면서 대체됐다. 구현은 `main` 기준의 별도 작업 브랜치에서 진행한다 |

## 8. 작업 단계

커밋·푸시·PR·병합은 사용자가 요청할 때만 한다 (D5). 새 코드와 자산의 `.meta`를 함께 만든다. 작업 트리에 이 계획과 무관한 미커밋 파일이 있으면 건드리지 않고 커밋에도 넣지 않는다.

### 0단계: 환경과 기준선

1. 맥락 저장소의 `project-context/README.md`, `CURRENT-STATE.md`, `SESSION-LOG.md` 최신 항목과 클라이언트 `AGENTS.md`, `Docs/csharp-convention.md`, `Docs/mobile-ui-scene-contract.md`를 읽는다.
2. `git status`와 브랜치를 확인한다. 기존 미커밋 변경을 보존한다.
3. Unity 6000.3.23f1 경로를 찾는다. PC마다 다르다. `C:\Program Files\Unity\Hub\Editor`에 없을 수 있고, `D:\UnityEditors\6000.3.23f1\Editor\Unity.exe`에 설치된 PC도 있다. 2026-10-28에 그 PC에서 라이선스 오류(종료 코드 198)로 EditMode 전에 중단된 기록이 있으므로 라이선스 활성화 여부도 확인한다.
4. **변경 전 기준선:** `Tools/VerifyIntegration.ps1 -EditorPath <Unity.exe>`를 실행하고 XML과 Player 로그까지 확인한다. 프로세스의 `JAMKKAEBI_SERVER_URL`은 비운다. 기존 실패는 이 작업의 실패와 구분해 기록한다.

Unity 없이는 프리팹 생성과 화면 확인이 불가능하다. 1·2단계 코드까지만 작성하고 미검증임을 밝힌다.

### 1단계: 모델·흐름 디버깅 (P1, P4~P10, P13, P14)

수정: `ExhibitionVisitScene`, `VisitObject`, `SnapshotPlacement`, `ServerExhibitionBackend`, `MockExhibitionBackend`, `FriendsPreviewState`, `JamkkaebiApiClient`(응답 주입 지점), `FriendsPresenter`.

- `isKnown` 주입, `Placeholder` 제거 (P4)
- 시드 난수 방향, 객체별 점유 계산, 겹침 경고 (P5, P8)
- 행 단위 파싱 실패 처리, `capturedAt` 형식 (P6, P9)
- 모의 데이터를 서버 시드 기준으로 교체 (P10)
- `_selectedObject` 초기값 (P7)
- 4.1절의 구현 규칙 전체: 보상·선물 후 화면 유지 (P1), 재조회 실패 처리 (P13), 차단 대상 비교 (P14), 프로필 재연결, 처리 중 알림
- `JamkkaebiApiClient`의 응답 주입 지점 (10장)

테스트: `ExhibitionVisitTests`, `ServerContractTests`에 추가·수정한다. 추가할 경우: 겹침 입력에서 이동 후에도 다른 객체 칸에 들어가지 않음, 한 행 누락 시 나머지 표시, 같은 시드의 이동 재현, 미등록 ID만 Unknown, Anchor 불변. `FriendsPreviewState`의 두 표시 메서드가 `ApplyServerSummary`로 덮이는지도 검사한다. `IntegratedVerification.cs:1151`의 "서버 객체 전부 Unknown" 기대값을 바꾼다. 4.1절의 경로 검사는 10장의 Player 검사로 추가한다.

완료: EditMode 통과. 경로 검사는 Player에서만 돌아가므로 `Tools/VerifyIntegration.ps1`까지 통과해야 P1, P13, P14를 완료로 보고한다.

### 2단계: `ExhibitionVisualCatalog`

스크립트, 자산, EditMode 테스트(조회, 미등록, 중복 거부)를 추가한다. 우선 프리팹 없이 유물 1~9, 건물 1~3을 등록한다.

### 3단계: 스테이지 프리팹과 재구축기

1. `Editor/FriendsSetup.cs`에 일회성 메서드 `ApplyVisitStage`를 추가한다. 3장의 표 값으로 `Assets/Jamkkaebi/Prefabs/Friends/FriendExhibitionStage.prefab`을 만들고 `Friends.unity`에 비활성 인스턴스로 넣은 뒤 참조를 연결한다. 표 값은 `ExhibitionSetup`과 중복되지 않게 공용 상수로 뽑는다. 기존 `Apply`, `Prepare`는 재실행하지 않는다.
2. `FriendExhibitionRebuilder`를 구현한다.
   - `Visit`: 세션 시작, 코루틴 소유. 완료 시 스테이지 활성화와 객체 생성.
   - `LateUpdate`: 카메라 크기 계산 후 각 레이어에 `Apply(100f, 0f, camera)` 호출.
   - `Leave`: 코루틴 중단, 객체 파괴, 스테이지 비활성화, 카메라 크기 5.5 복원.
   - 객체 생성: `Visit Content` 자식, `ExhibitionDepth.Configure`, 표현물 생성, `MirrorSceneRoot.SetLayer`.
3. `FriendsPresenter`의 `_visitRequest`, `_visitViews`, `_walkStep`, `_nextWalk`, `PositionVisitObjects`와 `_visitGrid`를 제거한다.

완료 확인: 캡처를 홈과 비교한다. 빈 씬이 아니라 공통 셸(`UIPrototype.unity`)에서 실행한다.

### 4단계: 방문 HUD와 탭 잠금 (P2, P3, D4)

**탭 잠금.** 콘텐츠가 호스트를 직접 참조하지 않도록 `MirrorSceneRoot`에 상태를 두고 호스트가 읽는다.

- `MirrorSceneRoot`: `public bool NavigationLocked { get; private set; }`, `SetNavigationLocked(bool)`. 친구 씬 단독 실행에서도 안전하다.
- `FriendExhibitionRebuilder`: `Visit`에서 잠그고 `Leave`에서 푼다. 4.1절 표의 종료 경로(`VisitBack`, 현재 소유자 차단 성공, 모의 차단, `ConnectServer`, `OnDestroy`)가 모두 `Leave`를 지나는지 확인한다. 보상·선물·차단 **실패**와 늦은 차단 응답은 종료 경로가 아니므로 잠금을 유지한다.
- `TabHost.SelectTab`, `BeginSwipe`: 활성 탭이 잠겨 있으면 거부한다. `SelectTab`은 `IsBusy` 검사 **전에** 거부해 `pendingTab`에 쌓이지 않게 한다.
- `TabHost.LateUpdate`: 잠금 상태가 바뀌면 하단 탭을 숨기고 `Layout`에서 `viewport.offsetMin`을 0으로 해 전시관을 전체 화면으로 쓴다. 나가면 복구한다.
- 뷰포트가 바뀌면 `LateUpdate`가 모든 탭의 RT를 다시 만든다. 진입·이탈마다 한 번씩 발생하므로 끊김과, 일시정지된 작업대 미리보기를 확인한다. 문제가 크면 하단 탭을 숨기지 않고 비활성화만 한다.
- 뷰포트가 화면 아래까지 내려가므로 HUD 하단 버튼이 안전 영역 안에 있는지 확인한다.

**HUD.** `BuildVisit`을 다시 쓴다.

- `VisitBack`은 로딩·오류·빈 전시 상태에서도 항상 누를 수 있어야 한다. 없으면 방문 화면에 갇힌다.

- 방문 중 `Paper`를 끄고, Canvas `sortingOrder`를 3000으로 한다 (홈 조작 UI와 같은 값, 전경 레이어 2110보다 위).
- 방문 페이지는 `MakeScroll`을 쓰지 않는다. 전체 크기 투명 Image가 월드 클릭을 막기 때문이다.
- 버튼 ID(`VisitBack`, `VisitReward`, `VisitGift`, `VisitMenu`, `VisitRetry`)를 유지한다. `IntegratedVerification`이 사용한다.
- 로딩·오류·빈 전시 안내를 HUD에 표시한다.
- `IntegratedVerification.cs:314`, `:320`의 `friends.Scroll` 사용을 제거한다. `Render()`의 `Scroll` 처리는 null을 허용한다.

### 5단계: 선택과 이동 보간 (P5)

- `Visit Input`에 `IPointerClickHandler`를 둔다. `eventData.position`은 이미 RT 픽셀이므로 다시 변환하지 않는다. 드래그는 탭 잠금으로 무시되므로 전용 드래그 처리는 두지 않는다.
- 선택 결과를 `ObjectSelected` 이벤트로 공개한다. NPC 상호작용이 나중에 연결될 자리이며, 상호작용 자체는 이번 범위가 아니다 (`Docs/npc-interaction-contract.md`).
- 객체 찾기는 `ExhibitionHousing.ItemAtTexturePoint`와 같은 방식(광선 → 평면 → 로컬 사각형, 정렬 순서가 높은 쪽 우선)으로 한다. 겹친 객체에서 앞쪽이 선택되도록 객체별 Collider를 쓰지 않는다.
- 이동은 모델 `Cell` 갱신 후 뷰가 `CellWorldPosition` 사이를 보간한다. 홈 값(칸당 .85초, 대기 .7초)을 기본으로 하고, 이동 중 `IsMoving`을 갱신한다.

### 6단계: 실제 아트 연결

1. 6장의 명령으로 도깨비 자산을 가져온다.
2. 카탈로그의 relicId 2에 `Dokkaebi_A2`, relicId 8에 `Dokkaebi_C2`를 연결하고, 칸 크기(폭 약 .3)에 맞게 scale과 footOffset을 조정한다. 건물 뒤를 지나갈 때 가려지는지 확인한다.
3. 앞모습 한정 아트이므로 이동 방향에 따른 뒷모습 전환은 만들지 않는다.
4. 건물은 더미를 유지한다 (D2). 이번 작업에서 건물 프리팹을 만들지 않는다.
5. 모의 시나리오에 relicId 2, 8을 넣어 서버 없이도 실제 아트를 확인할 수 있게 한다.
6. 종료 로그에서 GPU 버퍼 경고를 확인한다.

### 7단계: 마무리

전체 검증, `git diff --check`, 문서 갱신(11장).

## 9. 함정

- **탭 잠금이 풀리지 않으면 앱이 방문 화면에 갇힌다.** 반대로 실패를 이탈로 처리하면 재시도할 화면이 사라진다. 경로는 4.1절 표를 따른다. 잠금 해제를 `Leave` 한 곳에 모으고, 씬 재시작 시에는 새 루트가 잠기지 않은 상태로 시작한다. 기존 검증이 방문 중에 `SelectTab`을 호출하면 실패하므로 나간 뒤 호출하도록 고친다.
- **`ExhibitionIntro`를 방문 스테이지에 넣지 않는다.** `_sessionShown`이 static이라 Complete 호출 시 내 홈의 최초 등장 연출이 생략된다. 같은 이유로 `ExhibitionIntro`를 요구하는 `ExhibitionView`도 쓰지 않는다.
- `ExhibitionLayer.Apply`의 `introTime`이 작으면 투명하게 그려진다. 큰 값을 넘긴다.
- `DokkaebiWanderer`와 `HousingLayout`을 재사용하지 않는다. 이동 예약 저장소가 한 마리 전제이고, 방문은 편집 모델이 필요 없다.
- 깊이 정렬에 Unity Layer를 쓰지 않는다. 레이어는 11로 통일하고, 런타임 생성물에 `SetLayer`를 호출한다. `IntegratedVerification.cs:360`이 이를 검사한다.
- `MirrorSceneRoot.Configure`가 `uiRaycasters`를 수집하므로, HUD 재구성 후 Canvas의 raycaster가 유지되는지 확인한다.
- `MirrorInput.Raycast`는 z=0 평면과 해당 씬의 Physics2D를 쓴다. 스테이지를 z=0에 두고, 탭 호스트가 Physics2D를 시뮬레이션함을 고려해 `Visit Input`은 움직이지 않게 둔다.
- `ExhibitionChecks`는 `Home.unity`만 열어 `FindFirstObjectByType<ExhibitionSurface>`를 쓴다. 단독으로 열리므로 문제없어야 하지만, 3단계 후 실행해 확인한다.
- `IntegratedSetup.Prepare`, `ExhibitionSetup.Prepare`, `CollectionSetup.Apply`, `FriendsSetup.Apply`를 재실행하지 않는다.
- 숨겨진 탭의 카메라는 꺼지지만 `LateUpdate`는 돈다. 스테이지가 비활성일 때는 카메라 크기를 건드리지 않는다.
- 배경 텍스처는 홈과 공유되므로 메모리가 두 배가 되지는 않는다. 다만 방문 중 렌더 비용은 모바일에서 따로 확인해야 하며, Windows 결과로 대신하지 않는다.
- `ExhibitionDummyVisuals`는 `IDisposable`이다. 파괴 시 `Dispose`를 호출한다.
- 커밋에 `Library`, `Logs`, `Builds`를 넣지 않는다. Unity가 자동 변경한 설정은 diff로 필요성을 확인한다.

## 10. 검증

| 시점 | 방법 | 확인 내용 |
| --- | --- | --- |
| 매 단계 | Unity Test Runner EditMode | 모델·카탈로그 규칙 |
| 3단계 이후 | `Tools/VerifyIntegration.ps1 -EditorPath <Unity.exe>` | 종료 코드, XML, Player 로그, 캡처 |
| 4단계 이후 | `ExhibitionChecks.Run`, `ReviewRegressionChecks.Run` | 씬 참조, 회귀 |
| 마지막 | `Tools/VerifyServerContract.ps1 -ServerPath <서버 체크아웃> -EditorPath <Unity.exe> -JavaHome <Java 21>` | 실제 HTTP로 재구축. 실행 환경이 없으면 미실행으로 보고 |

`IntegratedVerification`에 추가할 Player 검사:

- 방문 중 스테이지 활성, 나가면 비활성, 카메라 크기 복원
- 객체 수와 `Scene.Objects` 수 일치, 레이어 11, 스테이지 소속
- 객체의 플랫폼 로컬 위치가 해당 `Anchor`의 `CellCenter`와 일치 (이동 전)
- 앞 객체의 정렬 값이 뒤 객체보다 큼
- 클릭으로 선택 변경
- 방문 중 `host.SelectTab`과 스와이프로 탭이 바뀌지 않고, 하단 탭이 숨겨지며, 나간 뒤 대기 요청 없이 정상 복구됨
- 로딩·오류 상태에서 `VisitBack` 동작, 재접속·차단 후 잠금 해제
- 내 홈 배치 불변, 홈 등장 연출 상태 불변

**4.1절 경로 검사 (응답 주입).** 실제 서버 없이 일반 `VerifyIntegration.ps1`에서 돌린다. 실패·재조회 실패·늦은 응답은 실제 서버로 재현하기 어렵기 때문이다.

- 주입 방식: `JamkkaebiApiClient`의 `internal` 생성자로 `SendOnce` 대신 쓸 전달 함수를 받는다. `new JamkkaebiApiClient(baseUrl, token, transport)` 형태이고 공개 API와 기존 HTTP 경로는 바꾸지 않는다. 토큰 갱신 분기는 그대로 지난다. 테스트 전달 함수는 경로별 응답을 돌려주고 요청 수를 세며, 신호를 줄 때까지 응답을 붙잡을 수 있어야 한다.
- 왜 이 방식인가: 로컬 `HttpListener`는 스레드와 포트가 끼어들어 응답 순서를 정하기 어렵고, 늦은 응답(P14)에는 순서 제어가 필요하다.
- 배치: `IntegratedVerification`의 모의 검사 뒤, `VerifyServerFriends` 앞. `ConnectServer`로 서버 모드가 되면 모의로 돌아가지 않으므로 순서를 지킨다. 응답 JSON은 `FriendsServerMapper`와 `ParseSnapshot`이 요구하는 최소 필드로 코드 안에 둔다.

| 시나리오 | 기대 결과 |
| --- | --- |
| 보상 POST 성공, 재조회 성공 | `Page == "Visit"`, `Scene` 같은 객체, 잠금 유지, `VisitReward` 비활성, 전시 GET 추가 없음 |
| 선물 POST 성공, 재조회 성공 | 위와 같음, `VisitGift` 비활성 |
| 보상 POST 실패 | `Page == "Visit"`, 잠금 유지, 오류 알림, `VisitReward` 활성, POST 1회 (자동 재시도 없음) |
| 보상 POST 성공, 재조회 실패 (P13) | `Page == "Visit"`, `VisitReward` 비활성, 알림에 갱신 실패 표시, POST 1회 |
| 선물 POST 성공, 재조회 실패 (P13) | `Page == "Visit"`, `VisitGift` 비활성, POST 1회 |
| 재조회 중 선물·차단 입력 | 요청 수 증가 없음, 처리 중 알림 |
| 차단 성공, 현재 소유자 대상 | `Page == "Friends"`, `Scene == null`, 잠금 해제 |
| 차단 실패 | `Page == "Visit"`, 잠금 유지, 오류 알림 |
| A 차단 POST 대기 → 뒤로 → B 방문 → 응답 도착 (P14) | B 방문 유지, `Scene` 소유자 B, 잠금 유지 |
| A 차단 POST 대기 → 뒤로 → A 재방문 → 응답 도착 | 방문 종료, 잠금 해제 |
| 재조회 뒤 GET 실패 상태에서 `VisitRetry` | 전시 GET이 다시 나감 (`_visitor` 재연결) |
| 방문 중 `ConnectServer` | `Page == "Friends"`, 잠금 해제, 늦은 응답 알림 없음 |

주입 검사의 통과를 실제 서버 검증으로 보고하지 않는다. 마지막의 `VerifyServerContract.ps1`에서 실제 HTTP로 보상 후 `Page == "Visit"` 유지를 별도 확인한다.

사람이 볼 항목: `Friends_Visit.png`와 홈 캡처의 배경·상판 일치, 도깨비 크기, 긴 이름 HUD 줄바꿈. HTTP UnityTest 3개는 `JAMKKAEBI_CONTRACT_CONFIG`가 없으면 생략되므로 일반 EditMode 통과를 서버 검증으로 보고하지 않는다. Android, OAuth, 배포 서버는 범위 밖이며 검증했다고 표현하지 않는다.

## 11. 문서 갱신

- `Docs/friends-snapshot-integration.md`: 파일 구성과 표시 방식의 후속 단락
- `Docs/friends-server-verification.md`: 보상 후 화면 유지, 실행 결과
- `Docs/exhibition-design.md`: 방문 스테이지의 재사용 범위
- `project-context/`: `ARCHITECTURE.md` 친구 절, `CURRENT-STATE.md` 전시관 방문 행, `VALIDATION.md`, `SESSION-LOG.md`
- 이 문서: 완료된 단계와 실제 결정 기록

## 12. 새 세션 시작 문구

```text
Docs/friends-exhibition-rebuilder-plan.md를 읽고 0단계부터 진행해 줘.
```
