# 친구 클라이언트와 서버 통신 검증

검증일: 2026년 10월 11일. 서버 기준은 [jamkkaebi-server](https://github.com/SolyiPark/jamkkaebi-server/tree/9006089109169b59842b3c4dd807554e53166254) main의 `9006089109169b59842b3c4dd807554e53166254`이다.

기존 친구 탭은 실제 서버에 통신하지 않는 모의 구현이었다. 이번 변경으로 HTTP·인증·응답 변환 계층을 추가하고 친구 탭의 조회와 변경 버튼을 연결했다. 인증 토큰을 주입한 클라이언트와 로컬 Spring 서버 사이의 통신을 검증했다. 배포 서버에 바로 접속해 사용할 수 있는 완성된 계정 클라이언트로 해석하면 안 된다. 배포 주소, 실제 소셜 로그인 왕복과 격자 계약이 아직 남아 있다.

## 2026-10-29 서버 변경 반영

서버 main [`57dc54c`](https://github.com/SolyiPark/jamkkaebi-server/tree/57dc54c) 기준으로 위 기록 이후의 변경을 확인했다. 아래 기록의 서버 링크는 당시 기준 커밋을 그대로 가리킨다.

| 서버 변경 | 클라이언트 영향 |
| --- | --- |
| [`8b050b9`](https://github.com/SolyiPark/jamkkaebi-server/commit/8b050b9) (10-12) 전시관 격자를 7×7로 변경. 설정이 없어도 기본값 7. | 20×20 불일치는 해소됐다. 클라이언트 변경 없음. 기존에 20×20으로 저장된 스냅샷이 남아 있다면 방문 화면은 여전히 크기 오류를 표시한다. |
| [`e3f5d10`](https://github.com/SolyiPark/jamkkaebi-server/commit/e3f5d10) (10-14) 유물 표기를 `artifact`에서 `relic`으로 변경. 응답의 `artifactId`가 `relicId`로 바뀜. | 친구 전시 응답(`ServerExhibitionBackend`)과 내 전시 응답(`ExhibitionLayoutPackage`)에서 `relicId`를 읽도록 수정했다. 오류 코드 값(`W001`, `W002`)은 그대로다. |
| [`1060d35`](https://github.com/SolyiPark/jamkkaebi-server/commit/1060d35) (10-14) 친구 전시 응답의 `grid`, `capturedAt`을 항상 포함. | 클라이언트는 두 경우를 모두 처리하므로 변경 없음. |

이번 반영은 서버 소스 비교로 확인했고 로컬 서버 HTTP 검증(`Tools/VerifyServerContract.ps1`)은 다시 실행하지 않았다.

## 전시관 방문에 대한 판단

친구의 전시관을 서버에서 한 번 조회해 별도의 방문 장면으로 재현하는 방식은 가능하다. 서버가 이미 `GET /api/friends/{friendCode}/exhibition`으로 주인 프로필, 공개 도깨비·건물의 ID와 좌표, 각성 단계·레벨, 스냅샷 시각과 방문·선물 상태를 제공한다. 클라이언트는 이 정보를 읽고 해당 ID의 표현물을 만들어 배치하면 된다. 실제 도깨비·건물 아트워크가 미정이어도 임시 표현물로 데이터 흐름을 먼저 검증할 수 있다. [응답 정의](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/friend/dto/response/FriendExhibitionResponse.java), [전시 객체 정의](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/friend/spi/ExhibitionView.java).

다만 서버 기본 격자는 20×20이고 우리 홈과 방문 화면은 7×7이다. 20×20의 좌표를 7×7로 축소하거나 범위 밖 객체를 가장자리로 옮기는 것은 같은 배치의 재현이 아니다. 현재 방문 화면은 크기가 맞지 않으면 오류를 표시한다. 서버 담당자와 `app.exhibition.grid-width`, `grid-height`를 7로 맞추고 기존 20×20 배치·스냅샷의 전환 정책을 정해야 한다. 신규 설정만 바꾸어도 기존 저장 스냅샷은 20×20으로 남을 수 있다. 서버 저장소는 이번 작업에서 변경하지 않았다. [서버 설정](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/resources/application.yml).

## 기존 가정에서 수정한 부분

| 항목 | 서버 기준과 이번 처리 |
| --- | --- |
| 실제 연결 여부 | 이전에는 HTTP와 인증이 없었다. 모의 테스트 통과는 서버 통신의 증거가 아니었다. 이번에는 UnityWebRequest로 실제 Spring HTTP 요청을 검증했다. |
| 스냅샷 저장 시점 | 마지막 접속뿐 아니라 주인의 배치·공개 상태 변경 때에도 갱신된다. 방문 중 실시간 동기화나 서버 NPC 이동 경로를 제공하는 API는 아니다. |
| 묶음 전송 | 서버의 `PUT /api/exhibition/layout`은 배치 전체를 `{items:[{type,id,x,y}]}`로 교체한다. 누락된 객체는 배치에서 빠지고 `items:[]`는 전부 회수한다. 친구·재화·보상을 임의의 패키지 한 개로 저장하는 API는 없다. 현재 홈 더미의 ID를 이 API에 보내지 않는다. |
| 객체 식별자 | 도깨비는 `relicId`, 건물은 `buildingId`다. 로컬 오브젝트의 실행 중 ID를 서버 ID로 간주하지 않는다. 실제 ID에 대응하는 아트워크는 모두 임시 모습으로 표시한다. |
| 친구 코드 | 서버는 8자리이며 `0`, `1`, `I`, `L`, `O`를 제외한다. 공백·하이픈을 제거하고 대문자로 정규화한다. 화면 입력 검증과 HTTP 경로를 맞췄다. |
| 응답 래퍼 | `success`, `code`, `message`, 선택적 `data`를 읽는다. 성공 여부를 HTTP 상태나 `data` 존재 하나만으로 판단하지 않는다. |
| 목록과 상세 | 친구·추천 목록의 프로필은 평탄화되어 있고 요청함·조회·스냅샷 주인은 `player` 또는 `owner` 안에 있다. 각각 변환한다. |
| 방문과 보상 | 전시 GET은 보상과 방문 기록을 만들지 않는다. 보상 버튼의 POST 성공 후 서버 목록을 다시 조회한다. 오늘 첫 보상 이후 다른 주인의 POST는 0을 반환하며 방문 기록만 추가할 수 있다. 현재 UI는 보상을 받은 뒤 버튼을 비활성화하므로 다른 친구를 단순 열람한 사실을 서버 방문 기록으로 만들지는 않는다. |
| 보상 수치 | 방문 5·선물 3은 서버의 현재 설정값이다. 서버 모드의 화면은 고정 지급액을 약속하지 않고 POST 응답의 `jeongseongGained`를 표시한다. 반복 성공에서 0을 받아도 추가 보상을 만들지 않는다. |
| 친구 요청 | 양쪽이 동시에 요청하거나 거절했던 수신자가 반대 방향으로 요청하면 즉시 친구가 될 수 있다. 요청 취소는 이미 성립한 친구 관계를 지우지 않는다. 성공 후 전체 목록을 다시 읽는다. |
| 선물 상태 | `AVAILABLE`, `SENT`, `DONE_TODAY`, `RECEIVER_FULL`을 서버 응답에서 받는다. 받은 선물은 임의의 누적 수량이 아니라 `giftTicketAvailable`로 표시한다. |
| 날짜 초기화 | 서버 모드에서는 PC 자정에 로컬 보상을 리셋하지 않는다. KST의 `nextResetAt`를 표시하고 새로고침으로 서버 상태를 갱신한다. |

근거: [방문 처리](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/friend/service/FriendVisitService.java), [스냅샷 갱신](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/friend/service/ExhibitionSnapshotService.java), [친구 코드](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/user/service/FriendCodeFormat.java), [배치 API](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/exhibition/controller/ExhibitionController.java).

## 연결된 요청

| 화면 동작 | 메서드와 경로 | 본문 |
| --- | --- | --- |
| 목록·새로고침 | `GET /api/friends` | 없음 |
| 받은·보낸 요청 | `GET /api/friend-requests?direction=RECEIVED` 또는 `SENT` | 없음 |
| 추천 | `GET /api/friends/recommendations` | 없음 |
| 코드 조회 | `GET /api/friends/lookup/{friendCode}` | 없음 |
| 요청 보내기 | `POST /api/friend-requests` | `{"friendCode":"대문자8자리"}` |
| 수락·거절 | `POST /api/friend-requests/{friendCode}/accept` 또는 `/reject` | 없음 |
| 요청 취소 | `DELETE /api/friend-requests/{friendCode}` | 없음 |
| 전시 조회 | `GET /api/friends/{friendCode}/exhibition` | 없음 |
| 방문 보상 | `POST /api/friends/{friendCode}/visit-reward` | 없음 |
| 선물 | `POST /api/friends/{friendCode}/gift` | 없음 |
| 차단 | `POST /api/blocks` | `{"friendCode":"대문자8자리"}` |
| 차단 목록·해제 | `GET /api/blocks`, `DELETE /api/blocks/{friendCode}` | 없음 |

요청·친구 해제 등의 경로 변수는 요청 ID나 내부 userId가 아니라 친구 코드다. 서버 통신 실패는 오류로 표시하며 더미 데이터로 자동 전환하지 않는다. 변경 요청을 네트워크 실패 때문에 자동으로 반복하지 않는다.

## 인증과 실행 설정

`JamkkaebiApiClient`는 Bearer 헤더와 A004 Access 만료 시 한 번의 재발급·원 요청 재실행을 처리한다. 재발급 성공 시 Access와 Refresh를 함께 교체하며 A005 실패 시 토큰을 폐기한다. A003 오류나 재발급할 수 없는 A004도 다시 로그인해야 한다. 토큰은 메모리에서만 보관하며 자산·PlayerPrefs·로그에 기록하지 않는다.

로그인 API의 `StartLogin`은 무작위 verifier를 만들고 `Base64(SHA-256(verifier))`를 보낸다. `Exchange`는 딥링크의 `code`를 요청 본문의 `handoff`로 보내고 verifier 원문과 교환한다. 이 API들을 검증했지만 시스템 브라우저 → Google/Kakao → 앱 딥링크 복귀 전체를 구현·검증한 것은 아니다. 신규 가입자의 닉네임 입력, 기기별 딥링크 등록과 안전한 세션 보관도 계정 화면 작업으로 남아 있다. [인증 계약](https://github.com/SolyiPark/jamkkaebi-server/blob/9006089109169b59842b3c4dd807554e53166254/src/main/java/com/example/jamkkaebi/auth/controller/AuthController.java).

기본 주소를 지정하지 않으면 기존 모의 모드를 유지하고 제목에 ‘모의’를 표시한다. 실제 서버 모드는 `FriendsPresenter.ConnectServer(authenticatedClient)`로 전환한다. 로그인 UI가 준비되기 전 데스크톱 검토에는 프로세스 환경 변수 `JAMKKAEBI_SERVER_URL`, `JAMKKAEBI_ACCESS_TOKEN`, 선택적 `JAMKKAEBI_REFRESH_TOKEN`을 사용할 수 있다. Unity가 시작되기 전에 설정해야 하며 값을 저장소에 추가하거나 채팅에 보낼 필요는 없다. `_serverBaseUrl`에는 주소만 Inspector로 지정할 수 있다. 서버 모드 제목은 ‘서버’이고 새로고침 버튼을 제공한다.

## 검증 결과

서버의 기존 테스트 201개가 H2 환경에서 통과했다. 서버 소스 자체는 변경하지 않고 별도 체크아웃에 테스트용 HTTP 서버 실행기를 추가했다. 실행기는 실제 Spring Controller·인증 필터·서비스·저장소·JSON 직렬화를 사용한다. 계정은 가짜 Google 식별자로 생성하고 전시 객체 공급은 서버의 `FakeExhibitionViewReader`로 구성했다. 실제 OAuth 제공자, 운영 MySQL과 실제 유물 획득·배치 과정의 종단 검증을 대신하지 않는다.

Unity EditMode 테스트 90개가 통과했다. 이 중 실제 HTTP 검증은 만료 Access 재발급, 회전 Refresh 재사용 거부, 로그인 시작과 handoff 교환, 인증 오류, 친구·요청·추천·차단 조회, 요청 송신·교차 수락·수락·거절·취소, 스냅샷 역직렬화, 7×7 재현, 20×20 거부, GET의 보상·방문 무변경, 반복 보상과 선물의 0 반환, 차단·해제와 로그아웃을 검사한다. 로컬 서버를 지정하지 않는 일반 Test Runner에서는 HTTP 테스트 1개를 명시적으로 생략한다.

`Tools/VerifyServerContract.ps1`의 전체 실행이 통과했다. Windows Development Player의 자동 검사 238개, 실제 서버 목록·전시 표시·요청·취소·격자 오류·인증 실패 화면 및 GPU 종료 로그 검사를 통과했다. `ExhibitionChecks.Run`과 `ReviewRegressionChecks.Run`도 각각 통과했다. 실제 Android/iOS 기기·OAuth 제공자·배포 서버의 검증은 포함하지 않는다.

이번 결과는 `Logs/ServerContract/Run-20261011-175526/`의 `EditMode.xml`, `build.log`, `spring.log`, `Player/verification.txt`, `Player/player.log`에 있다. 전시관·회귀 검사 로그는 `Logs/ServerContract/exhibition.log`, `regression.log`다. 화면 검토에는 `Player/Server_Friends.png`, `Server_Visit.png`, `Server_GridMismatch.png`, `Server_AuthFailure.png`를 사용한다. 이 로그와 빌드는 커밋 대상이 아니다.

```powershell
.\Tools\VerifyServerContract.ps1 `
  -ServerPath '<별도 jamkkaebi-server 체크아웃>' `
  -EditorPath 'D:\UnityEditors\6000.3.23f1\Editor\Unity.exe' `
  -JavaHome 'C:\Program Files\Android\Android Studio\jbr'
```

Java 21과 서버 Gradle 의존성 다운로드가 필요하다. 이 스크립트는 외부 서버의 기존 테스트 파일·검증 세션 파일을 덮어쓰지 않는다. H2용 임시 실행기를 넣어 서버를 시작하고 Unity 테스트·Player 빌드·자동 화면 검증·GPU 종료 로그 검사를 실행한다. 종료 시 임시 실행기와 토큰 파일을 삭제한다. 테스트 handoff 수명은 준비 시간을 허용하도록 10분이며 운영 기본값 60초와 다르다. 포트는 실행마다 달라지고 서버를 종료하면 테스트 계정은 사라진다.

## 배포 연결에 필요한 자료

1. 실제 배포 서버의 기본 주소와 접속 가능한 테스트 환경. 소스 저장소 URL은 API 서버 주소가 아니다.
2. Google/Kakao 클라이언트와 redirect URI, 앱 복귀용 딥링크에 관한 서버 담당자 설정. 비밀 키는 서버 환경에서 관리한다.
3. ~~7×7 격자 설정~~ (2026-10-12 서버 반영 완료) 및 기존 20×20 데이터 처리 방침. 현재 사용자 확정 격자를 클라이언트가 임의로 바꾸지 않았다.
4. 서버 `relicId`(구 `artifactId`)·`buildingId`와 클라이언트 아트워크·기본 이름의 대응표. 완성된 NPC AI는 통신 검증의 선행 조건이 아니다.

친구 탭 외의 계정 화면·홈 하우징 저장·도감·발굴 보상은 이번에 서버와 연결하지 않았다. 방문 중 도깨비 움직임은 클라이언트의 연출이며 저장된 스냅샷 좌표나 내 홈을 수정하지 않는다.

후속 구현에서 홈 배치 송신 객체와 wrapper를 추가했다. 자동 저장은 연결하지 않았으며 API 호출 기반을 구현했다. 실제 전시 도메인의 저장 → 친구 공개 스냅샷 갱신과 기능 간 검수, EditMode 103개·Player 242개 최종 실행 결과는 [friends-interaction-review.md](friends-interaction-review.md)를 참고한다. 이전 90개·238개 결과는 당시 실행 기록이다.
