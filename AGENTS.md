# Jamkkaebi 클라이언트 작업 지침

이 저장소 루트가 Unity 프로젝트 루트입니다. 사용자와의 설명 및 새 문서는 한국어를 기본으로 합니다. 코드·커밋·브랜치의 상세 규칙은 `Docs/csharp-convention.md`를 따릅니다.

## 작업 시작

1. 현재 브랜치와 변경 파일을 확인하고 사용자의 기존 변경을 보존합니다. main 변경은 별도 작업 브랜치에서 수행하며 이미 적절한 작업 브랜치가 있으면 이어서 사용합니다.
2. `../project-context/README.md`가 있으면 `CURRENT-STATE.md`와 `SESSION-LOG.md`의 최신 항목까지 읽습니다. 별도 클론에서 이 폴더가 없으면 아래 원본 문서로 바로 진행합니다.
3. `Docs/csharp-convention.md`와 작업 영역의 필수 문서를 읽고 코드·씬·설정 자산을 확인합니다. README와 누적 설계 문서의 초기 설명을 현재 구현으로 단정하지 않습니다.
4. 변경에 맞는 검증을 정하고 실제 실행한 검사와 미실행 범위를 구분해 보고합니다.

## 실행과 구조

- Unity **6000.3.23f1**을 사용합니다. `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`과 lock 파일이 기준입니다. 작업과 무관한 Unity·패키지·URP·Both 입력 설정 변경을 피합니다.
- 시작 씬은 `Assets/Jamkkaebi/Scenes/UIPrototype.unity`입니다. 홈·작업대·도감·친구 순서와 정책은 `Assets/Jamkkaebi/Configuration/MainTabs.asset`에서 설정합니다.
- 모든 자산은 `Assets/Jamkkaebi/` 아래에 있습니다(2026-10-29 `Assets/MobileUI` 병합). `Scripts/Gameplay/`는 발굴 Core·Data·Presentation, 도감 진행 모델, NPC 규약을, `Scripts/UI/`는 Shell·Common·Workshop·Collection·Exhibition·Friends·Dev 폴더로 셸·입력·화면·서버 연동을 담당합니다.
- 의존 방향은 `Jamkkaebi.UI` → `Jamkkaebi.Gameplay`입니다. 게임 모델에 UI 표시 타입을 참조시키지 않습니다.
- 작업대는 로컬 난이도 테이블의 단판(씬은 Normal, 제한시간 없음), 도감은 샘플 진행, 친구는 서버 주소가 없으면 모의 모드입니다. 서버 코드의 존재를 로그인·홈 저장·보상 전체 연결 완료로 해석하지 않습니다.

## 작업별 필수 문서

| 영역 | 먼저 읽을 문서 |
| --- | --- |
| 셸·씬·입력·탭·작업대 통합 | `Docs/mobile-ui-scene-contract.md`, `Docs/mobile-ui-integration.md` |
| 도감·복원 등록·카탈로그·그림 | `Docs/collection-integration.md`, `Docs/collection-validation.md` |
| 전시관·하우징·탭 전환 연출 | **반드시 `Docs/exhibition-design.md`**, 필요 시 `Docs/exhibition-validation.md` |
| 홈 배치·서버 ID·송신 | `Docs/exhibition-layout-transmission.md`, `Docs/friends-interaction-review.md` |
| 친구·방문·HTTP·인증 | `Docs/friends-server-verification.md`, `Docs/friends-interaction-review.md`; 모의 흐름은 `Docs/friends-frontend.md`, `Docs/friends-snapshot-integration.md` |
| 빌드·회귀·GPU 종료·패키지 | `Docs/integration-validation.md`, `Docs/pr3-review-validation.md`, `Docs/gpu-buffer-shutdown-validation.md` |

