# 도감 구현과 작업대 연결 계약

이 문서는 현재 도감 화면과 향후 작업대 연결 계층의 경계를 기록한다. 작업대 연동, 유물 인스턴스 인벤토리, 복원 페이즈 진행, 저장·서버 기능은 아직 구현하지 않았다. 이번 도감은 샘플 기록으로 화면을 실행한다.

## 현재 구조

- `Assets/MobileUI/Scenes/Collection.unity`: 기존 도감 탭 씬. `MirrorSceneRoot` 아래에서 시대 목록 → 시대 상세 → 유물 상세를 전환한다.
- `Assets/MobileUI/Configuration/CollectionCatalog.asset`: 도감의 고정 정의 자산. `CollectionPresenter`와 공유 진행 기록은 **같은 자산 인스턴스**를 사용해야 한다.
- `Assets/MobileUI/Configuration/CollectionAppearance.asset`: 도감 공통 배경·카드·아이콘·장식의 선택적 스프라이트를 담는 `CollectionAppearance` 자산. 도감 씬 `CollectionPresenter`의 `_appearance`에 연결한다.
- `Assets/Jamkkaebi/Scripts/Gameplay/Collection/`: 고정 정의와 수집 규칙. UI, 작업대 어댑터, 전시관 배치 타입에 의존하지 않는다.
- `Assets/MobileUI/Scripts/Collection/CollectionPresenter.cs`: 도감 표시, 화면 이동, 신규 확인과 변경 이벤트 구독.
- `Assets/MobileUI/Scripts/Collection/CollectionCatalogData.cs`: 확정 시트의 시대·유물 명칭과 순서를 바탕으로 고정 카탈로그를 생성한다. 설명·복원 이야기와 실제 아트는 임시 콘텐츠다.
- `Assets/MobileUI/Scripts/Collection/CollectionDemoData.cs`: 화면 검토를 위한 초기 수집 기록만 주입한다. 고정 카탈로그 정의와 더미 사용자 기록을 분리한다.

현재 `CollectionPresenter.Awake()`는 `CollectionDemoData.CreateProgress(_catalog)`를 호출한다. 시작 상태는 유물 5/9종 복원 완료, 고려 건물 1개 해금이다. 삼국 금관·고려 3종·조선 옥새가 복원된 상태다. 금관·고려청자·옥새는 신규 상태이며 석불·나전칠기는 확인한 상태다. 재실행하면 이 샘플 상태로 초기화된다. 기록은 플레이 세션 메모리에만 존재한다.

## 확정 목록의 원본과 적용 범위

