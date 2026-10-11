# NPC·건물 송신 객체와 배치 wrapper

작성일: 2026-10-11. 서버 기준: SolyiPark/jamkkaebi-server 커밋 `9006089109169b59842b3c4dd807554e53166254`.

## 구현된 객체 계약

| 객체 | 현재 프로젝트에서 읽는 값 | 서버 식별자 | 저장 대상 |
| --- | --- | --- | --- |
| `ExhibitionNpcData` | 홈 도깨비의 고정 `PlacementCell` | 보유한 완성 유물의 `artifactId` | `SPIRIT`, ID, 배치 여부, 논리 좌표 |
| `ExhibitionBuildingData` | `HousingPlacement`의 ItemId·로컬 배치 번호·Cell·BaseSize·QuarterTurns | 보유 건물의 `buildingId` | `BUILDING`, ID, 배치 여부, 논리 좌표 |

공통 `ExhibitionObjectData`는 불변 데이터이다. `LocalId`는 클라이언트의 디버깅·식별용이며 송신 JSON에는 들어가지 않는다. 서버 ID는 로컬 배치 번호나 이름으로 추정하지 않는다. 실제 보유 목록과 로컬 정의 사이의 명시적인 바인딩이 필요하다. NPC 이름·각성·선명도, 건물 이름·레벨은 서버 소유 상태로, 배치 요청에서 변경하지 않는다.

NPC의 `PlacementCell`은 `Configure` 때 지정한 기준점이다. `CurrentCell`, `TargetCell`, 월드 좌표와 매 프레임 움직임은 저장하지 않는다. 이후 NPC 배치 편집을 추가할 때는 기준점 변경과 이동 경로 재설정을 함께 구현해야 한다.

현재의 ItemId 단위 건물 바인딩은 같은 건물 정의를 한 번만 배치할 수 있는 서버 계약에 맞춘다. 로컬은 같은 종류를 여러 번 배치할 수 있으므로, 동일 서버 ID의 다중 배치는 wrapper에서 거부한다.

## wrapper의 처리

1. `ExhibitionLayoutCapture.Capture`가 홈의 NPC·건물 정보를 복사해 `ExhibitionLayoutDraft`로 모은다. 바인딩 목록은 회수 후에도 유지하여 회수 객체를 `Placed=false`로 포함한다.
2. `ExhibitionLayoutWrapper.Save`가 `GET /api/exhibition`으로 최신 보유 목록·배치·격자를 읽는다.
3. `ExhibitionLayoutPackage.Build`가 관리 대상의 새 배치와 관리하지 않는 서버 배치를 합친다. 숨긴 NPC도 기존 배치를 보존한다. `Placed=false`인 관리 대상만 요청 목록에서 제외한다.
4. 최종 JSON을 **한 번의 `PUT /api/exhibition/layout`**으로 보낸다. 편집 동작별로 개별 요청하지 않는다. 저장당 GET 1회 + PUT 1회이며, 인증 만료 시 공통 API 클라이언트의 토큰 갱신 요청이 추가될 수 있다.
5. 서버 성공·실패를 콜백으로 전달한다. `LastPackage`로 실제 송신 패키지를 검토할 수 있다. 요청 중 중복 Save는 `SAVE_BUSY`, 로컬 계약 위반은 `LAYOUT_REJECTED`로 반환한다.

```json
{"items":[{"type":"SPIRIT","id":2,"x":1,"y":1},{"type":"BUILDING","id":1,"x":2,"y":2}]}
```

`items`는 전체 배치 교체 목록이다. 좌표는 0부터 시작한다. 회수 상태는 별도 `placed` 필드를 보내는 대신 해당 객체를 목록에서 제외해 표현한다. 빈 목록은 모든 관리 대상이 회수되고 보존할 서버 객체도 없을 때만 만들어진다.

서버 ID 누락·미보유 ID·중복 ID·격자 불일치·범위 초과·객체 간 위치 충돌은 PUT 전에 거부한다. 서버가 회전과 footprint 필드를 제공하지 않으므로 배치된 객체가 1×1이 아니거나 회전 값이 0이 아닌 경우도 거부한다. 잘못된 GET 응답을 빈 목록으로 해석하지 않는다.

## 호출 방법

인증된 공통 API 클라이언트와 보유 ID 바인딩을 사용하는 호출 예시이다. 숫자 1·2는 검증용 계정의 실제 보유 ID이며, 일반 사용자의 ID를 하드코딩하면 안 된다.

```csharp
var bindings = new Dictionary<string, int> { [housing.DefaultItem.ItemId] = ownedBuildingId };
var wrapper = new ExhibitionLayoutWrapper(authenticatedApiClient);
var draft = ExhibitionLayoutCapture.Capture(housing, bindings, ownedArtifactId);
yield return wrapper.Save(draft, result => HandleSaveResult(result));
```

저장할 때마다 홈을 다시 캡처한다. 전송 중 발생한 추가 편집은 이전 draft에 포함되지 않으므로 다음 저장에서 다시 캡처해야 한다. wrapper는 저장 성공을 근거로 현재 홈 전체를 저장 완료 처리하거나 자동으로 홈 상태를 덮어쓰지 않는다. MonoBehaviour 호출자는 코루틴 수명을 관리해야 한다.

