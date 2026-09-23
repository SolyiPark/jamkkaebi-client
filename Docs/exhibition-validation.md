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
