# 모바일 2D UI 시제품

처음 읽는 사람을 위한 상세 구현 보고서: [HTML 보고서](Documentation/모바일_UI_구현_보고서.html) · [Markdown 보고서](Documentation/모바일_UI_구현_보고서.md). 실행 순서, 구조도, 실제 화면, 탭·프로필·Scene 수정 실습과 검증 범위를 담았습니다.

Unity **6000.3.14f1**. `Assets/Scenes/UIPrototype.unity`를 열고 Play를 누릅니다.
`Prototype > Open UI Prototype` 메뉴로 씬과 세로형 Game 뷰 프리셋을 함께 열 수도 있습니다.

- 홈 / 작업대 / 도감 / 친구: 하단 탭으로 변경합니다.
- 홈에서만 프로필 바가 표시됩니다. 나머지 탭에서는 중앙 영역이 위로 확장됩니다.
- 진한 사각형은 자동으로 움직이고, 옅은 사각형은 드래그할 수 있습니다.
- 중앙의 `터치` 버튼은 해당 씬의 카운터만 증가시킵니다.
- 기본적으로 모든 씬이 로드되어 계속 실행되고, 탭을 왕복해도 상태가 유지됩니다.

## 탭과 애셋 변경

Project 창에서 `Assets/Configuration/MainTabs.asset`를 선택합니다.
Scene Path 필드에는 Scene 파일을 드래그해서 연결할 수 있습니다.

| Inspector 항목 | 의미 |
| --- | --- |
| Tabs | 탭 추가·삭제·순서 변경. 각 탭에 고유 ID와 별도 Scene 경로 지정 |
| Title / Icon | 표시 문구와 선택적 아이콘 |
| Background / Selected Background | 기본·선택 배경 Sprite |
| Selection Marker / Marker Color | 선택 표시 Sprite와 색상 |
| Normal Color / Selected Color | 문구 색상 |
| Show Profile | 해당 탭에서 프로필 표시 여부 |
| Background Policy | 다른 탭에 있을 때의 실행 정책 |
| Initial Tab | 최초 선택 인덱스 |
| Maximum Texture Width | 미러 영상의 최대 가로 해상도 |

애셋이 비어 있으면 사각형·색상·텍스트로 표시합니다. 탭 개수, 순서, 연결 Scene 변경은 **Play를 종료한 뒤** 적용합니다. Scene을 추가한 뒤 `Prototype > Sync Build Scenes From Catalog`를 실행하면 빌드 씬 목록이 갱신됩니다. 시제품은 렌더링 격리용으로 레이어 8–31을 예약하므로 최대 24개 탭을 지원합니다. 모바일에서 탭이 많아지면 터치 영역을 고려해 별도 내비게이션 디자인이 필요합니다.

## 실행 정책과 개발 명령

각 탭의 Background Policy:

- **Continue Running**: 기본값. 숨겨져도 Update·2D 물리가 계속 실행됩니다. 카메라 렌더링만 생략합니다.
- **Pause While Hidden**: 씬의 콘텐츠 루트를 비활성화하고 2D 물리 시뮬레이션을 중단합니다. 돌아오면 같은 인스턴스를 다시 활성화합니다.
- **Restart On Return**: 숨겨진 동안 정지하고, 재방문할 때 해당 씬을 언로드·재로드합니다.

정책은 Play 중에도 변경할 수 있습니다. `Tab Host`를 선택하면 Inspector의 **Restart Current Scene Now** 버튼으로 현재 씬을 즉시 재시작할 수 있습니다. 이미 선택된 하단 탭을 다시 누르는 것은 재시작하지 않습니다.

일시정지는 루트 활성화 방식이므로 Unity의 OnDisable/OnEnable이 호출됩니다. 비활성화로 종료되는 코루틴이나 실제 시계(Time.time)에 의존하는 타이머까지 자동으로 정지·복원하는 범용 일시정지 기능은 아닙니다. 새 콘텐츠는 누적 deltaTime 기반 상태를 사용하고 코루틴 재개 정책을 별도로 구현하세요. DontDestroyOnLoad, 정적 변수, 파일 저장은 씬 재시작으로 초기화되지 않습니다.

