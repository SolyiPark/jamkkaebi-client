# 작업대 서버 연결

2026-11-27 기록이다. 작업대가 모의 백엔드 대신 Spring 서버와 통신하는 구조와 검증 방법을 적는다. 서버 계약 기준은 jamkkaebi-server `feat/clarity-step-restore`(c3642b6)다. 이 브랜치는 이 기록 시점에 서버 `main`에 아직 합쳐지지 않았다(`main`은 `lastClearedPhase`·`resealAt`을 쓰는 옛 복구 모양).

## 구조

| 위치 | 역할 |
| --- | --- |
| `Scripts/UI/Common/JamkkaebiApiClient.cs`, `ServerApiResult.cs` | 공용 HTTP 클라이언트(Bearer·A004 재발급·회전 refresh). 친구 탭 폴더에서 옮겼다. 친구 코드 경로 인코딩은 `FriendsServerMapper.CodePath`로 옮겼다. |
| `Scripts/UI/Common/ServerConnection.cs` | 서버 주소·토큰을 찾아 클라이언트를 만든다. 주소가 없으면 null(모의 모드). |
| `Scripts/UI/Shell/SharedRecords.cs` | 셸이 클라이언트 하나와 작업대 백엔드를 만든다. 주소가 있으면 `ServerWorkbenchBackend`, 없으면 `MockWorkbenchBackend`. |
| `Scripts/UI/Workbench/Backend/WorkbenchServerMapper.cs` | 서버 JSON ↔ 응답 그릇·요청 본문 변환. HTTP는 모른다. |
| `Scripts/UI/Workbench/Backend/ServerWorkbenchBackend.cs` | `IWorkbenchBackend`의 HTTP 구현. |
| `Scripts/Gameplay/Minigame/Core/PurificationBoardParameters.cs` | 정화 시작 응답의 `board` → `MinigameBoardConfig`. `PlaySetup.Board`로 미니게임에 넘긴다. |

서버 주소는 `SharedRecords._serverBaseUrl`(Inspector, 비움이 기본) → 환경 변수 `JAMKKAEBI_SERVER_URL` 순으로 찾는다. 토큰은 환경 변수 `JAMKKAEBI_ACCESS_TOKEN`·`JAMKKAEBI_REFRESH_TOKEN`으로만 받는다(로그인 화면은 범위 밖). 에디터에서는 둘 다 없을 때 `Temp/ServerContract/workbench-session.json`(아래 검증 서버가 만드는 파일)을 읽는다. 이 경로는 `#if UNITY_EDITOR`라 빌드에는 들어가지 않는다.

## 지킨 계약

- **클라이언트 하나**: 친구 탭은 셸이 있으면 `SharedRecords.Current.Api`를 쓴다. 탭마다 클라이언트를 만들면 회전 refresh 토큰을 서로 소비해 두 번째 재발급이 A005로 실패한다. 친구 씬만 단독으로 열면 예전처럼 직접 만든다.
- **HTTP 코루틴 소유**: 작업대 요청은 꺼지지 않는 `SharedRecords`에서 돈다. 작업대는 `PauseWhileHidden`이라 탭 코루틴에서 돌리면 재발급이 중간에 끊길 수 있다. 늦은 응답은 작업대 화면의 수명·화면 세대 검사가 버린다(기존 방식 그대로).
- **재시도 없음·모의 전환 없음**: 변경 요청(상자 열기·정화 시작·결과 보고·이름 변경)은 한 번만 보내고 실패하면 서버 메시지로 알린다. 다시 보내기는 사용자가 고른다.
  - 상자: 결과를 못 받으면 요청 번호를 남기고 다음 누름에 같은 번호를 쓴다. 서버는 같은 번호면 다시 뽑지 않는다.
  - 결과 보고: 기존 재전송 화면이 같은 토큰·같은 결과로 보낸다. 서버는 이미 정산한 판이면 같은 결과를 돌려준다.
- **기록**: 실패 경고에는 오류 코드와 HTTP 상태만 남긴다. 결과 보고 경로의 세션 토큰은 `{sessionToken}`으로 지운다.
- **`OwnedRelic`·`CareCount`**: 서버 응답은 응답 그릇(`WorkbenchSpirit.CareCount`, `RelicDetail.CareCount`)으로만 바꾸고 `OwnedRelic`의 필드는 바꾸지 않았다.

