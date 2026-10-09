# 전시관 구현 및 검증

2026-09-23, Unity 6000.3.23f1. 요구사항의 기준은 [전시관 설계 기록](exhibition-design.md)이다.

## 구현 구성

| 구성 | 위치 | 책임 |
| --- | --- | --- |
| 홈 씬 | `Assets/MobileUI/Scenes/Home.unity` | 10개 배경, 전시관 카메라, 하우징 콘텐츠의 부모 |
| 탭 전환 | `Assets/MobileUI/Scripts/TabTransition.cs` | 드래그 진행량, 두 화면 슬라이드, 확정/취소, 프로필 높이 전환 |
| 입력 분기 | `Assets/MobileUI/Scripts/MirrorInput.cs` | 전용 콘텐츠 드래그 우선, 일반 수평 스와이프, 클릭 취소, 단일 포인터 소유권 |
| 논리 격자 | `Assets/MobileUI/Configuration/ExhibitionGrid.asset` | 상판의 7×7 좌표, 두 축, 유효 범위 및 제외 셀 |
| 격자 변환 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionGrid.cs` | 로컬↔격자, 셀 중심, 범위 및 다중 셀 영역 검사 |
| 바닥 입력 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionSurface.cs` | 카메라 텍스처 좌표에서 셀 선택, 셀의 월드 위치 |
| 표시 순서 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionDepth.cs` | 바닥 접점 기준 SortingGroup 정렬 |
| 최초 등장 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionIntro.cs` | 앱 실행 세션에서 홈 최초 표시 시 한 번 재생 |
| 시점·시차 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionView.cs` | 카메라 맞춤, 줌 API, 독립적인 탭 전환 오프셋 |
| 배경 레이어 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionLayer.cs` | 원래 위치 + 등장 위치 + 시차의 합성, 레이어별 불투명도 |

## 입력 및 수명 정책

일반 영역을 왼쪽으로 밀면 다음 탭, 오른쪽으로 밀면 이전 탭으로 이동한다. 기본 확정 거리는 뷰포트 너비의 22%이며 짧고 빠른 플릭도 지원한다. 기준 미만에서 멈췄다 놓으면 취소한다. 첫 탭과 마지막 탭은 순환하지 않는다. 하단 버튼은 동일한 전환 처리를 사용하고, 멀리 떨어진 탭은 목적 탭으로 직접 이동한다.

`IDragHandler`가 있는 콘텐츠는 자체 드래그를 우선한다. 수평 탭 스와이프가 시작되면 기존 클릭을 취소한다. 세로로 시작한 일반 제스처는 탭 전환으로 바꾸지 않는다. 탭 전환 중 추가 버튼 요청은 마지막 요청을 보관해 전환 후 처리한다.

이동이 확정되기 전에는 원래 탭이 활성 탭이다. 숨겨진 `PauseWhileHidden`/`RestartOnReturn` 탭은 저장된 화면만 미리 보여주며, 미리보기를 위해 게임을 재개하지 않는다. 화면 비율 변경 때도 이전 텍스처를 복사해 미리보기를 유지한다. 전환 확정 후에만 목적 탭을 활성화하거나 재시작한다. 포커스 상실·화면 크기 변경 시 진행 중 스와이프를 취소한다.

최초 등장 연출과 배경 시차는 독립된 상태다. 최초 등장은 시간으로 진행하고, 시차는 전환 진행량으로 매번 반응한다. 전환으로 최초 등장을 중단하면 완성 상태로 정리하며, 홈에 다시 돌아와도 최초 등장을 반복하지 않는다.

## 확장 방법

- 새 배치물은 `Housing Content (platform local coordinates)` 아래에 둔다. `MirrorSceneRoot.RegisterSpawnedObject`는 루트 바로 아래로 부모를 바꾸므로, 호출 후 플랫폼의 콘텐츠 부모 아래로 다시 배치하고 로컬 좌표를 지정한다. 콘텐츠 씬 소속과 렌더링 Layer도 유지해야 한다.
- `ExhibitionGrid.CellCenter`로 셀 중심을 얻고, `ContainsFootprint`로 배치 범위를 검사한다. 실제 점유 예약·건물 편집·회전 데이터 저장은 별도 하우징 기능에서 구현한다.
- 큰 건물은 앞면·지붕 등 표현을 나누고 각 정렬 앵커를 구성할 수 있다. 기본 `ExhibitionDepth`는 플랫폼 로컬 바닥 접점의 Y를 사용하며, 줌·시차에 영향을 받지 않는다.
- `ExhibitionView.SetView(center, zoom)`는 향후 줌 입력용 API다. 현재 배율 범위는 0.75~2이며 핀치 제스처는 연결하지 않았다. 핀치를 추가할 때는 단일 포인터 입력 소유권도 확장해야 한다.
- 게임 화면에 격자선을 그리는 오브젝트는 없다. `ExhibitionSurface`의 에디터 격자 보기도 기본적으로 꺼져 있다.

