# 모바일 UI 통합

## 기준

- 기존 GitHub main: `33a93715934ac9c00abf3febc6a2e83dde8ec659`.
- UI 원본: 별도 로컬 `Mobile2DUIPrototype`의 Assets. 원본 프로젝트는 수정하지 않았습니다.
- 통합 브랜치: `feat/integrated-mobile-ui`. 로컬 검증용이며 자동 업로드하지 않습니다.
- Unity 6000.3.23f1, 기존 URP 17.3.0, uGUI 2.0.0, Both 입력 설정을 사용합니다.

## 통합 변경

UI의 자산과 .meta는 `Assets/MobileUI`로 옮겼으며 원래 GUID를 유지했습니다. 씬 경로 문자열과 에디터 메뉴는 새 위치로 갱신했습니다. 단일 프로젝트의 Packages와 ProjectSettings는 기존 게임을 기준으로 유지하고 시작 씬·세로형 미리보기 설정만 연결했습니다.

작업대는 기존 미니게임 씬의 어댑터·그리드·유물 데이터를 사용합니다. 원래 타일 프리팹의 변형인 `WorkshopTile.prefab`에 시제품의 사각형 스프라이트와 URP Unlit 재질을 지정했습니다. 기존 원본 씬과 프리팹은 보존됩니다. 통합 씬에는 별도의 EventSystem/AudioListener를 두지 않습니다.

`TileView`는 포인터 클릭을 선택적으로 받을 수 있습니다. 단독 미니게임의 기존 마우스 입력은 유지하고, 통합 작업대에서는 화면 좌표를 RenderTexture 좌표로 변환하는 `MirrorInput`을 거칩니다. 어댑터는 숨긴 카메라의 입력을 거부하며 새 타일에 부모의 렌더링 레이어를 적용합니다.

기존 타이머 코루틴은 오브젝트 비활성화 후 자동 재개되지 않아 프레임별 deltaTime 갱신으로 바꿨습니다. 미니게임 규칙은 동일하며 시간 표시는 연속 갱신됩니다. 비활성화 중 이벤트를 해제하고 다시 활성화할 때 구독을 복구합니다. 새 발굴 시 이전 타일의 입력을 즉시 끄고 제거합니다.

기존 빌드 목록에 남아 있던 존재하지 않는 SampleScene 경로를 제거하고 UI 셸과 네 콘텐츠 씬, 기존 단독 Minigame 씬을 등록합니다. 기존 프로토타입의 더미 씬 재생성 메뉴는 통합 콘텐츠를 덮어쓰지 않도록 제거했습니다.

## 재검증

1. Unity Test Runner에서 EditMode 테스트를 실행합니다.
2. 커맨드라인 `-batchmode -quit -projectPath <프로젝트> -executeMethod IntegratedSetup.Build -logFile <로그>`로 Windows Development Player를 빌드합니다.
3. `Builds/Integrated/Jamkkaebi.exe --verify-output=<결과폴더>`를 실행하면 통합 검증을 수행하고 종료합니다. 결과는 `verification.txt`와 PNG에 저장됩니다.
4. 검증 옵션 없이 실행하면 일반 게임 화면이 열립니다.

`IntegratedSetup.Prepare`는 최초 이관용으로 씬을 다시 생성하는 명령입니다. 이미 씬을 편집한 뒤에는 다시 실행하지 마세요. 일반 개발·빌드에는 필요하지 않습니다.

자동 검증은 실제 Development Player에서 씬 로드, UI/2D 포인터 전달, 타일 모델과 표시 갱신, 도구 전환, 숨김 실행·일시정지·재시작, 화면 비율 변경 및 렌더 결과를 확인합니다. Unity의 포인터 이벤트를 프로그램으로 전달하며 실제 손가락 입력이나 기기 노치·성능을 대신하지 않습니다. Android/iOS 기기 및 배포 빌드 검증은 별도입니다.

## 도감 화면 후속 구현 (2026-10-09)

기존 도감 탭의 더미 버튼·사각형을 시대 목록·시대 상세·유물 상세로 교체했다. 고정 정의는 `Assets/MobileUI/Configuration/CollectionCatalog.asset`, 수집 규칙은 `Jamkkaebi.Runtime`의 `Gameplay/Collection/`, 표시와 제스처는 `MobileUI.Runtime`의 `Collection/`에 둔다. 수집 인정 시점은 3페이즈 전체의 복원 완료이며, 현재 작업대는 변경하지 않고 샘플 완료 기록을 주입한다. 실제 연결 API와 미구현 범위는 `collection-integration.md`에 기록한다.

`CollectionSetup.Apply`는 이번 도감 씬을 변경한 일회성 batch 이관이다. 이미 이관된 씬에서는 실행을 거절하며 일반 실행·빌드·콘텐츠 교체에 사용하지 않는다. 현재 도감은 씬에 연결된 카탈로그와 글꼴로 런타임 UI를 생성한다. 실제 스프라이트는 카탈로그의 유물·정령 이미지 참조에 연결한다.

유물과 시대는 「깨비별 특성」 시트의 확정 9종을 사용한다. `CollectionCatalogData`는 고정 정의, `CollectionDemoData`는 더미 완료 기록만 담당한다. 이후 정의를 갱신하려면 Play를 종료하고 `Prototype > Collection > Sync Catalog From Confirmed Data`를 실행한다. 같은 유물 ID의 이미지 참조와 자산 GUID를 보존하며 씬은 재구성하지 않는다. 원본 범위와 ID 대응표는 `collection-integration.md`를 참고한다.

검증 실행 결과는 `Docs/integration-validation.md`에 기록합니다.

도감 이미지 교체는 `CollectionCatalog.asset`의 시대·유물 그림 슬롯과 `CollectionAppearance.asset`의 공통 UI 그림 슬롯을 사용한다. 이미지가 없으면 기존 색상·벡터 표현을 유지한다. 실제 그림은 원본 색상과 종횡비를 사용하고, 배경·프레임은 Sprite Border가 있으면 9-slice로 표시한다. `Sync Catalog From Confirmed Data`는 같은 시대·유물 ID의 모든 그림 참조를 보존한다. 슬롯별 적용 위치와 우선순위는 `collection-integration.md`에 기록한다.
