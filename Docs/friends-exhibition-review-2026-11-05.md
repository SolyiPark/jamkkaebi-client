# 친구 전시관 방문 기능 검수 — 2026-11-05

검수 대상은 `feat/friend-exhibition-rebuilder` 브랜치의 `25fc7b1` 이후 미커밋 구현이다. 사용자 첨부 범위의 방문 흐름, 모델·파싱, 월드 표시·입력, 아트 연결, 자동 검사와 실제 로컬 HTTP 검증을 확인했다. **수정이 필요한 기능 결함 1건**을 발견했다. 구현 코드는 수정하지 않았다.

후속 상태: 아래 P2 결함은 같은 날 사용자 요청으로 수정했다. 새 회귀 사례 9개를 포함해 전체 EditMode 253개가 통과했고, HTTP 3개는 설정 부재로 생략됐다. 아래 발견 사항과 검수 실행 기록은 수정 전 상태이며, 수정 결과는 마지막 절에 기록한다.

## 발견 사항

### [P2] 손상된 이름 필드 한 개가 정상 배치를 포함한 전시 전체를 거부한다

- 위치: `Assets/Jamkkaebi/Scripts/UI/Friends/ServerExhibitionBackend.cs:80`.
- 조건: 도깨비 행의 `spiritName`이 객체 또는 배열이고 `realName`은 정상 문자열인 경우.
- 원인: 71행은 두 이름 중 하나가 문자열이면 통과시키지만, 80행은 타입을 검사하지 않고 `spiritName`을 먼저 문자열로 변환한다. `ArgumentException`은 84행의 `OverflowException` 처리 대상이 아니다.
- 결과: `ParseSnapshot` 밖으로 예외가 전파되고 `GetExhibition`은 전시 전체를 형식 오류로 처리한다. 다른 정상 행도 표시되지 않아 개별 손상 행만 제외한다는 계약을 지키지 못한다.

현재 Unity가 컴파일한 `Library/ScriptAssemblies/Jamkkaebi.UI.dll`의 `ParseSnapshot`을 직접 호출해 재현했다. 정상 행 한 개와 아래 행을 같은 `spirits` 배열에 넣고, 정상 7×7 격자·소유자·보상 필드를 제공하면 `System.ArgumentException: Can not convert Object to String.`이 발생한다.

```json
{
  "relicId": 8,
  "spiritName": {},
  "realName": "정상 대체 이름",
  "position": { "x": 2, "y": 2 },
  "awakeningStage": "AWAKENED"
}
```

이름을 필드 타입에 따라 선택하거나 해당 행을 경고와 함께 제외해야 한다. 기존 `SnapshotSkipsMalformedRowsWithoutDiscardingOtherObjects` 검사는 이 입력 조합을 다루지 않는다. 수정 후 정상 행 보존과 객체·배열 이름 필드의 처리 결과를 검사해야 한다.

## 검증 중 확인한 불안정

일반 통합 검증의 최초 Player 실행은 방문 조회가 아직 로딩 중일 때 `IntegratedVerification.cs:324`에서 `Scene.Objects`에 접근하여 `NullReferenceException`으로 중단됐다. `VerifyIntegration.ps1`도 종료 코드 1이었다. 해당 실행 캡처의 HUD는 로딩 중이며 요청 실패 문구는 없었다.

동일 빌드를 수정 없이 다시 실행하면 355개가 모두 통과했다. 후속 실제 HTTP 전체 실행도 통과했다. 현재 근거만으로 제품의 조회 실패라고 단정하지 않는다. 검사에는 `.45초` 고정 대기 후 장면에 접근하는 기존 취약성이 있다. 시간 경과 대신 제한 시간 안에 `Loading` 종료를 기다리고 `Scene` 존재를 검사하면 실패 원인을 더 정확히 구분할 수 있다. 이번 검수에서 이 검사 코드는 수정하지 않았다.

최초 실패 증거: [verification.txt](../Logs/Integration/Player-20261105-193143/verification.txt), [로딩 상태 캡처](../Logs/Integration/Player-20261105-193143/Friends_Visit.png).

## 이번에 실제 실행한 검사

환경: Unity 6000.3.23f1, Windows Development Player, D3D12. 일반 검증은 프로세스의 서버 URL·HTTP 계약 설정을 비웠다. 실제 HTTP 검증은 기존 별도 서버 체크아웃 `tmp/friend-visit-server-20261105`의 `be72ae8`과 Java 21.0.10을 사용했다.

