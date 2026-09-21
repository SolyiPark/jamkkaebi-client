# 코드 컨벤션 (jamkkaebi-client)

## 네이밍

| 대상 | 규칙 | 예시 |
| --- | --- | --- |
| 클래스, 메서드, public 필드/프로퍼티 | PascalCase | `GachaController`, `PullDaily()` |
| private 필드 | camelCase, `_` 접두사 | `_clarityGauge` |
| 지역변수, 매개변수 | camelCase | `spiritId` |
| 상수 | PascalCase | `MaxDailyPulls` |
| Inspector에 노출할 private 필드 | `[SerializeField] private` | `[SerializeField] private int _tier;` |

Microsoft C# 공식 컨벤션 + Unity 커뮤니티 관례를 따름.

- [C# 식별자 명명 규칙 및 관례](https://learn.microsoft.com/ko-kr/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [.NET 코딩 규칙 - C# | Microsoft Learn](https://learn.microsoft.com/ko-kr/dotnet/csharp/fundamentals/coding-style/coding-conventions)

**한 파일에 클래스 하나**: MonoBehaviour든 아니든, 파일명 = 클래스명.

**네임스페이스는 폴더 구조와 일치**: 예를 들어 `Gameplay/Gacha/` 폴더 안의 코드는 `namespace Jamkkaebi.Scripts.Gameplay.Gacha`.

## 프로퍼티 구현 방식

외부에 노출하는 멤버는 필드 대신 프로퍼티로 감싼다 (`public bool isRevealed;` 대신 `public bool IsRevealed { get; }`).

| 상황 | 방식 | 예시 |
| --- | --- | --- |
| 생성자에서만 값이 정해지고 이후 불변 | 자동 프로퍼티, `set` 없음 | `public TileContent Content { get; }` |
| 외부는 읽기만, 변경은 클래스 내부 로직(메서드)을 통해서만 | 자동 프로퍼티, `private set` | `public bool IsRevealed { get; private set; }` |
| get/set 본문에 추가 로직이 필요하지만 한 줄로 끝남 | Expression Body (`=>`) | `get => _isRevealed;` |
| get/set 본문에 여러 줄 로직 필요 | 중괄호 + `return` | `get { ...; return _x; }` |

private 필드를 직접 두고 프로퍼티로 감쌀 경우, 필드는 `_camelCase`, 프로퍼티는 `PascalCase`로 이름을 다르게 지어 역할을 구분한다.

---

## 커밋 컨벤션

### 형식

```
<type>: <description>
```

커밋 제목의 설명과 본문은 반드시 한국어로 작성합니다. `feat`, `fix` 등의 type 접두사와 코드 식별자·파일명·제품명은 원문을 사용할 수 있습니다. scope(`feat(gacha):`)는 표기하지 않습니다.

자동 생성하는 병합·되돌리기 커밋에도 같은 규칙을 적용합니다. 영어 기본 메시지를 그대로 사용하지 않습니다. 이미 공유한 과거 커밋은 메시지 형식을 바꾸기 위해 임의로 재작성하지 않습니다.

### type 목록

| type | 용도 |
| --- | --- |
| `feat` | 새 기능 |
| `fix` | 버그 수정 |
| `refactor` | 동작 변화 없는 코드 개선 |
| `test` | 테스트 코드 추가·수정 |
| `art` | 스프라이트/오디오 등 아트 에셋 추가·교체 |
| `chore` | 설정, 패키지, 문서 등 기타 |

### 예시

```
feat: 가챠 일일 1회 제한 로직 추가
fix: 선명도 게이지가 음수로 내려가는 버그 수정
art: 전시관 소나무 스프라이트 교체
```

### 원칙

**커밋 하나 = 논리적으로 하나의 변경.**
"가챠 로직 + UI 버튼 수정 + 오타 고침"을 한 커밋에 몰아넣지 않기.

---

## 브랜치 컨벤션

### 형식

```
<type>/<짧은-설명-kebab-case>
```

`type`은 커밋 컨벤션과 동일한 6개를 그대로 씁니다 (`feat`, `fix`, `refactor`, `test`, `art`, `chore`). 설명 부분은 영어 kebab-case로 통일합니다 (커밋 메시지는 한국어 자유서술이어도, 브랜치명은 CLI에서 자주 타이핑하므로 짧은 영어가 실용적입니다).

### 예시

```
feat/gacha-daily-limit
fix/clarity-gauge-negative
art/pavilion-pine-sprite
```

### 원칙

**`main`은 항상 빌드되고 실행 가능한 상태를 유지합니다.** 작업은 무조건 별도 브랜치에서 하고, 끝나면 PR로 합칩니다. 정식 리뷰 프로세스가 부담스러우면 합치기 전에 팀 채팅방에 한 줄 공유하는 정도로 충분합니다.