## 에셋 처리

원본 PNG 10장을 `Assets/MobileUI/Art/Exhibition`에 복사하고 기존 Git LFS 규칙을 따른다. 중앙 피벗, 1000 PPU, Full Rect, 자동 물리 윤곽 없음, 밉맵 없음, 최대 2048로 임포트한다. Android/iOS는 ASTC 6×6 설정을 제공한다. 원본 이미지 자체는 수정하지 않았다.

질감 레이어 9번은 불투명도 0.22, 10번은 0.075를 기본으로 한다. 배경 1번은 화면 비율과 작은 시차 이동에 대비해 여유 있게 채운다. 산·구름(2·4·6·7·8번)은 원본 비율을 유지한 채 화면 폭에 맞춰 확대해 양옆의 잘린 경계를 화면 밖으로 보낸다. 플랫폼과 논리 격자는 이 장식용 확대에서 제외한다. 순수 배경은 표시 순서 0~30, 플랫폼은 100, 배치물의 기본 깊이 정렬은 150~1850, 앞쪽 구름·질감은 2000 이상을 사용한다.

`ExhibitionSetup.Prepare`는 홈을 교체하는 일회성 batch 마이그레이션이다. 편집한 홈을 보존하려면 다시 실행하지 않는다. 일반 빌드는 `ExhibitionSetup.ValidateAndBuild`를 사용한다.

## 검증 실행 방법

1. Unity batch mode에서 `-executeMethod ExhibitionSetup.ValidateAndBuild -quit`를 실행한다. 격자·씬 참조 검사와 기존 에디터 회귀 검사를 수행하고 Windows 개발 빌드를 생성한다.
2. `Builds/Integrated/Jamkkaebi.exe`를 `--verify-output=<절대 경로>`로 실행한다. 실제 탭 씬·입력 브리지·렌더링을 사용해 검사하고 결과 텍스트와 화면 PNG를 저장한다.

Android/iOS 실제 기기의 터치 감각, Safe Area와 GPU 성능은 별도의 기기 검증 대상이다. 정령 AI나 실제 건물 에셋은 이번 구현에 포함하지 않는다.

## 이번 검증 결과

- 에디터 검사 **68개 통과**: 49개 셀 왕복 변환, 상판 밖 거부, 다중 셀 범위, 깊이 정렬 기준, 축소 임포트 후 4.32×7.68 좌표 유지, 홈의 격자 에셋 연결, 기존 에디터 회귀 검사.
- Windows 개발 빌드 성공. 실행 검사 **79개 통과**: 전체 탭 양방향 스와이프·버튼 이동·끝 경계·취소, 최초 연출과 시차의 독립성, 전용 오브젝트 드래그, 작업대 오클릭 방지·숨김 타이머·재시작, 줌 API 및 리사이즈 후 좌표 변환, 렌더링과 캡처 검사.
- 540×960, 480×1040, 768×1024 화면을 직접 확인했다. 태블릿 화면에서 보이던 산·구름의 이미지 경계를 주변 풍경 확대 처리로 보정했다.
- 검증 중 발견한 홈의 격자 참조 누락을 수정하고, 빌드 전 필수 참조 검사에 포함했다. 숨김 실행 직후의 검은 캡처를 방지하도록 검증 시작 시 화면 크기를 실제로 변경하며, 캡처의 가시 픽셀 검사도 추가했다.
- `git diff --check` 통과. Android/iOS 실기기 검증과 GPU 성능 측정은 수행하지 않았다.

[실행 검증 결과](ExhibitionScreenshots/verification.txt) · [기본 세로 화면](ExhibitionScreenshots/Home_540x960.png) · [긴 세로 화면](ExhibitionScreenshots/Home_480x1040.png) · [태블릿 화면](ExhibitionScreenshots/Home_768x1024.png) · [스와이프 도중 화면](ExhibitionScreenshots/Home_Swipe_Preview.png)

## 초기 로딩 일시정지 회귀 수정

2026-09-23: `Load`에서 비활성 탭의 배경 실행 정책을 적용한다. 최초 미리보기는 동기 렌더 요청으로 캡처하고 즉시 원래 일시정지 상태를 복원하므로, 캡처를 위해 활성화한 루트에서 업데이트·물리 프레임이 진행되지 않는다.

