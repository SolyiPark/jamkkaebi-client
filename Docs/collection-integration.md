# 도감 구현과 작업대 연결 계약

이 문서는 현재 도감 화면과 작업대 연결 계층의 경계를 기록한다. 2026-11-25부터 도감은 셸의 공유 기록(`SharedRecords`)을 보고, 작업대가 최초 정화를 마친 유물을 그 기록에 등록한다. 2026-11-27부터 서버 주소가 있으면 작업대는 서버 백엔드를 쓰고, 작업대 조회 결과로 도감 등록을 채우는 흐름은 같다([작업대 서버 연결](workbench-server-integration.md)). 서버 주소가 없으면 모의 백엔드다.

## 현재 구조

- `Assets/Jamkkaebi/Scenes/Collection.unity`: 기존 도감 탭 씬. `MirrorSceneRoot` 아래에서 시대 목록 → 시대 상세 → 유물 상세를 전환한다.
- `Assets/Jamkkaebi/Configuration/CollectionCatalog.asset`: 도감의 고정 정의 자산. `CollectionPresenter`와 공유 진행 기록은 **같은 자산 인스턴스**를 사용해야 한다.
- `Assets/Jamkkaebi/Configuration/CollectionAppearance.asset`: 도감 공통 배경·카드·아이콘·장식의 선택적 스프라이트를 담는 `CollectionAppearance` 자산. 도감 씬 `CollectionPresenter`의 `_appearance`에 연결한다.
- `Assets/Jamkkaebi/Scripts/Gameplay/Collection/`: 고정 정의와 수집 규칙. UI, 작업대 어댑터, 전시관 배치 타입에 의존하지 않는다.
- `Assets/Jamkkaebi/Scripts/UI/Collection/CollectionPresenter.cs`: 화면 이동, 신규 확인과 변경 이벤트 구독. 표시 데이터 복사본을 View에 전달하며 UI 배치는 소유하지 않는다.
- `CollectionView` / `CollectionDefaultView`: 교체 가능한 표시 계약과 기존 디자인 구현. 그림·좌표·색상·UI 생성은 기본 View에 있다. Presenter의 선택적 `_view`에 다른 씬 View 인스턴스를 연결할 수 있다. 교체 방법과 아직 프리팹화하지 않은 범위는 [표시 계층 분리 기록](collection-view-separation.md)을 참고한다.
- `Assets/Jamkkaebi/Scripts/UI/Collection/CollectionCatalogData.cs`: 도감 구조(시대·유물 ID, 소속, 순서, 강조색)와 서버 시드 기준 이름(시대·건물·유물·도깨비 기본 이름)만 만든다. 화면 문구는 넣지 않는다.
- `Assets/Jamkkaebi/Configuration/Text/collection_eras.csv`, `collection_relics.csv`: 시대 기간 문구·시대 설명, 유물 설명·복원 기록의 원본. 기획 담당의 구글 시트에서 내보낸 파일이며 에디터 임포트로만 읽는다(런타임에 파싱하지 않는다). 현재 문구는 임시 초안이다.
- `Assets/Jamkkaebi/Editor/TextTables/`: 에디터 전용 어셈블리 `Jamkkaebi.Editor.TextTables`. RFC 4180 CSV 파서(`CsvReader`), 헤더·키 검증(`TextTable`, `TextTableSchema`, `TextTableReport`), 도감 임포트(`Collection/CollectionTextImport`)가 있다. 파서와 검증은 다른 텍스트 표에도 재사용한다.
- `Assets/Jamkkaebi/Scripts/UI/Shell/SharedRecords.cs`: 셸(`UIPrototype.unity`의 `Shared Records` 오브젝트) 수명의 공유 기록. 작업대 백엔드·시계·도감 기록(`CollectionProgress`)·`CollectionRecorder`를 하나씩 만들고 `SharedRecords.Current`로 노출한다.
- `Assets/Jamkkaebi/Scripts/UI/Shell/CollectionRecorder.cs`: 작업대 `relicId`를 `WorkbenchRelicInfo.CollectionId`(도감 종류 ID)로 바꿔 등록한다.