시대와 유물 명칭의 원본은 [확정 Google Sheet의 시트1](https://docs.google.com/spreadsheets/d/1h2kPX9OrotI1mofpJxDIXg06z8RpFlT2e5LXNudL_rw/edit?gid=0)이다. `시트1!A2:C10`의 9개 항목을 원본 순서 그대로 사용한다. 시대 표시명은 **삼국·고려·조선**이고 각 시대의 유물은 아래 표에 나오는 순서다. `CollectionCatalogData.CreateCatalog()`가 이 목록을 생성하며, 런타임 화면은 저장된 `CollectionCatalog.asset`을 사용한다.

같은 시트의 `D:I`는 아트 방향 후보다. 물음표·`X` 등 후보 상태를 포함하므로 해당 특성을 확정 정령 이름, 확정 도감 설명, 복원·보존 이야기로 해석하지 않는다. 정령 이름을 새로 정하지 않았으며 현재 표시는 `정령 이름 미정`이다. 유물 설명과 복원 이야기는 임시 초안임을 화면에 표시하고, 실제 자료와 아트가 준비되면 별도로 교체한다.

## 데이터 자산 계약

`CollectionCatalog`는 `ScriptableObject`다. `Configure(eras, relics)`는 시대·유물 ID의 중복, 비어 있는 ID, 유물의 존재하지 않는 시대 참조를 거절한다. `Validate()`는 저장 자산을 Inspector에서 편집하거나 다시 불러온 경우에도 같은 조건을 검사하며, `CollectionProgress` 생성자가 이를 호출한다. 정의 자산을 모두 구성한 다음 `CollectionProgress`를 생성하고, 사용 중인 카탈로그를 재구성하지 않는다. `Eras`와 `Relics`는 읽기 전용 목록이고 `FindEra(id)`, `FindRelic(id)`는 알려지지 않은 ID에 `null`을 반환한다.

| 정의 | 저장 필드와 읽기 속성 |
| --- | --- |
| `CollectionEraDefinition` | ID, 이름, 표시용 시대 문구, 설명, 해금 건물 이름, 강조색, 선택적 `OverviewIllustration`, `ContextIllustration`, `BuildingIllustration`, `LockedBuildingIllustration`, `BackgroundIllustration` |
| `CollectionRelicDefinition` | ID, 시대 ID, 유물 이름, 정령 이름, 유물 설명, 복원·보존 이야기 초안, 강조색, 선택적 `Illustration`, `SpiritIllustration`, `Thumbnail`, `LockedIllustration`, `BackgroundIllustration` |

고정 ID와 표시명은 아래와 같다. 표의 위에서 아래 순서는 원본 `A2:C10`과 같다. 이후 그림·설명 교체를 위해 ID를 변경하지 않는다.

| 원본 행 | 시대 표시명 / ID | 유물 표시명 | 유물 ID |
| --- | --- | --- | --- |
| 2 | 삼국 / `samguk` | 금동여래입상 | `samguk-standing-buddha` |
| 3 | 삼국 / `samguk` | 금동대향로 | `samguk-incense-burner` |
| 4 | 삼국 / `samguk` | 금관 | `samguk-crown` |
| 5 | 고려 / `goryeo` | 석불 | `goryeo-stone-buddha` |
| 6 | 고려 / `goryeo` | 고려청자 | `goryeo-celadon` |
| 7 | 고려 / `goryeo` | 나전칠기 | `goryeo-lacquerware` |
| 8 | 조선 / `joseon` | 옥새 | `joseon-royal-seal` |
| 9 | 조선 / `joseon` | 마패 | `joseon-horse-medallion` |
| 10 | 조선 / `joseon` | 묘작도 | `joseon-cat-sparrow-painting` |

초기 샘플의 `samguk-pottery`, `goryeo-bronze-mirror`, `goryeo-bell`, `joseon-white-porcelain`, `joseon-brush`, `joseon-sundial`은 폐기했다. 영속 저장이 아직 연결되지 않았으므로 이전 ID에서 새 ID로의 별칭이나 저장 마이그레이션은 만들지 않는다. 향후 작업대는 위 확정 목록의 종류 ID를 사용한다.

실제 문화유산 설명과 발굴·복원·보존 이야기를 제공하려면 현재 가상 초안을 출처가 확인된 콘텐츠로 별도 교체한다. 그림 교체는 아래 자산 설정으로 처리하며, 유물 종류 ID와 수집 기록을 변경하지 않는다.

Unity 자산과 스크립트의 `.meta`를 함께 유지하고, 자산 이동·이름 변경 시 GUID를 보존한다. 카탈로그는 현재 프로젝트 로컬 자산이며 서버 데이터 스키마나 저장 파일 형식을 정의한 것은 아니다.

## 도감 이미지를 교체하는 방법

1. 준비한 그림을 프로젝트의 `Assets/` 아래에 추가하고 Inspector의 **Texture Type**을 **Sprite (2D and UI)**로 설정해 적용한다. 여러 그림이 있는 시트는 Sprite Editor에서 분할한 스프라이트를 사용한다. `.meta`도 함께 관리한다.
2. `CollectionCatalog.asset`을 선택하고 해당 시대·유물 항목에 아래 표의 스프라이트를 할당한다. 상세와 카드에 같은 유물 그림을 쓰려면 `Illustration`만 할당한다. 카드의 별도 구도나 크롭이 필요할 때 `Thumbnail`을 추가한다.
3. 공통 UI 모양은 `CollectionAppearance.asset`의 슬롯에 할당한다. 이 자산은 도감 씬의 `CollectionPresenter._appearance`에 연결되어 있다. 배경·카드·버튼·장식 그림은 각 공통 슬롯에서 변경한다.
4. Play를 실행해 시대 목록, 복원·미복원 항목, 건물 잠금·해금, 유물 상세를 확인한다. 가로세로 비율에 따른 스크롤과 버튼 입력도 확인한다. 이미지 교체에 코드 편집이나 `CollectionSetup.Apply()` 실행은 필요하지 않다.

모든 이미지 슬롯은 선택 사항이다. 비어 있는 슬롯은 다음 순서로 대체 그림을 찾고, 끝까지 비어 있으면 기존 벡터 그림 또는 단색 UI를 사용한다. 일부 슬롯만 교체해도 나머지 화면은 계속 표시된다.

| 표시 위치 | 스프라이트 선택 순서 |
| --- | --- |
| 시대 목록 카드의 시대 그림 | 시대 `OverviewIllustration` → 기존 시대 벡터 그림 |
| 시대 상세의 시대 설명 그림 | 시대 `ContextIllustration` → 시대 `OverviewIllustration` → 기존 시대 벡터 그림 |
| 해금된 건물 그림 | 시대 `BuildingIllustration` → 기존 건물 벡터 그림 |
| 잠긴 건물 그림 | 시대 `LockedBuildingIllustration` → 공통 `LockIllustration` → 기존 잠금 벡터 그림 |
| 복원한 유물 카드의 그림 | 유물 `Thumbnail` → 유물 `Illustration` → 기존 유물 벡터 그림 |
| 미복원 유물 카드의 그림 | 유물 `LockedIllustration` → 공통 `LockIllustration` → 기존 잠금 벡터 그림 |
| 유물 상세의 유물 그림 | 유물 `Illustration` → 기존 유물 벡터 그림 |
| 유물 상세의 정령 그림 | 유물 `SpiritIllustration` → 기존 정령 벡터 그림 |
| 시대 목록의 전체 배경 | 공통 `PageBackground` → 기존 종이색 배경 |
| 시대 상세의 전체 배경 | 시대 `BackgroundIllustration` → 공통 `PageBackground` → 기존 종이색 배경 |
| 유물 상세의 전체 배경 | 유물 `BackgroundIllustration` → 소속 시대 `BackgroundIllustration` → 공통 `PageBackground` → 기존 종이색 배경 |

공통 외형 슬롯은 `CollectionAppearance`에 있다. 같은 종류의 UI에 공통 그림을 적용하고, 시대·유물별 주요 그림과 전체 배경은 위 카탈로그의 슬롯으로 구분한다.

| 공통 슬롯 | 적용 위치 / 비어 있을 때 |
| --- | --- |
| `PageBackground` | 전체 페이지 배경 / 기존 종이색 배경 |
| `EraCardBackground` | 시대 목록 카드 / 기존 시대 강조색을 섞은 카드 |
| `RestoredRelicCardBackground` | 복원한 유물 카드 / 기존 시대 강조색을 섞은 카드 |
| `LockedRelicCardBackground` | 미복원 유물 카드 / 기존 잠금 카드색 |
| `BuildingCardBackground` | 시대 상세의 건물 카드 / 기존 카드색 |
| `RelicPlateBackground` | 유물 상세의 큰 그림 패널 / 기존 패널색 |
| `SpiritPlateBackground` | 유물 상세의 정령 그림 패널 / 기존 종이색 패널 |
| `NewBadgeBackground` | 신규 배지의 바탕 / 기존 배지색 |
| `NewBadgeIcon` | 신규 배지 안의 그림 / `N` 텍스트 |
| `BackButtonBackground` | 뒤로가기 버튼의 바탕 / 기존 버튼색 |
| `BackIcon` | 뒤로가기 버튼 안의 그림 / `‹` 텍스트 |
| `CompletionIcon` | 수집을 마친 시대 카드의 완료 그림 / `✓` 텍스트 |
| `LockIllustration` | 항목별 잠금 그림이 없는 유물·건물 / 기존 잠금 벡터 그림 |
| `ProgressTrack`, `ProgressFill` | 수집률 막대의 바탕·채움 / 기존 막대색 |
| `Divider`, `AccentStrip` | 구분선과 시대 카드 강조띠 / 기존 단색 장식 |

할당한 스프라이트는 `Image.color = Color.white`로 표시하므로 원본 색을 유지한다. UI 바탕 그림은 프레임을 채우도록 늘어나며, Sprite Editor의 **Border**가 설정된 스프라이트는 9-slice로 테두리를 보존한다. 장식 그림의 Raycast Target은 꺼 두고, 카드와 뒤로가기 버튼의 입력은 버튼의 클릭 영역으로 받는다. 그림이 없는 잠금 항목에는 복원한 유물의 `Illustration`이나 `Thumbnail`을 대신 표시하지 않는다.

런타임에 공통 설정을 교체하려면 `CollectionPresenter.BindAppearance(appearance)`를 호출한다. 현재 페이지와 스크롤 위치를 유지하며 즉시 그림을 갱신하고, `null`을 전달하면 공통 슬롯의 기본 표현으로 돌아간다. 유물·시대별 그림은 그대로 사용한다. 수집 종류와 복원·신규 상태를 변경하지 않는다.

위 슬롯은 도감 콘텐츠 씬 안의 그림을 대상으로 한다. 화면 위의 프로필 바와 화면 아래의 탭 버튼은 공통 셸 소유이므로 `CollectionAppearance`로 교체하지 않으며, 해당 공통 셸 자산에서 별도로 설정한다.

## 확정 목록을 저장 자산에 반영하는 방법

Unity 메뉴 **`Prototype > Collection > Sync Catalog From Confirmed Data`** 또는 batch mode의 **`CollectionSetup.SyncCatalog`**로 `CollectionCatalogData.CreateCatalog()`의 정의를 `Assets/MobileUI/Configuration/CollectionCatalog.asset`에 반영한다. 이 명령은 도감 씬을 이관하거나 재구성하지 않는다. `CollectionSetup.Apply()`는 초기 씬 이관 전용이므로 목록 갱신에 재실행하지 않는다.

기존 카탈로그가 있으면 같은 자산을 갱신해 `.meta` GUID와 씬의 카탈로그 참조를 유지한다. 같은 시대 ID는 기존 `OverviewIllustration`, `ContextIllustration`, `BuildingIllustration`, `LockedBuildingIllustration`, `BackgroundIllustration` 참조를 보존한다. 같은 유물 ID는 기존 `Illustration`, `SpiritIllustration`, `Thumbnail`, `LockedIllustration`, `BackgroundIllustration` 참조를 보존한다. 공통 `CollectionAppearance.asset`은 카탈로그 동기화 대상이 아니다. 시대·유물 순서와 이름, 정령 이름 미정 표시, 임시 설명·복원 이야기 등 나머지 정의는 `CollectionCatalogData`의 값으로 교체한다. 삭제된 유물 ID의 아트를 새 ID로 옮기지는 않는다.

읽기 모델 사용 중에는 카탈로그를 재구성하지 않는 계약에 따라 Play를 종료한 상태에서 실행한다. batch mode 예시는 다음과 같다. Unity 경로는 실제 설치 위치에 맞춘다.

```powershell
$unityEditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$collectionProjectPath = 'D:\프로젝트\c3\Capstone_working'
$collectionLogFolder = Join-Path $collectionProjectPath 'Logs\Collection'
New-Item -ItemType Directory -Path $collectionLogFolder -Force | Out-Null
$collectionSyncLog = Join-Path $collectionLogFolder 'catalog-sync.log'
& $unityEditorPath -batchmode -quit -projectPath $collectionProjectPath `
    -executeMethod CollectionSetup.SyncCatalog -logFile $collectionSyncLog
```

완료 로그의 표시는 `COLLECTION_CONFIRMED_CATALOG_SYNCED`다. 이 목록 동기화 로그만으로 도감 화면이나 전체 통합 검증의 성공을 판단하지 않는다.

## 수집 기록 API

`CollectionProgress`는 `CollectionCatalog`를 생성자로 받는 C# 모델이다. `Catalog`는 구성에 사용한 자산 인스턴스를 반환한다.

| API | 계약 |
| --- | --- |
| `RegisterRestorationCompleted(relicId)` | 최종 복원이 완료된 **유물 종류 ID**를 등록한다. 최초 등록은 `true`, 모르는 ID와 이미 등록된 ID는 `false`다. |
| `IsRestored(relicId)` | 해당 종류가 최종 복원 완료로 등록되었는지 반환한다. |
| `IsUnread(relicId)` | 등록 후 아직 상세를 확인하지 않은 종류인지 반환한다. |
| `MarkViewed(relicId)` | 신규 상태를 해제한다. 실제 상태 변경 시 `true`; 복원 기록과 수집 개수는 유지된다. |
| `RestoredCount`, `TotalCount` | 완료된 유물 종류 수와 정의된 전체 유물 종류 수다. 중복 인스턴스와 건물은 유물 수집률에 포함되지 않는다. |
| `CollectionRate` | 0~1 비율. 전체 유물이 없으면 0이며 화면에서 백분율로 변환한다. |
| `RestoredInEra(eraId)`, `TotalInEra(eraId)` | 해당 시대의 완료 종류 수와 전체 종류 수다. |
| `IsBuildingUnlocked(eraId)` | 유물이 1종 이상 정의된 시대의 모든 종류가 완료되어야 `true`다. 빈 시대와 모르는 시대는 해금되지 않는다. |
| `CompletedBuildingCount` | 모든 유물이 완료된 시대의 건물 수다. 건물은 현재 시대당 하나다. |
| `HasUnreadInEra(eraId)` | 해당 시대에 상세를 확인하지 않은 신규 유물이 남아 있는지 반환한다. |
| `Changed` | 최초 완료 등록 또는 신규 확인처럼 실제 상태가 바뀔 때 한 번 발생한다. 중복 완료·중복 확인은 발생시키지 않는다. |

`CollectionPresenter.ShowRelic(id)`는 존재하면서 복원 완료된 유물에만 상세 진입을 허용하고, 정상 진입 시 `MarkViewed(id)`를 호출한다. 미복원 항목은 잠금과 `???`로 표시한다. 이후 같은 종류를 다시 복원해도 신규 표시를 다시 켜지 않는다.

## 향후 작업대 연결 순서

1. 공통 셸 수명으로 도감 기록 서비스를 구성한다. 작업대는 숨겨질 때 비활성화되는 `Pause While Hidden` 탭이므로, 공유 기록의 소유자를 작업대의 비활성 루트에 두지 않는다.
2. 셸과 도감이 같은 `CollectionCatalog.asset` 인스턴스를 사용해 공유 `CollectionProgress`를 생성한다.
3. 도감 씬이 준비된 후 `CollectionPresenter.BindProgress(sharedProgress)`로 주입한다. 이 메서드는 이전 기록의 구독을 해제하고 새 기록을 구독하며 화면 갱신을 요청한다. 카탈로그가 다른 기록과 `null`은 거절한다.
4. 현재 `Awake()`의 더미 초기 기록 주입을 실제 공유 기록 구성으로 교체한다. 더미 초기화와 실제 저장 기록 초기화를 동시에 유지하지 않는다.
5. 작업대 연결 계층은 인스턴스별 진행과 결과를 확인한 뒤, **3페이즈 전체의 최종 복원이 완료되었을 때만** 종류 ID로 `RegisterRestorationCompleted(kindId)`를 호출한다. 각 페이즈 성공, 발굴 시작, 유물 획득만으로 호출하지 않는다.

향후 구성 계층과 최종 완료 처리에서 사용할 최소 예시는 다음과 같다. 아래 코드는 현재 작업대에 연결된 구현이 아니다.

```csharp
using Jamkkaebi.Scripts.Gameplay.Collection;
using MobilePrototype.Collection;

// 공통 셸이 도감 씬 준비 후 구성한다.
CollectionCatalog catalog = presenter.Catalog;
CollectionProgress sharedProgress = new CollectionProgress(catalog);
presenter.BindProgress(sharedProgress);

// 작업대 연결 계층이 3페이즈 전체의 최종 완료를 확인한 다음 호출한다.
string kindId = "samguk-crown";
bool registered = sharedProgress.RegisterRestorationCompleted(kindId);
// registered가 false이면 모르는 ID 또는 이미 기록한 종류다.
// 이미 기록한 종류의 재복원은 새 도감 등록 이벤트를 만들지 않는다.
```

최종 복원 완료와 도감 등록 사이에 결과가 중복 전달되어도 종류 등록·건물 해금 개수는 증가하지 않는다. 도감 모델은 인스턴스 ID, 보유 수량, 복원 중 단계, 실패·재시도, 보상, 저장 성공을 판단하지 않는다. 이 책임은 향후 작업대·인벤토리·저장 계층에서 맡는다.

## 저장과 전시관의 미구현 범위

현재 모델에는 로컬 JSON, 서버 동기화, 저장 복구 API가 없다. 저장된 완료 기록을 단순히 `RegisterRestorationCompleted`로 재생하면 모두 신규가 되므로, 실제 저장 연동 전에는 **복원 완료 종류와 확인 여부를 함께 복구하는 API 및 신규 정책**을 설계해야 한다. 저장 기록을 읽는 것과 새 복원 완료 이벤트를 별도로 다룬다.

건물 해금 표시만으로 건물 인스턴스 생성·보유 수량 증가·전시관 배치를 실행하지 않는다. `HousingItemDefinition`, 전시관 배치 선택, 정령 인벤토리와의 연결은 별도 후속 작업이다. 도감 상세는 열람 역할을 유지한다.

## 검증 대상

규칙 회귀 검사는 `Assets/Jamkkaebi/Tests/EditMode/CollectionProgressTests.cs`가 담당한다. `Assets/MobileUI/Tests/EditMode/CollectionArtworkSyncTests.cs`는 임시 자산의 서로 다른 시대·유물 그림 10개를 저장·동기화·재로드하여 ID별 그림 GUID·localFileID와 카탈로그 GUID 보존을 검사한다. UI 입력 회귀 검사는 `CollectionScrollRectTests.cs`와 통합 검증기가 담당한다. 화면·입력·씬 변경은 프로젝트의 `Tools/VerifyIntegration.ps1`로 전체 통합 검증한다. 실행 결과와 로그 경로는 실제 실행 후 별도 검증 기록에 기재한다.

확정 시트 목록을 반영한 2026-10-09 실행 결과는 [collection-validation.md](collection-validation.md)에 기록했다. 초기 구현 결과와 후속 데이터 반영 결과를 구분한다. `CollectionScrollRect`는 뷰포트 높이 변경 전의 논리적 스크롤 위치를 유지하므로 프로필 표시가 다른 탭에 다녀와도 읽던 위치를 보존한다. 콘텐츠 교체와 명시적 페이지 위치 설정은 새 위치를 사용한다.

이미지 슬롯 확장 후 실제 스프라이트를 연결한 화면 렌더링, 잠금 그림과 복원 그림의 분리, `null` 슬롯의 대체 순서, 원본 색과 9-slice, 장식 그림의 입력 비간섭, 목록 동기화 후 전체 그림 참조와 자산 GUID 보존을 확인했다. 최종 EditMode 64개·Player 183개 실행 범위와 로그는 [collection-validation.md](collection-validation.md)에 기록했다.
