# 친구 전시 스냅샷 모의 기능 통합

독립 웹 모의 구현의 스냅샷 조회·재현·임시 이동·실패 처리 흐름을 현재 Unity 친구 탭에 이식했다. 프로필을 누르면 모의 응답 계층에서 전시 전체 패키지를 한 번 받아 방문 전용 객체를 생성한다. 홈의 배치 데이터와 씬은 변경하지 않는다. 실제 HTTP 통신과 운영 서버 인증은 연결하지 않았다.

## 통합한 기능

| 기능 | Unity 구현 |
| --- | --- |
| 전시 패키지 조회 | `IExhibitionBackend.GetExhibition`과 `MockExhibitionBackend`가 비동기 모의 응답을 제공 |
| 조회와 보상 명령 분리 | 조회로 보상을 지급하지 않고 `ClaimVisitReward` 명령으로 로컬 일일 보상을 판정 |
| 저장된 배치 재현 | 소유자·캡처 시각·7×7 격자·객체 목록으로 화면 구성 |
| 좌표 변환과 표시 순서 | 기존 `ExhibitionGrid.CellCenter`를 사용하고 바닥 접점 Y에 따라 UI 형제 순서를 변경 |
| 방문 중 임시 이동 | 인접 셀 이동, 건물 및 다른 도깨비의 현재 셀 회피, 저장 앵커 유지 |
| 방문 읽기 전용 | 배치 편집 기능 없이 객체 선택과 정보 표시만 제공 |
| 빈 전시와 미지원 객체 | 빈 상태 안내, 미지원 ID는 물음표 대체 표현 |
| 손상된 배치 | 잘못된 좌표·ID와 중복 ID는 생략하고 안내, 7×7이 아닌 패키지는 적용 중단 |
| 응답 취소와 순서 역전 | 코루틴 취소와 요청 세대 번호로 복귀 후 응답·이전 요청 응답 무시 |
| 조회 실패 | 오류와 재시도 버튼 표시, 이미 재현한 전시가 있으면 유지 |

웹 모의 구현의 파일을 Unity에 그대로 실행하거나 운영 서버 코드로 취급하지 않았다. C# 화면 모델과 모의 어댑터로 동작을 옮겼다. JSON 파싱·HTTP 상태 코드·Bearer 인증을 검증했던 웹 모의 서버는 이번 Unity 연결에 포함하지 않는다. `Malformed`는 잘못된 응답을 어댑터가 거절한 결과를 모의한다.

## 검토 방법

1. `Assets/Jamkkaebi/Scenes/UIPrototype.unity`에서 Play하고 친구 탭의 프로필을 누른다.
2. 약 0.3초 동안 로딩 표시와 비활성 보상 버튼을 확인한다.
3. 전시 객체를 누르면 이름·각성·레벨이 나타난다. 도깨비는 약 1초마다 이동하지만 저장 배치는 바뀌지 않는다.
4. 방문 보상을 누르면 별도 모의 명령이 완료된 뒤 일일 완료 상태로 바뀐다. 친구를 바꾸어도 같은 날 두 번 받지 않는다.
5. 뒤로 돌아가거나 조회 중 화면을 닫아도 늦은 응답이 방문 화면을 다시 열지 않는다.
6. 홈으로 돌아가 기존 하우징 배치가 유지되는지 확인한다.

예외 시나리오는 게임 화면에 개발용 버튼을 추가하지 않고 Play 중 친구 씬의 `FriendsPresenter` Inspector에 있는 **Visit Scenario**로 선택한다. 다음 방문부터 반영된다.

| 시나리오 | 확인할 결과 |
| --- | --- |
| Normal | 공개 전시 샘플: 도깨비 3개, 건물 2개 |
| Empty | 저장된 전시 없음, 객체 0개 |
| Unknown | 미지원 건물 ID 999의 대체 표시 |
| Invalid | 범위 밖 객체가 생략됨 |
| Duplicate | 중복 객체가 생략됨 |
| Mismatch | 20×20 응답을 적용하지 않고 오류 표시 |
| Denied / Error / Malformed | 접근 불가·조회 실패·잘못된 응답 안내와 재시도 |
| Slow | 약 1.8초 로딩 중 뒤로 돌아가기와 요청 교체 검사 |

## 모의 계약과 실제 연결 시 결정할 사항

현재 `ExhibitionSnapshot`은 서버 DTO에서 변환된 것으로 가정한 클라이언트 패키지다. 저장 배치는 불변 `SnapshotPlacement`, 방문 중 현재 셀은 별도 `VisitObject`에 보관한다. 숫자 ID 1~3과 단일 셀 건물은 모의 콘텐츠 기준이며 실제 콘텐츠 ID·프리팹·건물 점유 크기를 확정한 계약이 아니다.

