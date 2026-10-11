# 친구 탭 검증 결과

이 문서는 스냅샷 계층을 통합하기 전의 최초 프론트엔드 검증 기록이다. 후속 변경의 최신 검증 결과는 [스냅샷 모의 기능 통합 보고서](friends-snapshot-integration.md)를 참고한다.

검증일: 2026-10-11. Unity 6000.3.23f1, Windows Development Player에서 PDF 기반 친구 프론트엔드를 검증했다. 최종 코드의 EditMode 테스트 73개와 Player 검사 202개가 통과했다. 검토 범위와 실행 방법은 [친구 프론트엔드 보고서](friends-frontend.md)를 참고한다.

## 실행 결과

| 검사 | 결과 | 로컬 증거 |
| --- | --- | --- |
| 친구 씬 최초 이관과 컴파일 | 종료 코드 0 | `Logs/Friends/setup.log`, `FRIENDS_SCENE_CONFIGURED` |
| 최종 EditMode | 73개 통과, 실패 0 | `Logs/Friends/EditMode-final.xml` |
| 씬 동기화와 검증기 회귀 | 통과, 종료 코드 0 | `Logs/Friends/regression-final.log`, `REVIEW_REGRESSION_CHECKS_PASSED` |
| Windows Development Player 빌드 | 성공, 종료 코드 0 | `Logs/Friends/build-final.log`, `INTEGRATED_BUILD_SUCCESS` |
| 최종 Player 통합 검사 | 202개 통과, 종료 코드 0 | `Logs/Integration/Player-Friends-Final-20261011-165924/verification.txt` |
| 종료 후 GPU 버퍼 로그 | 검증 스크립트의 미해제 버퍼 경고 패턴 3종 없음 | 같은 폴더의 `player.log` |
| 코드·문서 공백 검사 | `git diff --check` 통과 | 로컬 Git 검사 |

새 EditMode 테스트는 친구가 달라도 하루 보상·선물 제한을 공유하는지, 자정에 초기화되는지, 가득 찬 수신자에게 선물을 보내지 않는지, 차단 후 검색·요청을 막는지, 해제로 친구 관계가 복구되지 않는지, 수락·중복 요청과 코드 입력 검증을 확인한다.

Player는 기존 홈·작업대·도감 검사에 친구 화면 검사를 추가했다. 실제 `MirrorInput`을 통해 세로 드래그, 요청 수락, 프로필 방문, 코드 조회 버튼, 친구 요청 전송, 방문 보상, 선물 확인, 차단 해제를 실행했다. 코드 입력값은 검증기가 입력 필드에 설정하고 이후 버튼 클릭은 공통 입력을 통과한다. 생성 UI의 씬·렌더 레이어도 검사했다.

`Tools/VerifyIntegration.ps1` 실행에서 EditMode와 빌드는 완료했지만 Unity 종료 뒤 프로세스 대기 호출이 반환되지 않아 그 실행을 중단했다. 최종 검증은 같은 Unity 실행 인자와 Player 옵션을 별도 실행하고 `Process.WaitForExit()`로 종료 코드를 회수했다. 종료 후에는 동일한 GPU 로그 패턴도 검사했다. 스크립트 자체가 끝까지 반환했다고 보고하지 않는다.

## 화면 캡처와 시각 확인

최종 캡처 폴더: `Logs/Integration/Player-Friends-Final-20261011-165924/`.

| 화면 | 파일 |
| --- | --- |
| 친구 목록 | [Friends_List.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_List.png) |
| 요청함 | [Friends_Requests.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_Requests.png) |
| 추천 | [Friends_Recommendations.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_Recommendations.png) |
| 코드 추가 시트 | [Friends_Add.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_Add.png) |
| 친구 전시 방문 | [Friends_Visit.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_Visit.png) |
| 차단 목록 | [Friends_Blocked.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_Blocked.png) |
| 긴 세로 화면 | [Friends_List_480x1040.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_List_480x1040.png) |
| 태블릿 비율 | [Friends_List_768x1024.png](../Logs/Integration/Player-Friends-Final-20261011-165924/Friends_List_768x1024.png) |

기본 여섯 화면과 두 추가 비율의 캡처를 확인했다. 첫 실행에서 큰 한글 제목이 줄 높이 때문에 생략되어 텍스트의 세로 Overflow를 수정했다. 최종 캡처에서 제목·친구명·전시 정보가 표시된다. 작은 화면의 목록 끝은 마스크로 잘리고 세로 스크롤로 접근한다. 하단 공통 탭은 별도로 유지된다.

통합 검증 중 요청을 수락하므로 뒤쪽 캡처에는 친구 13명·받은 요청 2개가 나타난다. 일일 행동 검사 후의 크기별 캡처에는 방문·선물 완료 상태가 표시된다. 로그, 캡처와 빌드는 Git 제외 경로이며 이 로컬 작업공간에서 열람할 수 있다.

## 검증 범위의 한계

서버 통신, 실제 스냅샷 재구현, 계정 간 상태 동기화, 초대 링크와 QR 공유, 받은 선물의 홈 개봉은 구현·검증 범위에 포함하지 않았다. 도깨비·건물은 PDF와 같은 임시 도형이다. 로컬 샘플 상태는 Play 재실행 시 초기화된다.

실제 Android/iOS 키보드·터치, 클립보드 권한, 노치·안전 영역, 성능과 배포 빌드는 별도 확인이 필요하다. Windows Player의 포인터 검사가 모바일 기기 검증을 대신하지 않는다.

변경은 `feat/friends-frontend` 브랜치의 로컬 작업으로 남겼다. 작업 전에 있던 `AGENTS.md` 수정은 보존했다. Unity 실행 중 자동 갱신된 무관한 렌더링·프로젝트 설정은 원래 상태로 복원했다.