설계 기록을 구현 완료 상태로 해석하지 않습니다. 날짜별 후속 단락과 코드를 함께 확인합니다. 검증 문서의 통과 수는 당시 결과이며 현재 머신의 실행 결과가 아닙니다.

## 코드와 Unity 자산

- 새 코드는 기존 C# 규칙을 적용하고 모델과 MonoBehaviour·UI 표현을 분리합니다. 외부 컬렉션은 읽기 전용 인터페이스로 노출하고 이벤트·코루틴 수명을 명확히 합니다.
- 기존 `Jamkkaebi.Scripts.Gameplay.*`와 `Jamkkaebi.Scripts.UI.*` 네임스페이스를 존중합니다(옛 `MobilePrototype.*`는 2026-10-29 통일됨). 규칙 정리를 이유로 기존 네임스페이스나 Inspector 필드를 일괄 변경하지 않습니다.
- 직렬화 필드명·타입 변경은 씬·프리팹·ScriptableObject와 마이그레이션을 함께 검토합니다. 새 자산·코드의 `.meta`를 포함하고 GUID를 보존합니다. Unity 밖에서 이동할 때도 원본과 `.meta`를 함께 이동합니다.
- PNG·PSD·WAV·MP3는 Git LFS 대상입니다. 자산 문제가 있으면 포인터 여부부터 확인합니다. `Library`, `Temp`, `Logs`, `Builds`, 생성된 프로젝트 파일은 커밋하지 않습니다.
- Unity 자동 변경 설정은 diff로 필요성을 확인합니다. `m_Script` GUID·asmdef를 확인하지 않고 YAML의 옛 어셈블리 표시만으로 Missing Script라고 단정하지 않습니다.

## 보존할 실행 계약

- 셸의 공용 EventSystem·AudioListener·Overlay Canvas, 콘텐츠의 카메라 Canvas·RT·전용 레이어를 유지합니다. 런타임 UI는 올바른 Scene·부모·레이어·raycaster에 연결합니다.
- 작업대 기본 정책은 PauseWhileHidden입니다. 비활성화·복귀와 구독 복구를 고려하며 특정 탭을 전역 timeScale로 멈추지 않습니다.
- MirrorInput의 화면→RT 변환은 한 번입니다. 콘텐츠 입력 소유권·클릭 취소·가로 탭 전환과 세로 스크롤의 구분을 유지합니다.
- 홈의 **5번 아트워크 상판 각 변을 7등분한 보이지 않는 7×7 논리 격자**와 보정값을 유지합니다. 최초 접속 등장 연출과 스와이프 배경 시차를 독립적으로 취급합니다.
- 하우징 배치·이동·회전은 격자·전시물·NPC 현재/목적지 점유를 검증합니다. 카메라 격리 Layer와 바닥 접점 Sorting Order를 구분합니다.
- 도감 등록은 개별 페이즈 성공이 아닌 **전체 최종 복원 완료**입니다. 종류 ID, 하우징 종류/인스턴스 ID, 서버 relicId/buildingId(옛 artifactId)를 구분하며 공유 진행은 같은 카탈로그 자산을 사용합니다.
- NPC 송신은 고정 PlacementCell을 사용합니다. 전체 배치 PUT은 미관리 배치·숨김 NPC를 보존하고 회수 대상만 제외합니다. 빈 목록의 전체 회수 의미를 지킵니다.
- 미바인딩/미보유/중복 ID·격자 차이·다칸/회전 등 서버 미지원 배치는 송신 전에 거부합니다. 20×20 스냅샷을 7×7로 임의 축소·보정하지 않습니다.
- 친구 전시 GET과 보상 POST를 분리합니다. 방문 연출은 원본 스냅샷·내 홈을 수정하지 않습니다. 서버 실패 시 mock으로 전환하지 않고 변경 요청을 네트워크 실패 때문에 임의 반복하지 않습니다.
- 최상위 소셜 코루틴 소유권·재접속 취소·늦은 응답 세대 검사·공유 인증 갱신 취소를 유지합니다. 토큰을 자산·PlayerPrefs·로그·문서에 기록하지 않습니다.