Unity 6000.3.23f1에서 에디터 검사와 Windows 개발 빌드를 다시 실행해 통과했다. 초기 로딩 중 배경 실행 정책 검사 1개를 추가해 실행 검사 총 80개가 통과했다. 아래 실행 결과 텍스트를 갱신했으며 기존 화면 캡처는 유지했다.

## 하우징과 도깨비 더미 (2026-10-09)

`Home.unity`의 `ExhibitionHousing`이 기존 플랫폼·콘텐츠 부모·폰트·Unlit 재질을 참조한다. 런타임에 초기 1×1 전시물 하나, 도깨비 한 마리, 하단 배치·회수·취소 UI를 생성한다. 기존 홈을 재생성하는 이관 명령은 실행하지 않았다.

- `HousingLayout` / `HousingPlacement`: 셀 앵커·크기·ID, 다중 셀 점유, 겹침·범위 검사, 이동·회수. 실패한 이동은 기존 점유를 유지한다.
- `HousingSurfaceInput`: 별도 입력 오브젝트의 상판 PolygonCollider2D를 통해 공통 MirrorInput의 클릭을 받는다. 비가독성 배경 Sprite의 자동 윤곽 생성 오류를 피하도록 이미지와 충돌체를 분리했다. 드래그 핸들러를 두지 않아 바닥 스와이프가 탭 전환으로 이어지고 배치 클릭은 취소된다.
- `ExhibitionHousing`: 배치 버튼 → 빈칸 터치, 전시물 선택 → 빈칸 터치로 이동, 선택 후 회수·취소. 초기 UI의 크기는 1×1이다. 격자선을 표시하지 않는다.
- `DokkaebiWanderer`: 빈 상하좌우 인접 셀을 무작위로 선택해 이동하고 잠시 대기한다. 막히면 기다린다. 현재·목적지 칸을 예약해 이동 중 전시물 배치를 막는다. 첫 더미는 한 마리만 지원하며 여러 도깨비의 예약 조정은 후속 범위다.
- `ExhibitionDummyVisuals`: 임시 전시대와 뿔·눈을 가진 도깨비를 코드로 그린다. 실제 아트·애니메이션은 후속 교체 대상이다. 플랫폼 콘텐츠 부모, 바닥 접점 정렬, 콘텐츠 씬·렌더 Layer를 공유하며 최초 등장 완료 후 표시한다.
- 실행 중 배치는 탭 왕복 시 유지된다. 앱 재실행 후 저장·복원, 회전 UI, 인벤토리 연계, 핀치 줌, 정식 도깨비 AI는 이번 범위에 포함하지 않는다.

### 이번 실행 결과

Unity **6000.3.23f1** (`D:\UnityEditors\6000.3.23f1\Editor\Unity.exe`)에서 검증했다.

- EditMode **25개 통과**: 기존 발굴 테스트 및 신규 하우징 테스트 5개. 다중 셀 겹침·범위, 이동 실패의 원자성, 회수, ID, 이동 예약·인접 셀 제약을 검사했다.
- `ExhibitionSetup.ValidateAndBuild`의 전시관·씬 참조 및 기존 회귀 검사 **72개 통과**, Windows Development Player 빌드 성공. 내부에서 `ExhibitionChecks.Run`, `ReviewRegressionChecks.Run`, `IntegratedSetup.Build`를 호출했다.
- 최종 Player 자동 검사 **89개 통과**, 종료 코드 **0**. 공통 입력을 통한 버튼·바닥 배치·이동·회수, 중복 배치 거부, 바닥 스와이프 오배치 방지, 도깨비 예약·이동·씬·레이어 및 기존 탭·작업대 검사를 확인했다.
- 종료 로그의 ComputeBuffer 누수·해제 누락 및 Sprite fallback 정리 실패 패턴이 없음을 확인했다.
- 최종 캡처 540×960, 480×1040, 768×1024를 시각적으로 확인했다. 기기 검증은 수행하지 않았다.
- 로그: `Logs/Integration/EditMode.xml`, `housing-build.log`, `HousingPlayer/verification.txt`, `HousingPlayer/player.log`; 최종 캡처는 `Logs/Integration/HousingPlayer/` 아래에 저장했다. 기존 2026-09-23 캡처·결과 링크는 당시 기록으로 유지한다.

`Tools/VerifyIntegration.ps1`의 EditMode 단계는 완료됐으나 해당 실행의 프로세스 대기가 반환되지 않아 종료 후 동일 검증 범위를 개별 프로세스로 수행했다. 최종 빌드·Player 검사 및 종료 로그 검사는 위의 이번 실행 결과에 해당한다.