## 프로필 데이터

`Profile Data (dummy publisher)`의 `ProfileDataSource`가 더미 정보를 제공합니다. UI는 `ProfileBar`에서 표시합니다. 실제 데이터가 준비되면 공급 코드에서 아래처럼 호출합니다.

```csharp
source.SetProfile(new ProfileData {
    nickname = "플레이어",
    consecutiveDays = 12,
    avatar = profileSprite
});
```

홈을 떠나 있는 동안 갱신된 데이터도 홈으로 돌아오면 표시됩니다. 오른쪽 별표는 기능 없는 자리 표시자입니다.

## 새 콘텐츠 Scene 연결 계약

1. 기존 더미 Scene을 복제하는 것이 가장 간단합니다.
2. 모든 콘텐츠를 최상위 **MirrorSceneRoot** 오브젝트 아래에 둡니다. 전용 orthographic Camera를 `sceneCamera`에 연결합니다.
3. UI Canvas는 **Screen Space - Camera**로 만들고 그 카메라를 연결합니다. Screen Space - Overlay는 카메라 영상에 담기지 않습니다.
4. 씬 내부에는 EventSystem과 AudioListener를 추가하지 않습니다. 공통 UI가 EventSystem을 제공합니다.
5. 탭 카탈로그에 `Assets/Scenes/Example.unity`처럼 경로를 등록하고 빌드 목록을 동기화합니다.
6. 런타임에 생성하는 오브젝트는 `MirrorSceneRoot.RegisterSpawnedObject(obj)`로 부모·렌더링 레이어를 맞춥니다. 별도 Scene에 생성됐다면 먼저 `SceneManager.MoveGameObjectToScene`으로 소속 Scene도 맞추세요.

공통 씬은 additive로 콘텐츠를 로드하고, 씬마다 별도의 2D 물리 공간을 생성·시뮬레이션합니다. 활성 씬 카메라의 RenderTexture를 RawImage로 표시합니다. 중앙 입력은 화면 좌표를 텍스처 좌표로 변환한 뒤 활성 씬의 GraphicRaycaster 또는 2D Collider에 전달합니다.

현재 입력 범위는 **단일 포인터의 누르기·놓기·클릭·드래그**입니다. 2D Collider는 z=0 평면에서 선택하며, 씬 오브젝트는 Unity EventSystem의 포인터/드래그 인터페이스로 입력을 받습니다. 전역 Input을 직접 읽는 콘텐츠는 자동으로 입력이 격리되지 않습니다. 멀티터치, 핀치 줌, 키보드 탐색, 텍스트 입력, 호버·스크롤 휠·드롭은 이후 확장 범위입니다. 3D 물리는 포함하지 않습니다.

## 검증

개발용 Windows 빌드 생성: Unity 명령줄 `-batchmode -projectPath <프로젝트> -executeMethod PrototypeSetup.BuildPreview -quit`.

생성한 플레이어를 `--verify-output=<결과 폴더 절대 경로>` 인수와 함께 실행하면 실제 씬 로드, 입력 전달, 실행 정책, 해상도 변경을 검증하고 화면 PNG와 `verification.txt`를 저장합니다. 인수가 없으면 일반 시제품으로 실행합니다. 실제 Android/iOS 기기의 터치·Safe Area 검증은 별도입니다.

2026-09-18 Windows 개발 빌드의 자동 통합 검증 **28개 통과**. 결과: `Screenshots/verification.txt`. 540×960, 480×1040, 768×1024 화면 캡처를 같은 폴더에 저장했습니다. 테스트는 포인터 이벤트를 입력 브리지에 주입해 실제 GraphicRaycaster·2D Collider 선택과 이벤트 전달을 검사합니다.

`Prototype > Generate Initial Demo`는 **기존 더미 씬과 탭 설정을 덮어쓰는 초기 생성 도구**입니다. 사용자 편집 후에는 다시 실행하지 마세요.

한글 폰트: Noto Sans CJK KR, SIL Open Font License. 라이선스는 `Assets/Fonts/OFL.txt`에 포함되어 있습니다.
