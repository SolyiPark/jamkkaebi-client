using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Newtonsoft.Json.Linq;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 작업대 서버 API의 JSON(ApiResponse의 data)과 응답 그릇 사이를 바꾼다. HTTP는 모르고 값만 바꾼다.
    /// 형식이 맞지 않으면 <see cref="FormatException"/>을 던지고, 백엔드가 실패 콜백으로 바꾼다.
    /// </summary>
    /// <remarks>
    /// 서버 계약 기준은 jamkkaebi-server main이다(2026-11-28 PR #33으로 선명도 한 칸 회복 모양이 머지됨).
    /// 서버 시각은 오프셋 없는 한국 시간(LocalDateTime, Asia/Seoul)이라 UTC로 바꿔 담는다(클라이언트 시계가 UTC).
    /// </remarks>
    public static class WorkbenchServerMapper
    {
        private static readonly TimeSpan ServerOffset = TimeSpan.FromHours(9);

        // ---------- 요청 본문 ----------

        /// <summary>POST /api/box/open 본문.</summary>
        public static JObject BoxOpenBody(BoxSource source, string requestId)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("requestId가 비어 있습니다.", nameof(requestId));
            return new JObject { ["source"] = ServerName(source), ["requestId"] = requestId };
        }

        /// <summary>POST /api/purifications 본문. 난이도가 없으면 빼서 보내고 서버가 고정 난이도로 정한다.</summary>
        public static JObject StartBody(int relicId, MinigameDifficulty? difficulty)
        {
            JObject body = new JObject { ["relicId"] = relicId };
            if (difficulty.HasValue) body["difficulty"] = ServerName(difficulty.Value);
            return body;
        }

        /// <summary>POST /api/purifications/{sessionToken}/result 본문. 실패 사유는 실패일 때만 넣는다.</summary>
        public static JObject ReportBody(PlayReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            JObject body = new JObject
            {
                ["result"] = ServerName(report.Result),
                ["tapsUsed"] = report.TapsUsed,
                ["scoutsUsed"] = report.ScoutsUsed,
                ["damage"] = report.Damage,
                ["bonusTiles"] = new JObject { ["jeongseong"] = report.BonusJeongseong }
            };
            if (report.FailReason.HasValue) body["failReason"] = ServerName(report.FailReason.Value);
            return body;
        }

        /// <summary>PATCH /api/relics/{relicId}/spirit-name 본문. null이면 기본 이름으로 되돌린다.</summary>
        public static JObject RenameBody(string normalizedName)
        {
            return new JObject { ["name"] = normalizedName == null ? JValue.CreateNull() : (JToken)normalizedName };
        }

        // ---------- 응답 ----------

        /// <summary>GET /api/workbench. 건물 목록(buildings)은 아직 쓰지 않는다.</summary>
        public static WorkbenchSnapshot Snapshot(JToken data)
        {
            JObject root = Object(data, "작업대");
            List<WorkbenchSlot> slots = Array(root, "slots").Select(token =>
            {
                JObject slot = Object(token, "슬롯");
                return Build(() => new WorkbenchSlot(Int(slot, "relicId"), Enum<RevealLevel>(slot, "revealLevel"),
                    Enum<Phase>(slot, "currentPhase")));
            }).ToList();
            List<WorkbenchSpirit> spirits = Array(root, "spirits").Select(token =>
            {
                JObject spirit = Object(token, "정령");
                return Build(() => new WorkbenchSpirit(Int(spirit, "relicId"), String(spirit, "spiritName"),
                    Enum<Clarity>(spirit, "clarity"), Int(spirit, "careCount")));
            }).ToList();
            return Build(() => new WorkbenchSnapshot(Int(root, "capacity"), slots, spirits, Int(root, "lockedCount")));
        }

        /// <summary>GET /api/relics/{relicId}. 최초 정화 중이면 정령 이름·선명도·시각을 버린다(서버도 아직 공개 전이다).</summary>
        public static RelicDetail Detail(JToken data)
        {
            JObject root = Object(data, "유물 상세");
            JObject relic = Object(root["relic"], "유물 카드");
            RelicStatus status = Enum<RelicStatus>(root, "status");
            bool completed = status == RelicStatus.Completed;

            Clarity? clarity = completed ? Enum<Clarity>(root, "clarity") : (Clarity?)null;
            string spiritName = completed ? String(relic, "spiritName") : null;
            DateTime? lastPurifiedAt = completed ? Time(root, "lastPurifiedAt") : null;
            // 재봉인이면 더 떨어질 선명도가 없다.
            DateTime? changesAt = completed && clarity != Clarity.Resealed ? Time(root, "clarityChangesAt") : null;
            int careCount = completed ? Int(root, "careCount") : 0;
            PlayPlan nextPlay = Plan(Object(root["nextPlay"], "다음 판"), clarity);

            return Build(() => new RelicDetail(Int(relic, "relicId"), status, spiritName, clarity,
                lastPurifiedAt, changesAt, careCount, nextPlay));
        }

        /// <summary>POST /api/box/open. 중복 보상과 구매 지출은 없으면 0이다.</summary>
        public static BoxOpenOutcome BoxOutcome(JToken data)
        {
            JObject root = Object(data, "상자 결과");
            JObject relic = Object(root["relic"], "유물 카드");
            JObject reward = OptionalObject(root, "duplicateReward");
            JObject spent = OptionalObject(root, "spent");
            return Build(() => new BoxOpenOutcome(Enum<AcquireResult>(root, "result"), Int(relic, "relicId"),
                reward == null ? 0 : Int(reward, "jeongseong"),
                reward == null ? 0 : Int(reward, "eraCrystal"),
                spent == null ? 0 : Int(spent, "jeongseong")));
        }

        /// <summary>POST /api/purifications. 응답에 relicId가 없어 요청한 relicId를 받는다.</summary>
        public static PurificationSession Session(JToken data, int relicId)
        {
            JObject root = Object(data, "정화 세션");
            JObject board = Object(root["board"], "보드");
            PurificationBoardParameters parameters = new PurificationBoardParameters(Long(board, "seed"),
                Int(board, "width"), Int(board, "height"), Int(board, "threatCount"),
                Int(board, "reinforcedOnShape"), Int(board, "reinforcedOnField"), Int(board, "helperCount"),
                Int(board, "bonusJeongseongCount"), Int(board, "shapeCount"), Int(board, "shapeCellTotal"),
                Int(board, "tapBudget"), Int(board, "scoutLimit"));

            MinigameBoardConfig config;
            try { config = parameters.ToConfig(); }
            catch (InvalidOperationException error) { throw new FormatException(error.Message, error); }

            // 범위 검사를 통과해도 칸이 모자라는 등 판을 만들 수 없는 조합이 있다. 미니게임을 열어 셸을 가린 뒤에
            // 생성이 실패하면 되돌릴 길이 없으므로, 같은 설정·같은 시드로 지금 만들어 보고 안 되면 시작 실패로 돌린다.
            try { MinigameGridGenerator.Generate(config, parameters.Seed); }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            {
                throw new FormatException("서버 보드 값으로 판을 만들 수 없습니다. " + error.Message, error);
            }

            SealMedium medium;
            try { medium = SealMediumParser.Parse(String(root, "sealMedium")); }
            catch (ArgumentException error) { throw new FormatException(error.Message, error); }

            return Build(() => new PurificationSession(String(root, "sessionToken"), relicId,
                Enum<EntryRoute>(root, "entryRoute"), Enum<Phase>(root, "phase"),
                Enum<MinigameDifficulty>(root, "difficulty"), medium, parameters.Seed, config));
        }

        /// <summary>
        /// POST /api/purifications/{sessionToken}/result. 서버 결과에는 진입 경로·난이도가 없어 시작한 판(session)에서 가져온다.
        /// </summary>
        /// <remarks>
        /// 서버 next는 action·phase·remainingPhases만 주고, 실패(RETRY)면 phase도 없다. 결과 화면은 다음 계획의 Phase와
        /// (복구 중이면) Clarity만 쓰고, 다음 판은 상세를 다시 조회해 서버 nextPlay로 정하므로 빈 값은 이렇게 채운다.
        /// <list type="bullet">
        /// <item>난이도 목록: 이번 판 난이도 하나.</item>
        /// <item>재시도 페이즈: 최초 정화면 이번 판 페이즈부터 오염 관리까지. 관리 플레이면 응답의 지금 선명도로 정한다
        /// (플레이 도중 경계를 넘어 떨어졌으면 다음 판이 달라진다. 예: 기억 회복 중 재봉인 → 봉인 해제부터).</item>
        /// </list>
        /// </remarks>
        public static SettlementResult Settlement(JToken data, PurificationSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            JObject root = Object(data, "정산 결과");
            JObject next = Object(root["next"], "다음 행동");
            PlayResult result = Enum<PlayResult>(root, "result");
            RelicStatus status = Enum<RelicStatus>(root, "status");
            NextAction action = Enum<NextAction>(next, "action");
            RevealLevel? revealed = OptionalEnum<RevealLevel>(root, "revealed");
            bool completedNow = result == PlayResult.Success && session.Route == EntryRoute.First &&
                                status == RelicStatus.Completed;
            IReadOnlyList<MinigameDifficulty> difficulties = new[] { session.Difficulty };

            PlayPlan nextPlan;
            switch (action)
            {
                case NextAction.Done:
                    nextPlan = null;
                    break;
                case NextAction.NextPhase:
                    nextPlan = Build(() => new PlayPlan(EntryRoute.First, Remaining(next), difficulties, null));
                    break;
                case NextAction.ContinueRestore:
                    // 서버 clarity는 이번 판으로 오른 선명도다.
                    Clarity reached = Enum<Clarity>(root, "clarity");
                    nextPlan = Build(() => new PlayPlan(EntryRoute.Care, Remaining(next), difficulties, reached));
                    break;
                case NextAction.Retry:
                    Clarity? clarity = session.Route == EntryRoute.Care ? Enum<Clarity>(root, "clarity") : (Clarity?)null;
                    IReadOnlyList<Phase> retry = clarity.HasValue
                        ? ClarityPolicy.Default.RemainingPhases(clarity.Value)
                        : PhasesFrom(session.Phase);
                    nextPlan = Build(() => new PlayPlan(session.Route, retry, difficulties, clarity));
                    break;
                default:
                    throw new FormatException($"다음 행동 {action}을 처리할 수 없습니다.");
            }

            return Build(() => new SettlementResult(result, action, nextPlan, completedNow, revealed));
        }

        /// <summary>PATCH /api/relics/{relicId}/spirit-name. 서버가 풀어 준 이름(사용자 이름 또는 기본 이름).</summary>
        public static string SpiritName(JToken data)
        {
            JObject root = Object(data, "이름 변경 결과");
            return String(Object(root["relic"], "유물 카드"), "spiritName");
        }

        // ---------- 열거형 ----------

        /// <summary>C# 열거형 이름을 서버 표기로 바꾼다(ContinueRestore → CONTINUE_RESTORE).</summary>
        public static string ServerName<T>(T value) where T : struct, Enum
        {
            if (!System.Enum.IsDefined(typeof(T), value))
                throw new ArgumentException($"{typeof(T).Name} {value}는 정의되지 않았습니다.", nameof(value));

            string name = value.ToString();
            StringBuilder builder = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) builder.Append('_');
                builder.Append(char.ToUpperInvariant(name[i]));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 서버 표기를 열거형으로 바꾼다. 표기가 정확히 같을 때만 받는다.
        /// Enum.TryParse는 "2" 같은 숫자 문자열도 받아들이므로 쓰지 않는다.
        /// </summary>
        public static bool TryParse<T>(string serverName, out T value) where T : struct, Enum
        {
            foreach (T candidate in System.Enum.GetValues(typeof(T)))
            {
                if (ServerName(candidate) != serverName) continue;
                value = candidate;
                return true;
            }
            value = default;
            return false;
        }

        // ---------- 시각 ----------

        /// <summary>서버 시각(오프셋 없는 한국 시간, 또는 오프셋이 있는 시각)을 UTC로 바꾼다.</summary>
        public static DateTime ParseServerTime(JToken token)
        {
            // ServerApiResult는 JObject.Parse를 써서 날짜처럼 생긴 문자열이 이미 Date 토큰으로 바뀌어 온다.
            if (token?.Type == JTokenType.Date)
            {
                object raw = ((JValue)token).Value;
                if (raw is DateTimeOffset offset) return offset.UtcDateTime;
                DateTime date = (DateTime)raw;
                return date.Kind == DateTimeKind.Unspecified
                    ? new DateTimeOffset(date, ServerOffset).UtcDateTime
                    : date.ToUniversalTime();
            }

            if (token?.Type == JTokenType.String &&
                DateTime.TryParse((string)token, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                if (parsed.Kind == DateTimeKind.Unspecified) return new DateTimeOffset(parsed, ServerOffset).UtcDateTime;
                return parsed.ToUniversalTime();
            }

            throw new FormatException("서버 시각 형식이 올바르지 않습니다.");
        }

        // ---------- 내부 ----------

        private static PlayPlan Plan(JObject plan, Clarity? clarity)
        {
            EntryRoute route = Enum<EntryRoute>(plan, "entryRoute");
            List<Phase> remaining = Array(plan, "remainingPhases").Select(token => EnumValue<Phase>(token, "remainingPhases")).ToList();
            List<MinigameDifficulty> difficulties = Array(plan, "difficulties")
                .Select(token => EnumValue<MinigameDifficulty>(token, "difficulties")).ToList();
            if (remaining.Count == 0 || remaining[0] != Enum<Phase>(plan, "phase"))
                throw new FormatException("다음 판의 페이즈와 남은 순서가 맞지 않습니다.");
            return Build(() => new PlayPlan(route, remaining, difficulties, route == EntryRoute.Care ? clarity : null));
        }

        private static IReadOnlyList<Phase> Remaining(JObject next)
        {
            Phase phase = Enum<Phase>(next, "phase");
            if (next["remainingPhases"] == null || next["remainingPhases"].Type == JTokenType.Null) return new[] { phase };
            List<Phase> remaining = Array(next, "remainingPhases").Select(token => EnumValue<Phase>(token, "remainingPhases")).ToList();
            if (remaining.Count == 0 || remaining[0] != phase)
                throw new FormatException("다음 페이즈와 남은 순서가 맞지 않습니다.");
            return remaining;
        }

        private static IReadOnlyList<Phase> PhasesFrom(Phase phase) =>
            ((Phase[])System.Enum.GetValues(typeof(Phase))).Where(candidate => candidate >= phase).ToList();

        // 그릇 생성자의 검사 실패(ArgumentException)도 서버 응답 형식 오류로 본다.
        private static T Build<T>(Func<T> create)
        {
            try { return create(); }
            catch (ArgumentException error) { throw new FormatException(error.Message, error); }
        }

        private static JObject Object(JToken token, string what) =>
            token as JObject ?? throw new FormatException($"{what} 응답 형식이 올바르지 않습니다.");

        private static JObject OptionalObject(JObject root, string key)
        {
            JToken token = root[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            return Object(token, key);
        }

        private static JArray Array(JObject root, string key) =>
            root[key] as JArray ?? throw new FormatException($"{key} 목록이 없습니다.");

        private static int Int(JObject root, string key)
        {
            JToken token = root[key];
            if (token?.Type != JTokenType.Integer) throw new FormatException($"{key} 값이 정수가 아닙니다.");
            try { return (int)token; }
            catch (OverflowException) { throw new FormatException($"{key} 값이 범위를 벗어났습니다."); }
        }

        private static long Long(JObject root, string key)
        {
            JToken token = root[key];
            if (token?.Type != JTokenType.Integer) throw new FormatException($"{key} 값이 정수가 아닙니다.");
            try { return (long)token; }
            catch (OverflowException) { throw new FormatException($"{key} 값이 범위를 벗어났습니다."); }
        }

        private static string String(JObject root, string key)
        {
            JToken token = root[key];
            if (token?.Type != JTokenType.String || string.IsNullOrEmpty((string)token))
                throw new FormatException($"{key} 값이 없습니다.");
            return (string)token;
        }

        private static DateTime? Time(JObject root, string key)
        {
            JToken token = root[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            return ParseServerTime(token);
        }

        private static T Enum<T>(JObject root, string key) where T : struct, Enum => EnumValue<T>(root[key], key);

        private static T? OptionalEnum<T>(JObject root, string key) where T : struct, Enum
        {
            JToken token = root[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            return EnumValue<T>(token, key);
        }

        private static T EnumValue<T>(JToken token, string key) where T : struct, Enum
        {
            if (token?.Type == JTokenType.String && TryParse((string)token, out T value)) return value;
            throw new FormatException($"{key} 값({token})을 {typeof(T).Name}으로 읽을 수 없습니다.");
        }
    }
}