## 씬 재생성과 검증

- `IntegratedSetup.Prepare`, `ExhibitionSetup.Prepare`, `CollectionSetup.Apply`, `FriendsSetup.Apply`는 일회성 이관입니다. 기존 씬 편집 후 일반 실행·검증 목적으로 재실행하지 않습니다.
- 반복 갱신은 `PrototypeSetup.SyncBuildScenes`, `CollectionSetup.SyncCatalog`, `CollectionSetup.ConfigureImages`를 목적에 맞게 사용합니다. 카탈로그 동기화는 Play 종료 후 수행하고 ID별 그림·GUID를 보존합니다.
- 규칙은 관련 EditMode 테스트, 씬·입력·UI·생명주기는 Player 통합 검증으로 확인합니다. 단순 문서 변경에는 Unity 전체 검증을 요구하지 않습니다.
- 공통 검증: `Tools/VerifyIntegration.ps1 -EditorPath <Unity.exe>`. 모의 기본 상태를 전제로 하므로 프로세스의 `JAMKKAEBI_SERVER_URL`과 친구 씬 URL을 비웁니다. EditMode → `IntegratedSetup.Build` → Windows Development Player → 종료 후 로그 검사입니다.
- 서버 검증: `Tools/VerifyServerContract.ps1 -ServerPath <별도 서버 체크아웃> -EditorPath <Unity.exe> -JavaHome <Java 21 폴더>`. 외부 체크아웃의 임시 실행기·세션 파일 생성 범위를 확인합니다.
- HTTP UnityTest 3개는 `JAMKKAEBI_CONTRACT_CONFIG`가 없으면 생략됩니다. 일반 EditMode 통과를 실제 서버 검증으로 보고하지 않습니다.
- 개별 검사는 `IntegratedSetup.ValidateScenes`, `ExhibitionChecks.Run`, `ReviewRegressionChecks.Run`입니다. `ExhibitionSetup.ValidateAndBuild`는 검사 후 빌드하며 Prepare를 호출하지 않습니다.
- 보고서뿐 아니라 종료 코드·XML·최종 Player 로그를 확인합니다. Windows 2D Animation 13.0.6 종료 우회는 코드·version define·link.xml을 함께 유지하고 패키지 변경 시 재검토합니다.
- 실행 환경이 없으면 미실행 검사와 조건을 명시합니다. Windows 결과를 모바일·OAuth·배포 서버 검증으로 확대하지 않습니다.
- 종료 전 `git diff --check`와 범위를 확인합니다. 맥락 폴더가 있으면 구조·계약·상태 변화와 실제 검증 결과를 관련 문서와 작업 기록에 반영합니다.

## 커밋과 브랜치

- 커밋 제목의 설명과 본문은 반드시 한국어로 작성합니다.
- 형식은 `<type>: <한국어 설명>`입니다. type은 `feat`, `fix`, `refactor`, `test`, `art`, `chore`를 사용하며 scope는 붙이지 않습니다.
- 코드 식별자, 파일명, 제품명은 원문을 유지할 수 있습니다.
- 병합·되돌리기 커밋도 한국어로 작성하고 영어 자동 생성 메시지를 그대로 사용하지 않습니다.
- 이미 공유된 과거 커밋은 메시지 변경을 위해 임의로 재작성하지 않습니다.
- 하나의 커밋에는 하나의 논리적 변경을 담습니다. 예: `chore: 프로젝트 작업 지침과 검증 경로 정리`.
- 브랜치는 `<type>/<짧은-영어-kebab-case>`입니다. main은 실행 가능한 상태를 유지하며 별도 브랜치에서 작업 후 PR로 합칩니다. 커밋·푸시·PR·병합은 사용자 작업 범위에 맞춰 수행합니다.