## 변환 규칙

- 열거형: 서버 표기(`CONTINUE_RESTORE`)와 정확히 같을 때만 받는다. 숫자 문자열은 거부한다.
- 시각: 서버는 오프셋 없는 한국 시간(`LocalDateTime`, Asia/Seoul)을 보낸다. UTC로 바꿔 담는다(클라이언트 시계가 UTC).
- 유물 상세: 최초 정화 중이면 서버가 계산해 보낸 `clarity`도 버린다(정령 공개 전). 재봉인이면 선명도 변화 시각이 없다.
- 정산: 서버 결과에는 진입 경로·난이도가 없어 시작한 판에서 채운다. 실패(`RETRY`)는 서버가 페이즈를 주지 않아 이번 판 페이즈로 채운다. 결과 화면은 다음 계획의 페이즈·선명도만 쓰고, 다음 판은 상세를 다시 조회해 서버 `nextPlay`를 쓴다.
- 작업대 응답의 `buildings`, 슬롯·정령의 카드 필드(시대·별칭)는 아직 쓰지 않는다.

## 검증

| 검사 | 서버 필요 | 무엇을 보장하나 |
| --- | --- | --- |
| `WorkbenchServerMapperTests` | 아니오 | JSON 샘플 → 응답 그릇 변환 |
| `ServerWorkbenchBackendTests` | 아니오 | 경로·본문, 실패 시 재전송 없음, 기록에 토큰 없음(전송 경계 교체) |
| `WorkbenchServerContractTests` | **예** | 실제 HTTP로 상자·멱등 재전송·정화 시작·보고·이름 변경·관리 실패 |

`WorkbenchServerContractTests`는 세션 파일이 없으면 생략(Ignore)된다. 일반 EditMode 통과를 서버 검증으로 보고하지 않는다.

### 로컬 검증 서버 (macOS·Linux)

```bash
Tools/serve-workbench-contract.sh ~/path/to/jamkkaebi-server
```

- 서버 체크아웃에 `Tools/UnityWorkbenchHarnessTest.java`를 잠시 복사해 `gradlew test`로 H2 로컬 서버를 연다(실제 시계, 최소 플레이 시간 1초). 끝내면(Ctrl+C) 복사한 파일과 세션 파일을 지운다. 로그는 `Logs/ServerContract/Workbench-*/spring.log`.
- 사용자 셋: box(유물 없음)·spirit(선명한 5번 정령)은 HTTP 테스트용, play(최초 정화 중 3번 + 선명 2·흐려짐 8·재봉인 9번)는 에디터 Play용이다.
- HTTP 테스트는 box·spirit 사용자의 상태를 바꾸므로 서버를 새로 띄운 뒤 한 번 돌린다.
- Unity 에디터가 열려 있어도 된다. 서버가 뜬 뒤 Test Runner에서 `WorkbenchServerContractTests`를 돌리거나 `UIPrototype` 씬을 Play한다.
- Windows용 `Tools/VerifyServerContract.ps1`(친구 탭)의 하네스는 지금 서버에 없는 `AwakeningStage`·`clearFirstPhase`를 써서 현재 서버에서는 컴파일되지 않는다. 이번 작업에서 고치지 않았다.

### 2026-11-27 실행 결과

- Unity 밖(mono)에서 변환 테스트 40개 통과. `ServerWorkbenchBackendTests`는 `UnityWebRequest` 모듈 때문에 Unity 밖에서 돌릴 수 없어 Test Runner 확인이 필요하다.
- 로컬 검증 서버(c3642b6)에 같은 변환기로 직접 요청을 보내 확인: 작업대·상세 4종(진행 중·선명·흐려짐·재봉인), 상자 열기와 같은 번호 재전송(같은 유물, 한 번만 뽑음), 최초 정화 3페이즈(서버 보드로 클라이언트 판 생성 포함), 관리 플레이 실패·성공, 이름 변경·되돌리기, 없는 유물 오류. Unity 안의 HTTP 테스트·Play 확인과는 별개다.
- 서버 동작 메모: 최초 정화의 마지막 페이즈(오염 관리) 성공에도 `revealed`가 `NAMED`로 다시 온다(서버 주석은 "새로 도달한 단계"). 모의 백엔드도 같게 돌려주어 결과 화면이 정체 공개 문장을 한 번 더 보인다.
