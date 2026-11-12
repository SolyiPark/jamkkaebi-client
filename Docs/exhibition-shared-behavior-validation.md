# 홈·방문 전시관 공용 동작 검증

2026-11-05, `feat/friend-exhibition-rebuilder` 미커밋 코드, Unity 6000.3.23f1 / Windows Development Player. [설계·구현 기록](exhibition-shared-behavior-plan.md)의 사용자 승인 계획을 단계별 구현·디버깅 후 검사했다. 기존 미커밋 변경과 작업 중 외부에서 갱신된 카탈로그/친구 검수 관련 변경은 보존했다. 아래 테스트 수는 실행 당시 작업 트리 기준으로 별도 카탈로그 테스트 보완 23개를 포함한다. 이후 사용자 요청으로 이번 기능은 `feat/exhibition-shared-behavior` 브랜치에 분리하며, 별도 테스트·주석 보완은 커밋에서 제외한다.

## 단계별 결과

| 단계 | 실행한 검사 | 결과/증거 |
| --- | --- | --- |
| 1. 화면 정책 | 홈/방문 권한·확대 기본 꺼짐·방문 CanEdit | EditMode 1 통과, `Logs/SharedBehavior/step1.xml` |
| 2. 카메라 공용화 | 기존 화면비 계산·접점 추적·기준 시점 복귀·반복 Capture의 원래 시점 보존 | EditMode 5 통과, `step2.xml` |
| 3. 선택 공용화 | 재선택/해제 이벤트·원래 알파 복원·정책 취소·대상 비활성/파괴·화면별 상태 독립 | EditMode 4 통과, `step3.xml` |
| 4. 화면 어댑터/입력 | 편집 정책의 직접 명령 차단·NPC 이동 금지 중 예약/고정 앵커 보존 | EditMode 2 통과, `step4.xml` |
| 5. 씬 연결 | `FriendsSetup.ValidateVisitStage` → 공용 동작 참조·전시관·씬·회귀 검사 | 종료 코드 0, `Logs/SharedBehavior/step5.log`의 `EXHIBITION_BEHAVIOR_CHECKS_PASSED`, `FRIEND_VISIT_STAGE_VALIDATED` |
| 6. 실제 실행 | 일반 통합 검사 후 최종 실제 HTTP 검사 | 아래 결과 |

첫 Unity batch 시도는 테스트 시작 전 종료 코드 1이었다. 동일 프로젝트를 직접 실행해 step1을 통과했으며 최초 종료의 원인은 확정하지 않았다. 선택 검사 첫 실행의 2건 실패는 EditMode에서 `SendMessage`로 Update를 실행하거나 자동 OnDisable을 가정한 검사 방식 때문이었다. EditMode 로직 호출을 고치고 실제 OnDisable 수명은 Player에서도 확인해 통과했다.

## 일반 통합 실행

`Tools/VerifyIntegration.ps1 -EditorPath 'D:/UnityEditors/6000.3.23f1/Editor/Unity.exe'`를 서버 URL/contract 설정을 비운 프로세스에서 실행했다. EditMode 288 통과·실패 0·실제 HTTP 3개 생략, Windows 빌드/Player 종료 코드 0, Player **377개 통과**, GPU 종료 오류/Exception 0이었다.

증거는 `Logs/Integration/EditMode.xml`, `build.log`, `Player-20261105-231811/verification.txt`, `player.log`다. 이후 가장자리 포커스의 종이 배경 덮임을 보강하고 아래 HTTP 전체 실행을 최종 결과로 사용한다.

## 최종 실제 HTTP 실행

```powershell
./Tools/VerifyServerContract.ps1 `
  -ServerPath 'D:/프로젝트/c3/Capstone_working/tmp/friend-visit-server-20261105' `
  -EditorPath 'D:/UnityEditors/6000.3.23f1/Editor/Unity.exe' `
  -JavaHome 'C:/Program Files/Android/Android Studio/jbr'
```

- EditMode **291 통과·실패 0·생략 0**, HTTP 3개 포함.
- `IntegratedSetup.Build` 성공·종료 코드 0.
- Windows Player **402개 통과**, `ALL CHECKS PASSED`, 종료 코드 0.
- 최종 Player 로그: GPU 종료 경고 3패턴/Exception 모두 0.
- Spring harness `BUILD SUCCESSFUL`, 서버 체크아웃 변경/임시 실행기·세션·stop 파일과 잔류 Unity/Player 없음 확인.

최종 증거는 `Logs/ServerContract/Run-20261105-231937/`의 `EditMode.xml`, `build.log`, `spring.log`, `Player/verification.txt`, `Player/player.log`다. 로그·빌드·캡처는 소스에 포함하지 않는다.

새 `SharedExhibitionVerification`은 실제 MirrorInput을 통한 홈 전시물/NPC 선택·재선택·빈 공간 해제, 방문 선택/해제, 확대 후 화면→RT→월드→셀 왕복, 프레임 간 포커스 유지/이동 접점 추적/원래 시점 복귀, 선택기 비활성화, 홈·방문 상태 독립, 가장자리 셀 포커스 시 종이 배경의 뷰포트 덮임을 검사한다. 임시 복제 정책으로 확대를 켜며 실제 정책 자산은 바꾸지 않는다.

기존 검사는 홈 배치·이동·회전·회수·길게 누르기·스와이프, 앞쪽 방문 객체 적중, 방문 탭 잠금, 로딩·오류·재시도·늦은 응답·보상/선물 후 유지와 내 홈/스냅샷 앵커 불변을 검사한다. 480×1040·768×1024의 방문 HUD·기본 카메라 구도와 Idle/Walk 캡처를 시각 확인했다. 포커스 시험 캡처는 `home_SelectionFocus.png`, `visit_SelectionFocus.png`다.

## 적용 범위와 한계

선택 포커스는 기능 기반을 검증했으며 기본 정책은 양쪽 꺼짐이다. 포커스 시험 캡처에서는 가장자리 대상 추적 시 원본 장식 그림의 외곽 경계가 보일 수 있다. 종이 배경의 화면 덮임과 구분하며, 확대를 기본 UX로 켜기 전 장식 아트의 확장/구도 검토가 필요하다. 기본 화면의 장식 배치·7×7 상판 보정은 변경하지 않았다.

실제 Android/iOS 터치·발열·모바일 렌더 비용·운영 OAuth/배포 서버는 검사하지 않았다. Windows 입력 자동화와 로컬 Spring/H2 harness 결과로 구분한다. 홈 저장·보유 ID 매핑·NPC AI·이동 알고리즘 통합은 추가하지 않았다. 커밋·푸시·PR·병합은 수행하지 않았다.