이번 샘플은 이미 공개된 객체 목록을 제공한다. 실제 서버 어댑터는 표시 여부·숨김 정책을 적용한 공개 데이터만 전달해야 한다. 클라이언트에서 필터링하는 것만으로 비공개 데이터를 보호할 수 없다.

실제 연결 시에는 HTTP 어댑터, 계정 인증, 응답 DTO 매핑, 서버 시각, 보상 멱등성, 스냅샷 버전과 콘텐츠 대응표를 추가한다. 임시 이동 상태를 배치 저장 API에 업로드하지 않는다. 선물은 기존 프론트엔드의 로컬 미리보기로 유지한다.

## 파일 구성

`Assets/Jamkkaebi/Scripts/UI/Friends/`의 `IExhibitionBackend`, `MockExhibitionBackend`, `ExhibitionSnapshot`, `SnapshotPlacement`, `ExhibitionVisitSession`, `ExhibitionVisitScene`, `VisitObject`가 조회와 재현을 담당한다. `FriendsPresenter`는 이 계층을 호출하고 표시한다. 기존 `Friends.unity`에 격자 자산 참조를 추가했으며 씬 GUID와 공통 입력·탭 구조를 유지한다.

## 실행 검증

2026-10-11, Unity 6000.3.23f1에서 최종 EditMode **81개**, Windows Player 통합 검사 **224개**가 통과했다. 빌드와 Player 종료 코드는 0이다. `ExhibitionChecks.Run`과 `ReviewRegressionChecks.Run`도 종료 코드 0으로 완료했다. 기존 검증 도구와 동일한 실행 인자를 사용하고 `Process.WaitForExit()`로 개별 실행 결과를 회수했다.

추가 EditMode 검사는 늦은 응답 무시, 조회 실패 시 기존 장면 유지, 소유자 불일치 거부, 특수 스냅샷, 100회 이동의 점유·경계·불변 앵커, 조회와 보상 분리 및 중복 보상 0 반환을 검증한다. Player는 로딩 중 행동 잠금, 실제 스냅샷 객체 생성, 이동 후 앵커 보존, 모든 예외 시나리오, 지연 중 복귀 및 홈 배치 보존을 검사했다.

- 테스트 결과: `Logs/FriendsSnapshot/EditMode.xml`.
- 전시관·회귀·빌드 로그: `Logs/FriendsSnapshot/exhibition.log`, `regression.log`, `build.log`.
- 최종 Player 결과와 캡처: `Logs/Integration/Player-Snapshot-Final-20261011-171219/`.
- 종료 로그에서 미해제 ComputeBuffer, 비해제 D3D 리소스, Sprite fallback 정리 불가 경고 패턴은 발견하지 않았다.
- `git diff --check`와 새 C# 자산의 `.meta` 존재를 확인했다. Unity가 자동 갱신한 무관한 프로젝트 설정은 복원했다.

최종 기본 전시와 미지원 객체 캡처를 확인했다. 초기 캡처에서 겹치던 두 건물의 샘플 좌표를 조정해 이름과 레벨을 읽을 수 있게 했다. 임시 이동 중 앞뒤 객체가 가려지는 순서는 바닥 접점을 따른다. 실제 아트워크·다중 셀 건물·모바일 터치·배포 빌드와 운영 서버는 별도 검증 범위다.

| 검토 화면 | 로컬 캡처 |
| --- | --- |
| 로딩 | [Friends_Visit_Loading.png](../Logs/Integration/Player-Snapshot-Final-20261011-171219/Friends_Visit_Loading.png) |
| 스냅샷 재현 | [Friends_Visit.png](../Logs/Integration/Player-Snapshot-Final-20261011-171219/Friends_Visit.png) |
| 미지원 객체 | [Friends_Snapshot_Unknown.png](../Logs/Integration/Player-Snapshot-Final-20261011-171219/Friends_Snapshot_Unknown.png) |
| 빈 전시 | [Friends_Snapshot_Empty.png](../Logs/Integration/Player-Snapshot-Final-20261011-171219/Friends_Snapshot_Empty.png) |
| 조회 실패 | [Friends_Snapshot_Error.png](../Logs/Integration/Player-Snapshot-Final-20261011-171219/Friends_Snapshot_Error.png) |

로그와 캡처는 Git 제외 경로의 로컬 산출물이다. 최초 프론트엔드 검증은 [이전 검증 기록](friends-validation.md)에 별도로 남아 있다. 작업은 기존 `feat/friends-frontend` 브랜치에서 이어 진행했고, 작업 전 변경과 `AGENTS.md` 수정은 보존했다.