## 적용 범위와 남은 연결

- 실제 HTTP 송신 계층과 현재 홈을 읽는 어댑터를 구현했다. 홈에 자동 저장·저장 버튼·로그인 연결을 추가하지는 않았다. 현재 더미 정의에 서버 ID를 임의로 지정하지 않았다.
- 현재 홈은 7×7, 서버 기본 설정은 20×20이다. 이 상태의 홈 저장은 거부된다. 7×7로 서버 계약을 확정하거나 클라이언트 격자 설계를 변경하는 합의가 먼저 필요하다. 이번 구현에서는 어느 쪽도 임의로 변경하지 않았다.
- 보유 건물·완성 유물을 조회해 로컬 자산과 매핑하는 인벤토리/카탈로그, 계정 세션 연결, 자신의 서버 배치를 홈에 복원하는 과정은 후속 작업이다. 친구 방문 수신 기능과는 별개이다.
- 같은 객체의 숨김 변경은 `PATCH /api/exhibition/spirits/{artifactId}/visibility`의 별도 기능이다. wrapper는 숨김 상태를 수정하지 않는다.
- GET과 PUT 사이 다른 기기에서 발생한 편집을 감지하는 버전/ETag 계약은 서버에 없다. 관리하지 않는 배치를 보존하더라도 동시 편집의 손실을 완전히 방지할 수 없다. 자동 재시도는 하지 않는다.
- 서버 배치 PUT는 새 NPC·건물을 생성하거나 획득시키지 않는다. 이미 보유한 객체를 배치·이동·회수한다.

## 구현 위치

| 기능 | 파일 |
| --- | --- |
| 공통·NPC·건물 데이터 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionObjectData.cs`, `ExhibitionNpcData.cs`, `ExhibitionBuildingData.cs` |
| 홈 데이터 캡처·불변 draft | `Assets/MobileUI/Scripts/Exhibition/ExhibitionLayoutCapture.cs`, `ExhibitionLayoutDraft.cs` |
| 서버 보유 목록 검증·전체 교체 패키지 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionLayoutPackage.cs` |
| 배치 통합 송신 | `Assets/MobileUI/Scripts/Exhibition/ExhibitionLayoutWrapper.cs` |
| NPC 고정 배치 좌표 | `Assets/MobileUI/Scripts/Exhibition/DokkaebiWanderer.cs` |
| 단위·실제 HTTP 검증 | `Assets/MobileUI/Tests/EditMode/ExhibitionLayoutTransmissionTests.cs` |
| 로컬 서버 보유 객체 준비 | `Tools/UnityContractHarnessTest.java` |

## 이번 검증

Unity 6000.3.23f1에서 `Tools/VerifyServerContract.ps1`을 실행했다. 최종 실행 폴더는 `Logs/ServerContract/Run-20261011-182041/`이다.

- EditMode 101개 통과, 실패·생략 0개. 신규 송신 계약 검사는 11개이며 실제 Spring HTTP 테스트도 실행했다.
- 로컬 H2 검증 계정에 도메인 메서드로 완성 유물 2·8과 건물 1을 지급했다. 기본 20×20 설정의 실제 전시관 GET/PUT로 NPC·건물 일괄 저장, 재조회, NPC 회수, 건물 이동, 관리하지 않는 유물 8의 배치 보존을 확인했다. 이 검증용 ID는 앱 자산에 저장하지 않는다.
- 같은 서버에 7×7 draft를 저장하면 `LAYOUT_REJECTED`가 반환되고 기존 저장 좌표가 유지됨을 재조회했다. 이름·레벨 등 서버 상태를 수정하는 요청은 보내지 않았다.
- Windows Development Player 빌드·자동 검증 241개 및 GPU 종료 로그 검사 통과. 실제 홈 캡처와 NPC 이동 중 고정 배치 좌표 유지도 검사했다.
- `ExhibitionChecks.Run`, `ReviewRegressionChecks.Run`, `git diff --check` 통과. 새 코드의 Unity `.meta`를 포함했다.
- 첫 Player 실행은 기존 스와이프의 `swipe left selects tab 1` 검사에서 실패했다. 최종 재실행에서는 이 검사와 전체 검증이 통과했다. 첫 실행의 EditMode 101개는 모두 통과했다. 최초 로그는 `Run-20261011-181850/`에 남아 있다.
- 모바일 실기기·배포 서버·실제 OAuth 로그인·다중 기기 동시 편집은 검증하지 않았다. 친구 공개 스냅샷 공급자는 기존 harness의 fake provider를 사용하므로, 이번 검증은 저장 후 친구 스냅샷 내용의 종단 간 갱신을 증명하지 않는다. 자신의 보유 객체와 저장·조회는 실제 서버 도메인/API를 사용했다.

후속 상호작용 검수에서는 송신 계정에 실제 `ExhibitionViewProvider`를 사용하도록 harness를 확장했다. 저장·이동·회수·숨김이 친구 공개 스냅샷에 반영되는 것을 실제 HTTP로 확인했다. 최종 EditMode 103개·Player 242개 결과와 수정 내역은 [friends-interaction-review.md](friends-interaction-review.md)에 기록했다.