| 검사 | 이번 결과 | 증거 |
| --- | --- | --- |
| 일반 EditMode | 244 통과, HTTP 3개 예정된 생략, 실패 0 | 최초 `VerifyIntegration.ps1` 실행. 공용 `Logs/Integration/EditMode.xml`은 후속 실행 시 덮어쓰므로 실행 시 확인한 값이다 |
| 일반 Windows 빌드 | 성공 | `Logs/Integration/build.log` |
| 일반 Player 최초 실행 | 방문 로딩 검사에서 NRE, 종료 1 | `Logs/Integration/Player-20261105-193143/verification.txt` |
| 동일 빌드 Player 재실행 | 355 통과, 종료 0 | `Logs/Integration/ReviewPlayer-20261105-193323/verification.txt` |
| `FriendsSetup.ValidateVisitStage` | 종료 0, 전시관·회귀 검사 포함 | `Logs/ReviewVisitStageValidation.log` |
| 실제 로컬 HTTP EditMode | 247 통과, 실패·생략 0 | `Logs/ServerContract/Run-20261105-193443/EditMode.xml` |
| 실제 HTTP Windows 빌드 | 성공 | 같은 실행의 `build.log` |
| 실제 HTTP Player | 376 통과, 종료 0 | 같은 실행의 `Player/verification.txt` |
| 실제 HTTP 전체 스크립트·서버 | 종료 0, `BUILD SUCCESSFUL` | 같은 실행의 `spring.log` |

재실행 일반 Player와 실제 HTTP 최종 Player 로그에서 GPU 종료 오류 3패턴 및 Exception은 각각 0개다. 실제 HTTP 보상·선물 성공 후 방문 장면·탭 잠금 유지 및 중복 버튼 비활성 검사를 포함한다. 서버 임시 실행기·세션·종료 파일은 정리됐고 서버 체크아웃은 깨끗하다.

정적 검수에서 새 Characters/Friends 자산의 `.meta` 누락 0개, Assets GUID 중복 0개와 참조 해소를 확인했다. A2/C2 PSB는 실제 바이너리이며 카탈로그 ID 2·8 연결을 확인했다. 7×7 좌표, 바닥 접점 정렬, RT 좌표 전달, 늦은 응답·차단 대상·재접속 세대 검사에서 추가 확정 결함은 찾지 못했다. 이번 HTTP 실행의 768×1024 방문 캡처도 직접 확인했다.

응답 주입 검사의 `onClick.Invoke()`는 상태 전이 검사다. 실제 레이캐스트·모달 입력 검증과 구분한다. Android/iOS·IL2CPP·운영 OAuth·배포 서버는 이번 검수에서 실행하지 않았다.

기존 소스·씬·아트와 무관한 설정·문서 변경은 보존했다. 일회성 씬 이관·재생성은 실행하지 않았고 커밋·푸시·PR은 하지 않았다. 이 보고서와 맥락 작업 기록만 추가했다.

## 후속 수정 — 2026-11-05

사용자 요청으로 `ServerExhibitionBackend.ReadRows`에서 `spiritName`의 타입이 문자열일 때만 해당 값을 선택하도록 수정했다. 그렇지 않으면 선행 필수 검사에서 확인한 문자열 `realName`을 사용한다. 두 이름 모두 사용할 수 없으면 기존 경고 분기로 해당 행만 제외한다. 정상 `spiritName` 우선순위와 건물 이름 파싱은 유지한다.

`ServerContractTests.SnapshotSelectsOnlyStringNamesAndPreservesOtherPlacements`에 9개 회귀 사례를 추가했다. 객체·배열·숫자·bool·null 이름과 정상 대체 이름, 사용할 이름이 없는 행 제외, 정상 이름 우선순위와 다른 도깨비·건물 보존을 검사한다.

- 수정 전 새 테스트 실행: 9개 중 4개 실패·5개 통과, Unity 종료 코드 2. 객체·배열은 변환 예외, 숫자·bool은 잘못된 이름 선택으로 실패했다.
- 수정 후 전체 EditMode: 256개 중 **253개 통과·실패 0·예정된 HTTP 3개 생략**, Unity 종료 코드 0. 새 9개 사례는 모두 통과했다.
- 증거: `Logs/NameParsingFix-20261105-211038/Red.xml`, `red.log`, `EditMode.xml`, `editmode.log`.
- 이번 변경은 파싱 규칙에 한정되어 Player·실제 HTTP 전체 검증은 다시 실행하지 않았다. 앞 절의 HTTP·Player 결과는 수정 전 검수 결과다. 고정 대기 기반 Player 검사의 불안정은 이번 수정 범위에 포함하지 않았다.
- `git diff --check` 통과. 기존 미커밋 작업과 씬·설정은 보존했으며 커밋·푸시·PR은 수행하지 않았다.

이후 사용자 화면 캡처 요청으로 수정된 코드를 실제 HTTP 전체 경로에서도 재검증했다. `Logs/ServerContract/Run-20261105-212036/`에서 EditMode **256개 통과·생략 0**, Player **376개 통과**, 전체 스크립트·빌드·Player 종료 코드 0, 최종 Player Exception·GPU 종료 3패턴 0을 확인했다. `Player/Server_Visit.png`는 로컬 검증 서버의 칠칸전시(도깨비 8번·미등록 건물 9번)를 현재 재구축기로 표시한 실제 Player 캡처다.
