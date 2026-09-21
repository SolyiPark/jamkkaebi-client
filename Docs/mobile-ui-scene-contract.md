# 통합 씬 연결 규격

프로젝트 루트는 이 저장소입니다. Unity 6000.3.23f1, URP, uGUI, Both 입력 설정을 기준으로 합니다.

- 시작 씬: `Assets/MobileUI/Scenes/UIPrototype.unity`.
- 탭 설정: `Assets/MobileUI/Configuration/MainTabs.asset`.
- 홈·작업대·도감·친구: `Assets/MobileUI/Scenes` 안의 각 씬.
- 기존 게임 코드: `Jamkkaebi.Scripts.*`, UI 연결 코드: `MobilePrototype`.

콘텐츠는 최상위 `MirrorSceneRoot` 아래에 두고 전용 orthographic Camera를 연결합니다. UI Canvas는 Screen Space - Camera를 사용하고 같은 카메라를 지정합니다. EventSystem과 AudioListener는 공통 셸에만 둡니다. 화면 전체를 가리는 추가 UI 셸이나 전역 부트스트랩을 콘텐츠에 만들지 않습니다.

탭은 별도 Physics2D 씬으로 Additive 로드됩니다. 층 8~31은 탭별 카메라 격리에 예약되어 있습니다. 런타임 생성 오브젝트는 해당 콘텐츠 씬 및 부모 레이어를 따라야 합니다. 2D 입력에는 Collider2D와 포인터 인터페이스를 사용합니다. 중앙 RawImage의 `MirrorInput`만 선택된 탭에 입력을 전달합니다. 장식 UI는 Raycast Target을 끕니다.

작업대는 Pause While Hidden, 나머지 탭은 Continue Running으로 설정되어 있습니다. 작업대를 떠나면 타이머와 게임이 동결되고 돌아오면 상태를 유지하며 재개합니다. Pause While Hidden은 루트 비활성화 방식이므로 구독 및 코루틴 수명 처리를 구현해야 합니다. Restart On Return은 씬을 다시 로드합니다. 전역 timeScale을 바꾸어 특정 탭만 멈추지 않습니다.

씬을 추가하면 `Prototype > Sync Build Scenes From Catalog`를 실행합니다. GUID와 .meta를 보존하고 Library/Temp/Builds/Logs를 소스에 추가하지 않습니다. 공통 셸, 패키지, 입력 설정, 빌드 목록을 변경하면 전체 탭 검증을 수행합니다.

`IntegratedVerification`은 현재 홈 더미·작업대 미니게임·도감 더미·친구 더미 구성을 검증합니다. 콘텐츠가 바뀌면 검증도 함께 갱신합니다. 원래 네 더미 씬 전용 `PrototypeVerification` 결과를 통합 검증으로 사용하지 않습니다.
