# Jamkkaebi

하나의 Unity 프로젝트에서 모바일 UI와 발굴 미니게임을 실행합니다.

## 실행

1. Git LFS를 설치하고 저장소 루트에서 `git lfs pull`을 실행합니다.
2. Unity Hub에 **이 저장소 루트**를 추가하고 Unity **6000.3.23f1**로 엽니다.
3. `Assets/MobileUI/Scenes/UIPrototype.unity`를 열고 Play를 실행합니다.
   `Prototype > Open UI Prototype` 메뉴로 세로형 Game 뷰도 설정할 수 있습니다.
4. 하단 **작업대** 탭에서 발굴 타일을 클릭/터치합니다. 안전·공격·정찰 도구와 새 발굴 버튼을 사용할 수 있습니다.

홈·도감·친구 탭은 기존 UI 시제품의 더미 콘텐츠입니다. 작업대는 기존 `Jamkkaebi`의 미니게임 규칙과 테스트용 유물 데이터를 사용하며, 계정·보상·서버 저장은 연결하지 않았습니다.

## 구조

- `Assets/Jamkkaebi/`: 기존 게임 규칙, 데이터, 프리팹, 단독 미니게임 씬 및 테스트.
- `Assets/MobileUI/`: 공통 UI 셸, 탭 씬, 프로필 표시, 입력 연결 및 작업대 프레젠테이션.
- `Packages/`, `ProjectSettings/`: 프로젝트 전체에서 한 벌만 사용합니다. 기존 URP 및 Both 입력 설정을 유지합니다.
- `Assets/MobileUI/Configuration/MainTabs.asset`: 탭 목록과 배경 실행 정책.

기본적으로 숨겨진 탭도 계속 실행되므로 작업대 타이머도 진행됩니다. `Background Policy`를 `Pause While Hidden`으로 바꾸면 숨긴 동안 멈추고, `Restart On Return`은 돌아올 때 초기화합니다.

단독 미니게임 개발에는 `Assets/Jamkkaebi/Scenes/Minigame.unity`를 사용할 수 있습니다. 통합 작업대 씬은 이를 바탕으로 구성했고, 공통 UI 안에서는 포인터 입력을 사용합니다.

자세한 내용은 [통합 안내](Docs/mobile-ui-integration.md)와 [씬 연결 규격](Docs/mobile-ui-scene-contract.md)을 참고하세요.