`CollectionPresenter.Awake()`는 `SharedRecords.Current`가 있으면 그 `Collection`을 `Progress`로 쓴다. 공유 기록의 카탈로그가 도감의 `_catalog`와 다른 인스턴스면 오류를 남기고 빈 기록으로 시작한다. 셸 없이 도감 씬만 열리면(EditMode 테스트 포함) 빈 기록으로 시작한다. 예전 화면 검토용 샘플 기록(`CollectionDemoData`, 5/9종)은 2026-11-25에 삭제했다.

셸로 시작하면 `SharedRecords.Start`가 작업대 조회의 정령 목록(모의 백엔드는 2·8·9번)을 신규 표시 없이 등록해 3/9종으로 시작한다. 모의 응답은 0.3초 뒤에 오므로 도감이 먼저 그려지면 잠깐 0/9가 보였다가 `Changed` 알림으로 다시 그려진다. 기록은 플레이 세션 메모리에만 존재하며 재실행하면 초기화된다.

## 확정 목록의 원본과 적용 범위

2026-11-24부터 시대와 유물의 구성·순서·이름은 서버 `ContentSeedData`(2026-11-19 확인)를 따른다. 유물 순서는 서버 `relicId` 1~9 순서이고 시대 표시명은 **삼국·고려·조선**이다. 처음 목록의 원본이었던 [「깨비별 특성」 시트1](https://docs.google.com/spreadsheets/d/1h2kPX9OrotI1mofpJxDIXg06z8RpFlT2e5LXNudL_rw/edit?gid=0) `A2:C10`과는 1·3·4·5·7번 유물이 다르다. `CollectionCatalogData.CreateCatalog()`가 이 목록을 생성하고, 도감 텍스트 임포트가 CSV 문구를 합쳐 `CollectionCatalog.asset`에 저장한다. 런타임 화면은 저장된 자산만 사용한다.

유물·도깨비·건물 이름의 기준은 서버 `ContentSeedData`의 `realName`, `spiritDefaultName`, 건물 `name`이다. 작업대 모의 백엔드가 쓰는 `WorkbenchRelicSeed`도 같은 시드를 옮긴 것이며, `WorkbenchRelicSeedTests`가 두 목록의 이름·시대·도감 ID가 같은지 검사한다. 클라이언트에 복사한 값이므로 서버 시드가 바뀌면 함께 갱신한다. 도깨비 이름은 주인이 짓기 전의 기본 이름이며 친구 전시 응답의 `spiritName`과 다를 수 있다. 화면의 "도깨비" 표기와 도깨비 이름은 확정 전 임시 내용이다. `SpiritName`, `_spiritName`, `SpiritIllustration` 같은 코드·직렬화 식별자는 씬·자산 참조를 지키기 위해 바꾸지 않았다.

같은 시트의 `D:I`는 아트 방향 후보다. 물음표·`X` 등 후보 상태를 포함하므로 해당 특성을 확정 도감 설명, 복원·보존 이야기로 해석하지 않는다. 유물 설명과 복원 이야기는 임시 초안임을 화면에 표시하고, 실제 자료가 준비되면 아래 [도감 텍스트를 시트에서 갱신하는 방법](#도감-텍스트를-시트에서-갱신하는-방법)으로 교체한다.

## 데이터 자산 계약

`CollectionCatalog`는 `ScriptableObject`다. `Configure(eras, relics)`는 시대·유물 ID의 중복, 비어 있는 ID, 유물의 존재하지 않는 시대 참조를 거절한다. `Validate()`는 저장 자산을 Inspector에서 편집하거나 다시 불러온 경우에도 같은 조건을 검사하며, `CollectionProgress` 생성자가 이를 호출한다. 정의 자산을 모두 구성한 다음 `CollectionProgress`를 생성하고, 사용 중인 카탈로그를 재구성하지 않는다. `Eras`와 `Relics`는 읽기 전용 목록이고 `FindEra(id)`, `FindRelic(id)`는 알려지지 않은 ID에 `null`을 반환한다.

| 정의 | 저장 필드와 읽기 속성 |
| --- | --- |
| `CollectionEraDefinition` | ID, 이름, 표시용 시대 문구, 설명, 해금 건물 이름, 강조색, 선택적 `OverviewIllustration`, `ContextIllustration`, `BuildingIllustration`, `LockedBuildingIllustration`, `BackgroundIllustration` |
| `CollectionRelicDefinition` | ID, 시대 ID, 유물 이름, 도깨비 이름, 유물 설명, 복원·보존 이야기 초안, 강조색, 선택적 `Illustration`, `SpiritIllustration`, `Thumbnail`, `LockedIllustration`, `BackgroundIllustration` |

고정 ID와 표시명은 아래와 같다. 유물 ID는 이름과 별개이므로 그림·설명을 교체해도 변경하지 않는다. 서버 `relicId`와 도감 유물 ID의 대응은 작업대의 `WorkbenchRelicInfo.CollectionId`에 있다(서버는 도감 ID를 모른다).

| 순서 | 시대 표시명 / ID | 유물 표시명 | 도깨비 기본 이름 | 유물 ID | 서버 `relicId` |
| --- | --- | --- | --- | --- | --- |
| 1 | 삼국 / `samguk` | 거북이 모양 연적 | 거북이 | `samguk-turtle-water-dropper` | 1 |
| 2 | 삼국 / `samguk` | 금동대향로 | 향산이 | `samguk-incense-burner` | 2 |
| 3 | 삼국 / `samguk` | 개구리 모양 토우 | 개굴이 | `samguk-frog-figurine` | 3 |
| 4 | 고려 / `goryeo` | 청자 석류 모양 연적 | 석류동이 | `goryeo-pomegranate-water-dropper` | 4 |
| 5 | 고려 / `goryeo` | 상감 청자 매병 | 구름결 | `goryeo-celadon` | 5 |
| 6 | 고려 / `goryeo` | 나전칠기 | 밤바다 | `goryeo-lacquerware` | 6 |
| 7 | 조선 / `joseon` | 봉 뒤꽂이 | 봉이 | `joseon-phoenix-hairpin` | 7 |
| 8 | 조선 / `joseon` | 마패 | 말발굽 | `joseon-horse-medallion` | 8 |
| 9 | 조선 / `joseon` | 묘작도 | 묘작이 | `joseon-cat-sparrow-painting` | 9 |

시대별 건물 이름은 삼국 석굴암(서버 `buildingId` 1), 고려 경천사지 십층석탑(2), 조선 경복궁(3)이다.

초기 샘플의 `samguk-pottery`, `goryeo-bronze-mirror`, `goryeo-bell`, `joseon-white-porcelain`, `joseon-brush`, `joseon-sundial`은 폐기했다. 2026-11-24에는 서버 시드에 맞추면서 `samguk-standing-buddha`, `samguk-crown`, `goryeo-stone-buddha`, `joseon-royal-seal`을 위 표의 새 ID로 바꿨다(연결된 그림이 없어 잃은 참조는 없다). 영속 저장이 아직 연결되지 않았으므로 이전 ID에서 새 ID로의 별칭이나 저장 마이그레이션은 만들지 않는다. 작업대는 위 확정 목록의 종류 ID로 등록한다.

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
| 유물 상세의 도깨비 그림 | 유물 `SpiritIllustration` → 기존 도깨비 벡터 그림 |
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
| `SpiritPlateBackground` | 유물 상세의 도깨비 그림 패널 / 기존 종이색 패널 |
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

## 도감 텍스트를 시트에서 갱신하는 방법

시대 기간 문구·시대 설명, 유물 설명·복원 기록은 구글 시트 → CSV → `CollectionCatalog.asset` 순서로 반영한다. 시트 양식과 기획 담당용 안내는 [도감 텍스트 시트 안내](collection-text-sheet-guide.md)에 있다.

1. 기획 담당이 구글 시트 [잠깨비 도감 텍스트](https://docs.google.com/spreadsheets/d/1k6CzJz-yQBEhWgbraVwkeBoApFrwjlaZlPwYRGy2VRU/edit)의 `collection_eras`, `collection_relics` 탭을 고친다. 고치는 칸은 노란색, 고치지 않는 칸은 회색이며 헤더 행과 `id` 열은 경고 보호가 걸려 있다.
2. 탭마다 **파일 → 다운로드 → 쉼표로 구분된 값(.csv, 현재 시트)**으로 내보낸다.
3. 내려받은 파일 이름을 `collection_eras.csv`, `collection_relics.csv`로 바꿔 `Assets/Jamkkaebi/Configuration/Text/`의 같은 파일을 덮어쓴다. `.meta`는 그대로 둔다.
4. Play를 종료하고 Unity 메뉴 **`Jamkkaebi > Collection > Import Text CSV`**를 실행한다. 대화 상자와 Console에 바뀐 행 수, 경고, 오류가 나온다.
5. 오류가 있으면 자산은 바뀌지 않는다. 시트를 고쳐 2번부터 다시 한다. 경고는 반영되지만 내용을 확인한다.
6. CSV 두 개와 `CollectionCatalog.asset`을 함께 커밋한다. `CollectionCatalogDataTests.SceneCatalogAssetMatchesStructureAndCsv`는 둘이 어긋나면(메뉴를 실행하지 않고 CSV만 커밋하면) 실패한다.

| 구분 | 열 | 시트에서 바꾸면 |
| --- | --- | --- |
| 키 | `id` | 바꾸지 않는다. 모르는 ID·빠진 ID·중복 ID는 오류다. |
| 반영 | `기간 문구`, `시대 설명`, `유물 설명`, `복원 기록` | 자산에 반영한다. 비어 있으면 오류다. 앞뒤 공백은 지운다. |
| 반영(한 줄) | `기간 문구` | 시대 카드의 한 줄 자리에 들어가므로 줄바꿈이 있거나 20자(`CollectionTextImport.PeriodMaxLength`)를 넘으면 오류다. |
| 참고 | `시대 이름(참고)`, `건물 이름(참고)`, `시대(참고)`, `유물 이름(참고)`, `도깨비 기본 이름(참고)` | 반영하지 않는다. 값이 있는데 서버 기준 이름과 다르면 경고만 한다. 열을 지우거나 비워도 된다. |
| 무시 | `비고`, 헤더가 빈 열 | 읽지 않는다. |
| 그 밖 | 스키마에 없는 헤더 | 읽지 않고 경고한다(헤더 오타 확인용). |

`시대 설명`, `유물 설명`, `복원 기록`은 길이 제한이 없다. 기본 View(`CollectionDefaultView`)가 디자인 칸보다 긴 본문의 실제 높이만큼 아래 요소와 스크롤 높이를 밀어낸다.

행 순서는 의미가 없다. 화면 순서는 `CollectionCatalogData`(서버 `relicId` 순서)를 따른다. 모든 셀이 빈 행은 건너뛴다. 셀 안 줄바꿈은 `\n`으로 맞춰 그대로 화면 줄바꿈이 된다. CSV는 UTF-8이며 BOM이 있어도 된다. 따옴표가 닫히지 않는 등 CSV 형식이 깨지면 행 번호와 함께 오류로 보고한다. 보고의 행 번호는 시트의 행 번호(헤더가 1행)와 같다.

유물·도깨비·시대·건물 이름은 서버 `ContentSeedData`가 기준이므로 시트로 바꾸지 않는다. 바꿔야 하면 서버 시드와 `CollectionCatalogData`, `WorkbenchRelicSeed`를 함께 고친 뒤 시트의 참고 열도 맞춘다.

도감 화면은 legacy `Text`와 동적 폰트라 글자 제한이 없다. 이후 같은 방식으로 TMP 화면(미니게임 도움말 등)의 문구를 임포트할 때는 정적 아틀라스의 2,350자 밖 글자가 있는지 확인하고, 있으면 `Jamkkaebi/Fonts/Bake Static Font Atlases`를 실행한다.

## 구조와 텍스트를 저장 자산에 반영하는 방법

Unity 메뉴 **`Jamkkaebi > Collection > Import Text CSV`**, **`Prototype > Collection > Sync Catalog From Confirmed Data`**(같은 동작) 또는 batch mode의 **`CollectionSetup.SyncCatalog`**는 `CollectionCatalogData.CreateCatalog()`의 구조·이름과 `Configuration/Text`의 CSV 문구를 합쳐 `Assets/Jamkkaebi/Configuration/CollectionCatalog.asset`에 반영한다. `CollectionSetup.ConfigureImages`도 내부에서 같은 동기화를 하므로 CSV 문구를 하드코딩 문구로 덮어쓰지 않는다. 이 명령은 도감 씬을 이관하거나 재구성하지 않는다. `CollectionSetup.Apply()`는 초기 씬 이관 전용이므로 목록 갱신에 재실행하지 않는다.

기존 카탈로그가 있으면 같은 자산을 갱신해 `.meta` GUID와 씬의 카탈로그 참조를 유지한다. 같은 시대 ID는 기존 `OverviewIllustration`, `ContextIllustration`, `BuildingIllustration`, `LockedBuildingIllustration`, `BackgroundIllustration` 참조를 보존한다. 같은 유물 ID는 기존 `Illustration`, `SpiritIllustration`, `Thumbnail`, `LockedIllustration`, `BackgroundIllustration` 참조를 보존한다. 공통 `CollectionAppearance.asset`은 카탈로그 동기화 대상이 아니다. 시대·유물 순서와 이름, 도깨비 기본 이름, 건물 이름은 `CollectionCatalogData`, 설명류 문구는 CSV 값으로 교체한다. 삭제된 유물 ID의 아트를 새 ID로 옮기지는 않는다. CSV 검증에 실패하면 자산을 바꾸지 않고 `InvalidOperationException`으로 멈춘다.

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

완료 로그의 표시는 `COLLECTION_CONFIRMED_CATALOG_SYNCED`이며 뒤에 바뀐 행 수와 경고 수가 붙는다. 경고는 `도감 텍스트 임포트 경고:`, 오류는 `도감 텍스트 임포트 오류:`로 시작한다. 이 목록 동기화 로그만으로 도감 화면이나 전체 통합 검증의 성공을 판단하지 않는다.

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

## 작업대 연결

1. 공유 기록은 셸 수명의 `SharedRecords`가 소유한다. 작업대는 숨겨질 때 비활성화되는 `Pause While Hidden` 탭이므로, 공유 기록의 소유자를 작업대의 비활성 루트에 두지 않는다.
2. 셸의 `SharedRecords._collectionCatalog`와 도감 `CollectionPresenter._catalog`는 같은 `CollectionCatalog.asset`을 연결한다. 같은 자산이면 같은 인스턴스이므로 카탈로그 검사를 통과한다.
3. 작업대 `WorkbenchPresenter`가 `SharedRecords.Current`의 백엔드·시계·`CollectionRecorder`를 `WorkbenchScreenController.Initialize`에 넘긴다. 컨트롤러는 전역 상태를 직접 읽지 않는다. 셸이 없으면 기존처럼 자기 데모 백엔드를 만들고 도감에 등록하지 않는다.
4. 정산 결과의 `CompletedNow`가 true일 때(최초 정화의 마지막 페이즈 성공)만 `RegisterCompleted(relicId, alreadySeen: false)`로 신규 등록한다. 개별 페이즈 성공, 상자 열기, 관리 플레이로 선명도가 오르는 것은 등록 조건이 아니다. 응답 전에 화면을 옮겨도 등록이 빠지지 않도록 화면 갱신 세대 검사보다 먼저 처리한다.
5. 작업대 조회(`GetWorkbench`)의 정령 목록으로 빠진 등록을 채운다(`CollectionRecorder.SyncCompleted`). 셸 시작 시 채우기와 작업대 홈 조회가 모두 이 메서드를 부른다. 처음 받은 목록은 시작 상태로 보고 신규 표시 없이(`alreadySeen: true`, 등록 직후 `MarkViewed`) 등록하고, 그 뒤 목록에서 새로 보이는 정령은 신규로 등록한다. 둘 중 먼저 온 응답이 시작 상태가 된다.

`CollectionPresenter.BindProgress(progress)`는 화면이 떠 있는 동안 기록을 교체할 때(계정 전환 등) 쓴다. 이전 기록의 구독을 해제하고 새 기록을 구독하며 화면 갱신을 요청한다. 카탈로그가 다른 기록과 `null`은 거절한다. 시작할 때는 `Awake`에서 공유 기록을 바로 넣으므로 쓰지 않는다.

보고 응답을 잃어 같은 결과를 다시 보내도 서버·모의 백엔드는 한 번만 정산하고, `RegisterRestorationCompleted`는 이미 등록한 종류에 false를 돌려주므로 종류 등록·건물 해금 개수는 증가하지 않는다. 마지막 페이즈의 정산 응답을 잃은 채 재전송 화면에서 `홈으로` 나가거나, 보고 중에 작업대 탭이 숨겨져 컨트롤러가 늦은 응답을 버리면 `RegisterCompleted`가 불리지 않는다. 이때는 다음 작업대 홈 조회의 `SyncCompleted`가 백엔드의 정령 목록을 기준으로 신규 등록해 누락을 메운다(`CollectionRecorderTests.ReportResponseLostThenHome_WorkbenchSnapshotRegistersCompletedSpirit`). 도감 모델은 인스턴스 ID, 보유 수량, 복원 중 단계, 실패·재시도, 보상, 저장 성공을 판단하지 않는다. 이 책임은 작업대·인벤토리·저장 계층에서 맡는다.

## 저장과 전시관의 미구현 범위

현재 모델에는 로컬 JSON, 서버 동기화, 저장 복구 API가 없다. 저장된 완료 기록을 단순히 `RegisterRestorationCompleted`로 재생하면 모두 신규가 되므로, 실제 저장 연동 전에는 **복원 완료 종류와 확인 여부를 함께 복구하는 API 및 신규 정책**을 설계해야 한다. 저장 기록을 읽는 것과 새 복원 완료 이벤트를 별도로 다룬다.

건물 해금 표시만으로 건물 인스턴스 생성·보유 수량 증가·전시관 배치를 실행하지 않는다. `HousingItemDefinition`, 전시관 배치 선택, 도깨비 인벤토리와의 연결은 별도 후속 작업이다. 도감 상세는 열람 역할을 유지한다.

## 검증 대상

규칙 회귀 검사는 `Assets/Jamkkaebi/Tests/EditMode/CollectionProgressTests.cs`가 담당한다. `Assets/Jamkkaebi/Tests/UI/EditMode/CollectionArtworkSyncTests.cs`는 임시 자산의 서로 다른 시대·유물 그림 10개를 저장·동기화·재로드하여 ID별 그림 GUID·localFileID와 카탈로그 GUID 보존을 검사한다. UI 입력 회귀 검사는 `CollectionScrollRectTests.cs`와 통합 검증기가 담당한다. 화면·입력·씬 변경은 프로젝트의 `Tools/VerifyIntegration.ps1`로 전체 통합 검증한다. 실행 결과와 로그 경로는 실제 실행 후 별도 검증 기록에 기재한다.

확정 시트 목록을 반영한 2026-10-09 실행 결과는 [collection-validation.md](collection-validation.md)에 기록했다. 초기 구현 결과와 후속 데이터 반영 결과를 구분한다. `CollectionScrollRect`는 뷰포트 높이 변경 전의 논리적 스크롤 위치를 유지하므로 프로필 표시가 다른 탭에 다녀와도 읽던 위치를 보존한다. 콘텐츠 교체와 명시적 페이지 위치 설정은 새 위치를 사용한다.

이미지 슬롯 확장 후 실제 스프라이트를 연결한 화면 렌더링, 잠금 그림과 복원 그림의 분리, `null` 슬롯의 대체 순서, 원본 색과 9-slice, 장식 그림의 입력 비간섭, 목록 동기화 후 전체 그림 참조와 자산 GUID 보존을 확인했다. 최종 EditMode 64개·Player 183개 실행 범위와 로그는 [collection-validation.md](collection-validation.md)에 기록했다.