## 길게 누르기 방향 메뉴 (2026-10-09 후속 검증)

이 변경은 위의 터치 선택·하단 회수 UI를 대체한다. 하단에는 배치·취소만 유지한다.

- `HousingSurfaceInput`은 홈 루트에서 공통 포인터 누름·드래그·놓기를 받는다. 전시물의 표시 영역을 현재 Transform에서 판정하며, 새로 배치한 직후의 물리 좌표 갱신 지연에 의존하지 않는다. 배경과 분리된 상판 충돌체 및 전시물의 BoxCollider2D가 입력 진입점을 제공한다.
- 전시물을 0.45초 누르면 `HousingRadialMenu`가 위쪽 이동·왼쪽 아래 회수·오른쪽 아래 회전을 표시한다. 누른 채 실제 버튼 방향으로 65 UI 단위 이상 밀면 한 번만 실행하고 강조한다. 중심·방향 사이의 이동은 실행하지 않는다. 메뉴는 손을 떼거나 입력을 취소할 때 닫힌다.
- 이동을 선택하면 손을 뗀 뒤 빈칸 터치로 이동한다. 회수는 즉시 제거하고, 회전은 90도씩 방향을 바꾼다. 더미 전시대의 흰색 방향 표시와 유물의 작은 위치 차이로 회전 상태를 표시한다. 실제 방향별 아트는 후속 교체 대상이다.
- `HousingPlacement`는 원래 크기와 0~3의 회전 상태를 가진다. 홀수 회전은 가로·세로 점유 크기를 교환한다. 경계·다른 전시물·도깨비 예약과 충돌하면 기존 방향과 점유를 유지한다.
- `IMirrorGestureOwner`를 통해 길게 누르기 이전에는 탭 스와이프를 허용하고, 메뉴가 열린 이후에는 하우징이 제스처를 소유한다. 기존 전용 드래그 및 다른 탭의 입력은 동일 정책을 유지한다. 클릭 전달은 `eligibleForClick`을 확인해 길게 누르기 종료 후 추가 클릭을 막는다.
- 중첩 검증 코루틴도 실패 보고와 Player 종료로 연결되도록 검증 실행기를 수정했다.

### 이번 실행 결과

Unity **6000.3.23f1**에서 EditMode **27개**, 전시관·에디터 회귀 **72개**, 최종 Windows Development Player 검사 **107개**가 통과했다. 최종 Player 종료 코드는 **0**이며 GPU 버퍼 종료 오류 패턴이 없었다.

신규 검사는 회전의 점유 변경·경계·예약·실패 시 상태 유지, 짧은 터치, 길게 누르기 전 스와이프, 세 방향 선택, 누르는 동안 메뉴 유지·해제 시 닫기, 반복 드래그 중 한 번만 실행, 입력 취소를 포함한다. 실제 메뉴 캡처와 기존 세 화면 비율 캡처를 확인했다. 모바일 실기기 검증은 수행하지 않았다.

로그는 `Logs/Integration/RadialEditMode.xml`, `radial-editmode.log`, `radial-build.log`, `RadialPlayer/verification.txt`, `RadialPlayer/player.log`에 있다. 메뉴 화면은 `RadialPlayer/Housing_Hold_Menu.png`다. 개별 프로세스로 EditMode → 전시관·회귀 검사와 빌드 → Player 검사 → 종료 로그 검사를 수행했다. `git diff --check`도 통과했다.

## 하단 UI도 길게 누르는 동안만 표시 (2026-10-09 후속 검증)

하단 배치·취소 버튼과 안내 문구를 담은 패널을 기본 비활성 상태로 생성한다. 전시물 또는 유효한 상판 셀을 0.45초 누르면 패널을 표시하고, 놓기·입력 취소·비활성화 시 주변 메뉴와 함께 숨긴다. 평소에는 배치·이동·회전 결과 문구도 노출하지 않는다.

빈 상판 길게 누르기는 하단 패널만 표시한다. 누른 채 배치 또는 취소 버튼까지 밀어 선택한다. 선택한 배치·이동 모드는 손을 뗀 뒤 유지하므로 숨겨진 UI 때문에 조작 경로가 끊기지 않는다. 목적지 셀은 손을 뗀 뒤 터치한다. 주변 세 방향 메뉴는 전시물을 누른 경우에만 표시한다.

