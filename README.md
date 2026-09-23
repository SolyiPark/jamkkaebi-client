# Jamkkaebi

하나의 Unity 프로젝트에서 모바일 UI와 발굴 미니게임을 실행합니다.

## 실행

1. Git LFS를 설치하고 저장소 루트에서 `git lfs pull`을 실행합니다.
2. Unity Hub에 **이 저장소 루트**를 추가하고 Unity **6000.3.23f1**로 엽니다.
3. `Assets/MobileUI/Scenes/UIPrototype.unity`를 열고 Play를 실행합니다.
   `Prototype > Open UI Prototype` 메뉴로 세로형 Game 뷰도 설정할 수 있습니다.
4. 하단 **작업대** 탭에서 발굴 타일을 클릭/터치합니다. 안전·공격·정찰 도구와 새 발굴 버튼을 사용할 수 있습니다.

홈은 10장의 배경을 합성한 아이소메트릭 전시관입니다. 모든 탭은 좌우 스와이프 또는 하단 버튼으로 이동합니다. 도감·친구는 기존 UI 시제품의 더미 콘텐츠입니다. 작업대는 기존 `Jamkkaebi`의 미니게임 규칙과 테스트용 유물 데이터를 사용하며, 계정·보상·서버 저장은 연결하지 않았습니다.

## 구조

- `Assets/Jamkkaebi/`: 기존 게임 규칙, 데이터, 프리팹, 단독 미니게임 씬 및 테스트.
- `Assets/MobileUI/`: 공통 UI 셸, 탭 씬, 프로필 표시, 입력 연결 및 작업대 프레젠테이션.
- `Packages/`, `ProjectSettings/`: 프로젝트 전체에서 한 벌만 사용합니다. 기존 URP 및 Both 입력 설정을 유지합니다.
- `Assets/MobileUI/Configuration/MainTabs.asset`: 탭 목록과 배경 실행 정책.

작업대는 `Background Policy`가 `Pause While Hidden`으로 설정되어 있어 탭을 떠나면 타이머와 게임이 멈추고, 돌아오면 같은 상태에서 이어집니다. 다른 탭은 계속 실행됩니다. `MainTabs`에서 탭별로 `Continue Running`(계속 실행), `Pause While Hidden`(숨김 중 동결), `Restart On Return`(복귀 시 초기화)을 선택할 수 있습니다.

단독 미니게임 개발에는 `Assets/Jamkkaebi/Scenes/Minigame.unity`를 사용할 수 있습니다. 통합 작업대 씬은 이를 바탕으로 구성했고, 공통 UI 안에서는 포인터 입력을 사용합니다.

자세한 내용은 [통합 안내](Docs/mobile-ui-integration.md)와 [씬 연결 규격](Docs/mobile-ui-scene-contract.md)을 참고하세요.


## 홈 전시관

게임 실행 후 홈 첫 표시에서 배경 등장 연출을 한 번 재생합니다. 홈 진입·이탈 시의 배경 시차는 별도 연출이며, 탭을 왕복해도 계속 동작합니다. 짧게 밀고 멈춘 뒤 놓으면 원위치로 복귀하고, 충분히 밀거나 빠르게 밀면 인접 탭으로 이동합니다. 양 끝 탭은 순환하지 않습니다. 전용 드래그 오브젝트는 자체 조작을 우선합니다.

5번 아트워크 상판은 **화면에 선을 표시하지 않는 7×7 논리 격자**입니다. `Assets/MobileUI/Configuration/ExhibitionGrid.asset`에서 원점·두 축·범위를 확인할 수 있습니다. 정령·건물 배치 편집·핀치 줌 조작은 아직 포함하지 않으며, 격자 변환·가려짐 정렬·시점 배율 변경 API를 제공합니다.

좌표 계약과 두 연출의 구분은 [전시관 설계 기록](Docs/exhibition-design.md), 실행 검증은 [전시관 검증 기록](Docs/exhibition-validation.md)을 참고하세요. `ExhibitionSetup.Prepare`는 홈을 교체하는 일회성 마이그레이션이므로 홈을 편집한 뒤 다시 실행하지 마세요.
