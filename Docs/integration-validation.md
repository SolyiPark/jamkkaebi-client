# 로컬 통합 검증 결과

검증일: 2026-09-21. Unity 6000.3.23f1 / Windows x64 Development Player / 기본 D3D12 렌더링.

| 검증 | 결과 |
|---|---|
| 스크립트 컴파일 | 통과 |
| 기존 EditMode 미니게임 규칙 테스트 | 20 / 20 통과, 실패·건너뜀 0 |
| 빌드 씬 6개의 Missing Script / 끊어진 오브젝트 참조 | 검사 통과 |
| Windows 개발용 실행 파일 빌드 | 성공, 종료 코드 0 |
| 실제 실행 파일 통합 검사 | 49 / 49 통과, 종료 코드 0 |
| 자산 GUID 중복 / .meta 누락 | 0 / 0 |
| 기존 미니게임 Core·Data 및 패키지·Unity 버전 | 원본 main과 동일 |

통합 검사는 셸과 콘텐츠 씬 로드, 공용 EventSystem/AudioListener 단일성, 프로필 갱신, 홈 버튼·드래그, 하단 탭 선택, 탭별 카메라 격리, URP 출력, 56개 발굴 타일 생성, 타일 클릭 1회당 도구 1회 소비, 모델 및 스프라이트 갱신, 정찰과 피드백, 전역 일시정지 입력 차단, 숨겨진 작업대의 시간 진행 및 입력 차단, 숨김 일시정지·복귀·씬 재시작, 새 발굴, 화면 비율 변경을 포함합니다.

검사 화면 크기는 540×960, 480×1040, 768×1024입니다. GPU 출력에서 내용이 그려지고 오류 셰이더 색상이 없는지 검사했고, 저장된 화면도 시각적으로 확인했습니다.

## 화면

![통합 작업대](IntegrationScreenshots/02_Workshop.png)

[홈](IntegrationScreenshots/01_Home.png) · [긴 세로 화면](IntegrationScreenshots/Workshop_480x1040.png) · [넓은 세로 화면](IntegrationScreenshots/Workshop_768x1024.png)

## 한계 및 남은 확인

- 검증 중 앱 오류 로그는 없었습니다. 종료 단계에는 ComputeBuffer 해제 및 D3D12 리소스 정리 관련 경고가 남았습니다. 종료 코드는 0이며, 경고의 원인을 이 작업에서 확정하지는 않았습니다.
- 마우스/터치의 포인터 이벤트 전달을 자동 검사했습니다. 실제 Android/iOS 손가락 입력, 노치, 발열·메모리, 백그라운드 복귀 및 스토어 배포 빌드는 별도 확인이 필요합니다.
- 홈·도감·친구는 더미 콘텐츠입니다. 미니게임은 저장소의 기존 테스트 유물 데이터를 사용하며 보상·계정·서버 연동은 없습니다.
- 초기 검증에서는 숨겨서 시작한 창의 비동기 스크린샷 저장이 실패했습니다. 검증 시작 시 화면 크기를 확정하고 동기 캡처로 바꾼 뒤 49개 검사와 여섯 장 캡처가 모두 성공했습니다.
- Unity가 저장/빌드 과정에서 URP 재질 프로필 기본값과 셰이더 사전 필터링/런타임 설정을 갱신한 파일도 포함됩니다. URP 자체를 교체하거나 패키지 버전을 변경하지 않았습니다.

## 재현 및 로그

Unity Editor를 닫고 저장소 루트에서 실행합니다.

```powershell
.\Tools\VerifyIntegration.ps1 -EditorPath 'D:\UnityEditors\6000.3.23f1\Editor\Unity.exe'
```

`Logs/Integration/EditMode.xml`, `Logs/Integration/build.log`, `Logs/Integration/Player-20260921-190859/verification.txt`가 이번 로컬 실행의 증거입니다. 로그와 빌드 산출물은 Git에서 제외되며, 재검증하면 새 실행 폴더가 생성됩니다.

일반 실행 파일: `Builds/Integrated/Jamkkaebi.exe`. 검증 옵션 없이 실행하면 자동 테스트 없이 조작할 수 있습니다.

이 결과는 위 범위의 통합 검증 통과를 뜻하며 모든 기기에서 무결함을 보장하지 않습니다. 통합 작업은 로컬에만 보관했고 GitHub push/PR 변경/병합은 수행하지 않았습니다.