Unity **6000.3.23f1**에서 EditMode **27개**, 전시관·회귀 **72개**, Windows Development Player **114개** 통과, 종료 코드 **0** 및 GPU 종료 로그 검사 통과. 초기 숨김·빈 상판 길게 누르기·하단 버튼 방향 선택·놓기 후 전체 UI 숨김·배치 상태 유지·입력 취소와 기존 탭/작업대 검사를 확인했다. 최종 평상시 화면과 누르는 중 메뉴 캡처를 시각적으로 확인했다. 모바일 실기기 검증은 수행하지 않았다.

로그: `Logs/Integration/QuietEditMode.xml`, `quiet-editmode.log`, `quiet-build.log`, `QuietPlayer/verification.txt`, `QuietPlayer/player.log`. 화면: `QuietPlayer/01_Home.png`, `QuietPlayer/Housing_Hold_Menu.png`. `git diff --check` 통과.

## 빈 상판 주변 배치·취소 메뉴 (2026-10-09 후속 검증)

하단 패널과 별도 하단 버튼을 제거했다. `HousingRadialMenu`가 전시물의 이동·회수·회전 세 버튼과 빈 상판의 왼쪽 배치·오른쪽 취소 두 버튼을 같은 스타일·방향 판정으로 표시한다. 각 경우에 해당하는 버튼만 활성화한다. 안내는 메뉴 아래에서 함께 열리고 닫힌다. 화면 경계 보정에는 안내 폭도 포함한다.

`HousingAction.Place`는 배치 모드를 시작하고 `Cancel`은 배치·이동 선택을 해제한다. 손을 떼면 메뉴가 닫히며 배치 모드는 다음 빈칸 터치까지 유지한다. 전시물은 여러 하위 Sprite가 하나의 부모 오브젝트·배치 ID·정렬 단위로 묶인 기존 구조를 유지했다.

Unity **6000.3.23f1**에서 EditMode **27개**, 최종 전시관·회귀 **72개**, Windows Development Player **123개** 통과. 최종 Player 종료 코드 **0**, GPU 버퍼 종료 로그 검사 및 `git diff --check` 통과. 빈 상판의 정확히 두 버튼, 전시물의 정확히 세 버튼, 방향 배치·취소, 연속 드래그 중 한 번만 실행, 하단 패널 부재, 메뉴 수명과 기존 탭·작업대 입력을 검사했다. 캡처를 확인한 뒤 빈 상판 문구 잘림을 줄이고 다시 빌드·Player 검증했다. 모바일 실기기 검증은 수행하지 않았다.

로그: `Logs/Integration/LocalMenuEditMode.xml`, `local-menu-editmode.log`, `local-menu-build.log`, `LocalMenuPlayer/verification.txt`, `LocalMenuPlayer/player.log`. 캡처: `LocalMenuPlayer/Housing_Empty_Menu.png`, `LocalMenuPlayer/Housing_Hold_Menu.png` 및 기존 세 화면 비율 캡처.

## 전시물 정의와 도감 연결 지점 분리 (2026-10-09 후속 검증)

`HousingItemDefinition` 자산으로 종류 ID·크기·표현 프리팹·터치 영역을 분리하고 홈의 기본 전시물 참조를 연결했다. 하우징 배치 인스턴스는 종류 ID를 보존한다. `PlacementRequested` → `BeginPlacement(definition)` 경로와 `PlacementAdded`/`PlacementRemoved` 이벤트를 추가해 추후 도감 연결 계층이 선택·배치 결과를 주고받을 수 있다. 실제 도감·인벤토리 연계는 아직 구현하지 않았다.

Unity **6000.3.23f1**에서 EditMode **31개**, 전시관·에디터 회귀 **74개**, Windows Development Player **127개** 통과. 외부 정의의 다른 ID와 2×1 점유를 배치하고 회수하며 이벤트를 받는 경로, 종류 ID의 이동·회전 유지와 같은 종류의 서로 다른 배치 ID, 빈 종류 ID 거부를 확인했다. 기존 길게 누르기·주변 메뉴·탭 입력 검사도 통과했다. Player 종료 코드 **0**, GPU 버퍼 종료 오류 패턴 **0개**, `git diff --check` 통과. 실제 도감 데이터와 모바일 실기기 검증은 수행하지 않았다.

로그: `Logs/Integration/ItemBridgeEditMode.xml`, `item-bridge-editmode.log`, `item-bridge-build.log`, `ItemBridgePlayer/verification.txt`, `ItemBridgePlayer/player.log`. EditMode → 전시관·회귀 검사와 Windows Development 빌드 → Player 자동 검사 → 종료 로그 검사를 개별 프로세스로 수행했다.
