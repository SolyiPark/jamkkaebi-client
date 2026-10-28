# 도감 Era Emblem CanvasRenderer 오류 및 수정 보고서

2026-10-28, 기준 코드 `f0efbc8`. Unity **6000.3.23f1**, uGUI **2.0.0**, 기존 `UIPrototype.unity`와 기본 카탈로그에서 보고된 오류를 **재현한 뒤 수정했다**. 수정 브랜치는 `fix/collection-artwork-renderer`다. 아래 재현 기록은 수정 전 실행이며, 수정 후 결과는 별도 절에 기록한다.

## 재현 방법과 실제 결과

1. `Assets/Jamkkaebi/Scenes/UIPrototype.unity`를 연다.
2. Editor Play Mode로 실행한다.
3. 공통 셸이 도감 콘텐츠를 Additive 로드하면서 `CollectionPresenter.Awake`가 시대 목록을 생성한다.
4. `RenderCurrent`의 `Canvas.ForceUpdateCanvases`에서 다음 예외가 발생한다.

```text
MissingComponentException: There is no 'CanvasRenderer' attached to the "Era Emblem" game object, but a script is trying to access it.
```

이번 재현은 그래픽을 사용하는 batch Editor에서 실제 Play Mode에 진입해 수행했다. 임시 에디터 검사기가 시작 씬을 열고 Play를 실행한 뒤 컴포넌트 상태를 기록했다. 도감 탭 선택도 시도하도록 구성했지만, 초기 콘텐츠 로드 단계에서 이미 오류가 발생해 셸이 준비되지 않았고 탭 선택까지 진행하지 못했다. 마지막 `TabHost.ActiveIndex`는 `-1`이었다. 따라서 도감 버튼을 누르기 전 초기화만으로도 재현된다.

생성된 `Era Emblem` 세 개 모두 `CollectionArtwork`는 있지만 `CanvasRenderer`가 없었다. 이후 Canvas 갱신에서도 같은 예외가 반복됐다.

## 원인 확인

- `CollectionPresenter.Artwork`(`CollectionPresenter.cs:584`)는 그림이 없으면 새 RectTransform에 `CollectionArtwork`를 추가한다.
- 수정 전 `CollectionArtwork`(`CollectionArtwork.cs:9`)는 `MaskableGraphic`을 상속하며 `CanvasRenderer` 요구 특성을 선언하지 않았다.
- 현재 설치된 uGUI의 `Graphic`은 `RectTransform`만 요구한다. `Image`는 별도로 `CanvasRenderer`를 요구하므로 Image를 추가하는 경로와 차이가 있다.
- `RectMask2D`가 해당 그래픽을 클리핑하면서 없는 렌더러에 접근한다. 실제 스택은 `CanvasRenderer.EnableRectClipping` → `MaskableGraphic.SetClipRect` → `RectMask2D.PerformClipping` → `CanvasUpdateRegistry.PerformUpdate` → `Canvas.ForceUpdateCanvases` → `CollectionPresenter.RenderCurrent:264` → `CollectionPresenter.Awake:73`이다.

수정 지점은 커스텀 `CollectionArtwork` 생성 시 필수 `CanvasRenderer`를 보장하는 부분이다. 앞선 EditMode 테스트의 실패 0개 결과는 이 Play Mode UI 경로의 정상 동작을 보장하지 않는다.

## 수정 전 증거

- `Logs/Integration/collection-reproduction.log`: 실제 Editor Play Mode의 예외와 스택.
- `Logs/Integration/collection-reproduction.txt`: 세 개 오브젝트의 `CanvasRenderer=False`, 셸 준비·탭 선택 상태.
- `Logs/Integration/CollectionReproductionProbe.cs`: 이번 실행에 사용한 임시 검사기 원본. 자동 컴파일되지 않는 로그 폴더에 보관했다. 실행 후 Assets의 임시 스크립트와 `.meta`는 제거했다.

로그는 Git에서 제외된다. 기존 씬·카탈로그·패키지는 변경하지 않았다.

## 수정 사항과 설계 이유

`Assets/Jamkkaebi/Scripts/UI/Collection/CollectionArtwork.cs`의 클래스에 필수 컴포넌트를 선언했다.

```csharp
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CollectionArtwork : MaskableGraphic
```

Unity가 `CollectionArtwork`를 추가할 때 `CanvasRenderer`를 함께 추가한다. 시대·유물·정령·건물의 대체 기호가 같은 컴포넌트를 사용하므로, 생성 호출마다 렌더러 추가 코드를 반복하지 않고 표시 컴포넌트가 의존성을 소유하게 했다. Presenter의 도감 탐색·수집률·잠금·해금 로직은 수정하지 않았다.

향후 디자인을 실제 스프라이트로 교체하거나 대체 기호를 제거해도 이 의존성은 해당 표시 컴포넌트 안에 머문다. 이번에는 프리팹·View 분리와 전면 디자인 개편을 수행하지 않았다. 해당 개편 시에는 표시 계층을 분리하고 Presenter가 표시 데이터를 바인딩하는 방향을 유지한다.

`RequireComponent`는 컴포넌트 추가 시 적용된다. 이미 저장된 프리팹·씬 인스턴스를 일괄 수리하는 마이그레이션은 아니다. 현재 문제 오브젝트는 `CollectionPresenter.Artwork`가 매번 런타임에 생성하므로 해당 경로에서 자동 추가되는 것을 실제로 확인했다.

## 수정 후 실행 검증

같은 Unity 버전의 그래픽 사용 batch Editor에서 `UIPrototype`을 다시 열고 실제 Play Mode로 검증했다. 첫 실행으로 초기 로딩과 도감 탭 진입을 비교하고, 두 번째 실행에서는 공개 화면 이동 API로 아래 순서를 실행했다. 각 단계에서 Canvas 갱신과 현재 기호들의 렌더러 존재 여부를 확인했다.

| 단계 | 실제 화면 | 확인한 CollectionArtwork 수 | CanvasRenderer 누락 |
| --- | --- | --- | --- |
| 도감 탭 진입 | Overview | 3 | 0 |
| 삼국 시대 상세 | Era | 5 | 0 |
| 삼국 금관 상세 | Relic | 2 | 0 |
| 뒤로 가기 | Era | 5 | 0 |
| 뒤로 가기 | Overview | 3 | 0 |

- 수정 후 두 실행 모두 `MissingComponentException` **0건**.
- 각 단계의 `TabHost.ActiveIndex=2`, 도감 탭 선택 완료.
- 시대·유물·건물·배경 맥락 기호와 정령·유물 상세 대체 그림에서 `CanvasRenderer=True` 확인.
- 수정 전에는 `Era Emblem` 3개가 모두 `False`, 셸의 활성 탭이 `-1`이었으나 수정 후 초기화와 화면 이동을 완료했다.

두 실행 로그에는 별개로 Unity Editor 내부 검색 인덱싱의 `ArgumentOutOfRangeException`이 각각 1건 기록됐다. 스택은 `UnityEditor.Search.SearchDatabase.EnumerateAll` → `GetDefaultSearchDatabase` → `SearchInit.IndexationOnStartup`이며, 도감 런타임 스택과 구분한다. 이 오류는 이번 수정 범위에서 처리하지 않았으므로 전체 로그가 오류 0건이라고 판정하지 않는다.

이번 검증은 Editor Play Mode의 기본 대체 기호와 화면 이동 경로다. 실제 마우스·터치 입력, 모든 시대·유물 조합, Windows Player 빌드, Android/iOS 실행은 새로 검증하지 않았다. 전체 EditMode 테스트도 이번 한 줄 변경 후 재실행하지 않았다.

## 수정 후 증거와 정리

- `Logs/Integration/collection-fix-check.log`: 수정 후 첫 초기화·탭 진입 실행 로그.
- `Logs/Integration/collection-fix-navigation.log`: 시대 목록 → 시대 상세 → 유물 상세 → 뒤로 가기 2회의 실행 로그.
- `Logs/Integration/collection-fix-check.txt`: 두 번째 실행의 단계별 화면과 렌더러 상태. 첫 실행의 같은 이름 보고서는 두 번째 실행으로 교체됐다.
- `Logs/Integration/CollectionFixVerificationProbe.cs`: 탐색 검증에 사용한 임시 검사기 원본. Assets의 임시 검사기와 `.meta`는 검증 후 제거했다.

기능 변경은 `CollectionArtwork`의 특성 선언 한 줄이다. 씬·카탈로그·패키지·기존 `.meta`는 수정하지 않았다. 문서의 참조 경로와 `git diff --check`를 확인했다.
