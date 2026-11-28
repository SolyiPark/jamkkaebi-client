using System;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Workbench;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 작업대 서버 응답(JSON 샘플) → 응답 그릇 변환을 검사합니다. 서버 없이 돌며 실제 서버 검증이 아닙니다.
    /// 샘플은 jamkkaebi-server main(PR #33 머지 이후)의 DTO 모양을 따릅니다.
    /// </summary>
    public sealed class WorkbenchServerMapperTests
    {
        // 실제 응답처럼 ApiResponse 봉투를 ServerApiResult.Parse로 읽는다(날짜 문자열이 Date 토큰으로 바뀌는 것까지 같게).
        private static JToken Data(string dataJson)
        {
            string json = "{\"success\":true,\"code\":\"S000\",\"message\":\"성공\",\"data\":" + dataJson.Replace('\'', '"') + "}";
            return ServerApiResult.Parse(json, 200).Data;
        }

        private static PurificationSession FirstSession(Phase phase = Phase.Unseal) =>
            new PurificationSession("token-1", 3, EntryRoute.First, phase, MinigameDifficulty.Normal, SealMedium.Root, 1L);

        private static PurificationSession CareSession(Phase phase, MinigameDifficulty difficulty) =>
            new PurificationSession("token-2", 2, EntryRoute.Care, phase, difficulty, SealMedium.Soot, 1L);

        // ---------- 열거형 ----------

        [TestCase(NextAction.ContinueRestore, "CONTINUE_RESTORE")]
        [TestCase(NextAction.NextPhase, "NEXT_PHASE")]
        [TestCase(FailReason.TapsExhausted, "TAPS_EXHAUSTED")]
        [TestCase(RelicStatus.InProgress, "IN_PROGRESS")]
        [TestCase(BoxSource.Free, "FREE")]
        public void ServerName_ConvertsPascalCaseToUpperSnake(object value, string expected)
        {
            string actual = value switch
            {
                NextAction action => WorkbenchServerMapper.ServerName(action),
                FailReason reason => WorkbenchServerMapper.ServerName(reason),
                RelicStatus status => WorkbenchServerMapper.ServerName(status),
                BoxSource source => WorkbenchServerMapper.ServerName(source),
                _ => throw new ArgumentException(nameof(value))
            };
            Assert.AreEqual(expected, actual);
        }

        [TestCase("2")]
        [TestCase("hard")]
        [TestCase("Hard")]
        [TestCase("VERY_EASY")]
        [TestCase("")]
        public void TryParse_RejectsAnythingButExactServerName(string serverName)
        {
            Assert.IsFalse(WorkbenchServerMapper.TryParse(serverName, out MinigameDifficulty _));
        }

        [Test]
        public void TryParse_AcceptsServerName()
        {
            Assert.IsTrue(WorkbenchServerMapper.TryParse("HARD", out MinigameDifficulty difficulty));
            Assert.AreEqual(MinigameDifficulty.Hard, difficulty);
        }

        // ---------- 시각 ----------

        [Test]
        public void ParseServerTime_OffsetlessValue_IsKoreanTime()
        {
            DateTime utc = WorkbenchServerMapper.ParseServerTime(Data("{'at':'2026-11-27T09:00:00'}")["at"]);
            Assert.AreEqual(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc), utc);
            Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
        }

        [Test]
        public void ParseServerTime_ValueWithOffset_KeepsInstant()
        {
            DateTime utc = WorkbenchServerMapper.ParseServerTime(Data("{'at':'2026-11-27T09:00:00+00:00'}")["at"]);
            Assert.AreEqual(new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc), utc);
        }

        [Test]
        public void ParseServerTime_NotADate_Throws()
        {
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.ParseServerTime(new JValue("어제")));
        }

        // ---------- 작업대 ----------

        [Test]
        public void Snapshot_MapsSlotsSpiritsAndIgnoresCardFieldsAndBuildings()
        {
            WorkbenchSnapshot snapshot = WorkbenchServerMapper.Snapshot(Data(@"{
                'capacity':3,
                'slots':[{'relicId':3,'era':'THREE_KINGDOMS','revealLevel':'FORM','poeticAlias':'별칭','currentPhase':'RECALL'}],
                'spirits':[{'relicId':9,'era':'JOSEON','revealLevel':'NAMED','poeticAlias':'별칭','realName':'이름',
                            'spiritName':'돌이','careCount':4,'clarity':'RESEALED'},
                           {'relicId':2,'era':'THREE_KINGDOMS','revealLevel':'NAMED','poeticAlias':'별칭','realName':'이름',
                            'spiritName':'연이','careCount':0,'clarity':'CLEAR','clarityChangesAt':'2026-12-01T10:00:00'}],
                'lockedCount':6,
                'buildings':[{'buildingId':1}]}"));

            Assert.AreEqual(3, snapshot.Capacity);
            Assert.AreEqual(6, snapshot.LockedCount);
            Assert.AreEqual(1, snapshot.Slots.Count);
            Assert.AreEqual(3, snapshot.Slots[0].RelicId);
            Assert.AreEqual(RevealLevel.Form, snapshot.Slots[0].RevealLevel);
            Assert.AreEqual(Phase.Recall, snapshot.Slots[0].CurrentPhase);
            // 서버가 정한 돌봐야 할 순서를 그대로 둔다.
            CollectionAssert.AreEqual(new[] { 9, 2 }, snapshot.Spirits.Select(s => s.RelicId));
            Assert.AreEqual("돌이", snapshot.Spirits[0].SpiritName);
            Assert.AreEqual(Clarity.Resealed, snapshot.Spirits[0].Clarity);
            Assert.AreEqual(4, snapshot.Spirits[0].CareCount);
        }

        [Test]
        public void Snapshot_SpiritWithoutCareCount_Throws()
        {
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.Snapshot(Data(@"{
                'capacity':3,'slots':[],'lockedCount':0,
                'spirits':[{'relicId':9,'spiritName':'돌이','clarity':'CLEAR'}]}")));
        }

        [Test]
        public void Snapshot_ZeroCapacity_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.Snapshot(Data(
                "{'capacity':0,'slots':[],'spirits':[],'lockedCount':0}")));
        }

        // ---------- 유물 상세 ----------

        [Test]
        public void Detail_InProgress_DropsSpiritValues()
        {
            // 서버는 최초 정화 중에도 clarity를 계산해 보낼 수 있다. 공개 전이므로 버린다.
            RelicDetail detail = WorkbenchServerMapper.Detail(Data(@"{
                'relic':{'relicId':3,'era':'THREE_KINGDOMS','revealLevel':'FORM','poeticAlias':'별칭'},
                'status':'IN_PROGRESS','currentPhase':'RECALL','clarity':'CLEAR',
                'displayed':false,'hidden':false,
                'nextPlay':{'entryRoute':'FIRST','phase':'RECALL','remainingPhases':['RECALL','GUARD'],'difficulties':['NORMAL']}}"));

            Assert.AreEqual(3, detail.RelicId);
            Assert.AreEqual(RelicStatus.InProgress, detail.Status);
            Assert.IsNull(detail.SpiritName);
            Assert.IsNull(detail.Clarity);
            Assert.IsNull(detail.LastPurifiedAt);
            Assert.IsNull(detail.ClarityChangesAt);
            Assert.AreEqual(0, detail.CareCount);
            Assert.AreEqual(EntryRoute.First, detail.NextPlay.Route);
            Assert.AreEqual(Phase.Recall, detail.NextPlay.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, detail.NextPlay.RemainingPhases);
            Assert.IsFalse(detail.NextPlay.DifficultySelectable);
            Assert.IsNull(detail.NextPlay.Clarity);
        }

        [Test]
        public void Detail_FadedSpirit_MapsTimesAndCarePlan()
        {
            RelicDetail detail = WorkbenchServerMapper.Detail(Data(@"{
                'relic':{'relicId':8,'era':'JOSEON','revealLevel':'NAMED','poeticAlias':'별칭','realName':'이름','spiritName':'솔이'},
                'status':'COMPLETED','careCount':5,'clarity':'FADED',
                'lastPurifiedAt':'2026-11-20T21:30:00','clarityChangesAt':'2026-11-27T21:30:00','resealAt':'2026-11-27T21:30:00',
                'displayed':true,'hidden':false,
                'nextPlay':{'entryRoute':'CARE','phase':'RECALL','remainingPhases':['RECALL'],'difficulties':['NORMAL','HARD']}}"));

            Assert.AreEqual("솔이", detail.SpiritName);
            Assert.AreEqual(Clarity.Faded, detail.Clarity);
            Assert.AreEqual(5, detail.CareCount);
            Assert.AreEqual(new DateTime(2026, 11, 20, 12, 30, 0, DateTimeKind.Utc), detail.LastPurifiedAt);
            Assert.AreEqual(new DateTime(2026, 11, 27, 12, 30, 0, DateTimeKind.Utc), detail.ClarityChangesAt);
            Assert.AreEqual(EntryRoute.Care, detail.NextPlay.Route);
            Assert.AreEqual(Clarity.Faded, detail.NextPlay.Clarity);
            CollectionAssert.AreEqual(new[] { MinigameDifficulty.Normal, MinigameDifficulty.Hard },
                detail.NextPlay.AllowedDifficulties);
        }

        [Test]
        public void Detail_ResealedSpirit_HasNoChangeTime()
        {
            RelicDetail detail = WorkbenchServerMapper.Detail(Data(@"{
                'relic':{'relicId':9,'spiritName':'돌이'},
                'status':'COMPLETED','careCount':0,'clarity':'RESEALED','lastPurifiedAt':'2026-11-01T10:00:00',
                'nextPlay':{'entryRoute':'CARE','phase':'UNSEAL','remainingPhases':['UNSEAL','RECALL'],'difficulties':['NORMAL','HARD']}}"));

            Assert.AreEqual(Clarity.Resealed, detail.Clarity);
            Assert.IsNull(detail.ClarityChangesAt);
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall }, detail.NextPlay.RemainingPhases);
        }

        [Test]
        public void Detail_ClearSpiritWithoutChangeTime_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.Detail(Data(@"{
                'relic':{'relicId':2,'spiritName':'연이'},
                'status':'COMPLETED','careCount':0,'clarity':'CLEAR','lastPurifiedAt':'2026-11-25T10:00:00',
                'nextPlay':{'entryRoute':'CARE','phase':'GUARD','remainingPhases':['GUARD'],'difficulties':['EASY','NORMAL','HARD']}}")));
        }

        [Test]
        public void Detail_PhaseNotFirstOfRemaining_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.Detail(Data(@"{
                'relic':{'relicId':3},'status':'IN_PROGRESS',
                'nextPlay':{'entryRoute':'FIRST','phase':'GUARD','remainingPhases':['RECALL','GUARD'],'difficulties':['NORMAL']}}")));
        }

        // ---------- 상자 ----------

        [Test]
        public void BoxOutcome_New_HasNoRewards()
        {
            BoxOpenOutcome outcome = WorkbenchServerMapper.BoxOutcome(Data(@"{
                'result':'NEW','relic':{'relicId':5,'era':'GORYEO','revealLevel':'SILHOUETTE','poeticAlias':'별칭'},
                'box':{'freeAvailable':false}}"));

            Assert.AreEqual(AcquireResult.New, outcome.Result);
            Assert.AreEqual(5, outcome.RelicId);
            Assert.AreEqual(0, outcome.JeongseongGained);
            Assert.AreEqual(0, outcome.CrystalGained);
            Assert.AreEqual(0, outcome.JeongseongSpent);
        }

        [Test]
        public void BoxOutcome_DuplicatePurchase_MapsRewardsAndSpent()
        {
            BoxOpenOutcome outcome = WorkbenchServerMapper.BoxOutcome(Data(@"{
                'result':'DUPLICATE','relic':{'relicId':2},
                'duplicateReward':{'era':'THREE_KINGDOMS','eraCrystal':1,'jeongseong':30},
                'spent':{'jeongseong':100}}"));

            Assert.AreEqual(AcquireResult.Duplicate, outcome.Result);
            Assert.AreEqual(30, outcome.JeongseongGained);
            Assert.AreEqual(1, outcome.CrystalGained);
            Assert.AreEqual(100, outcome.JeongseongSpent);
        }

        // ---------- 정화 시작 ----------

        private const string StartJson = @"{
            'sessionToken':'abc','expiresAt':'2026-11-27T10:30:00','entryRoute':'FIRST','phase':'UNSEAL','difficulty':'NORMAL',
            'sealMedium':'ROOT',
            'board':{'seed':9007199254740993,'width':7,'height':8,'threatCount':6,'reinforcedOnShape':2,'reinforcedOnField':1,
                     'helperCount':1,'bonusJeongseongCount':2,'shapeCount':3,'shapeCellTotal':12,'tapBudget':19,'scoutLimit':3},
            'rewardMultiplier':1.2,'perfectBonusEligible':false}";

        [Test]
        public void Session_MapsBoardAndKeepsRequestedRelic()
        {
            PurificationSession session = WorkbenchServerMapper.Session(Data(StartJson), 3);

            Assert.AreEqual("abc", session.SessionToken);
            Assert.AreEqual(3, session.RelicId);
            Assert.AreEqual(EntryRoute.First, session.Route);
            Assert.AreEqual(Phase.Unseal, session.Phase);
            Assert.AreEqual(MinigameDifficulty.Normal, session.Difficulty);
            Assert.AreEqual(SealMedium.Root, session.SealMedium);
            // 2^53보다 큰 시드도 잃지 않는다.
            Assert.AreEqual(9007199254740993L, session.Seed);
            Assert.IsTrue(session.Board.HasValue);
            Assert.AreEqual(7, session.Board.Value.Width);
            Assert.AreEqual(19, session.Board.Value.DestructiveToolLimit);
            Assert.AreEqual(session.Board, session.ToSetup().Board);
        }

        [Test]
        public void Session_UnknownSealMedium_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() =>
                WorkbenchServerMapper.Session(Data(StartJson.Replace("'ROOT'", "'MOSS'")), 3));
        }

        [Test]
        public void Session_ZeroTapBudget_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() =>
                WorkbenchServerMapper.Session(Data(StartJson.Replace("'tapBudget':19", "'tapBudget':0")), 3));
        }

        [Test]
        public void Session_BoardThatCannotBeGenerated_ThrowsFormatException()
        {
            // 범위 검사는 통과하지만 1×1 칸에 형상 12칸을 놓을 수 없다.
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.Session(Data(StartJson
                .Replace("'width':7", "'width':1").Replace("'height':8", "'height':1")), 3));
        }

        // ---------- 정산 ----------

        [Test]
        public void Settlement_FirstNextPhase_UsesServerPhases()
        {
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'SUCCESS','perfect':false,'rewards':{'jeongseong':10},'relic':{'relicId':3},
                'status':'IN_PROGRESS','revealed':'FORM','clarity':'CLEAR',
                'next':{'action':'NEXT_PHASE','phase':'RECALL','remainingPhases':['RECALL','GUARD']}}"), FirstSession());

            Assert.AreEqual(PlayResult.Success, result.Result);
            Assert.AreEqual(NextAction.NextPhase, result.Next);
            Assert.AreEqual(RevealLevel.Form, result.Revealed);
            Assert.IsFalse(result.CompletedNow);
            Assert.AreEqual(EntryRoute.First, result.NextPlan.Route);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, result.NextPlan.RemainingPhases);
            Assert.IsNull(result.NextPlan.Clarity);
        }

        [Test]
        public void Settlement_FirstLastPhase_CompletesNow()
        {
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'SUCCESS','perfect':false,'relic':{'relicId':3,'spiritName':'토리'},
                'status':'COMPLETED','careCount':0,'clarity':'CLEAR',
                'lastPurifiedAt':'2026-11-27T10:00:00','clarityChangesAt':'2026-12-04T10:00:00',
                'next':{'action':'DONE'}}"), FirstSession(Phase.Guard));

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.IsNull(result.NextPlan);
            Assert.IsTrue(result.CompletedNow);
            // 서버는 오염 관리 성공에 새 공개 단계를 보내지 않는다(jamkkaebi-server PR #30).
            Assert.IsNull(result.Revealed);
        }

        [Test]
        public void Settlement_CareDone_DoesNotCompleteNow()
        {
            // 관리 플레이는 이미 정령이라 상태가 COMPLETED여도 이번 판으로 완료한 것이 아니다.
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'SUCCESS','perfect':true,'perfectStreak':2,'relic':{'relicId':2},
                'status':'COMPLETED','careCount':3,'clarity':'CLEAR','next':{'action':'DONE'}}"),
                CareSession(Phase.Guard, MinigameDifficulty.Easy));

            Assert.AreEqual(NextAction.Done, result.Next);
            Assert.IsFalse(result.CompletedNow);
            Assert.IsNull(result.Revealed);
        }

        [Test]
        public void Settlement_CareContinueRestore_CarriesReachedClarity()
        {
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'SUCCESS','perfect':false,'relic':{'relicId':2},'status':'COMPLETED','careCount':1,
                'clarity':'FADED','next':{'action':'CONTINUE_RESTORE','phase':'RECALL','remainingPhases':['RECALL']}}"),
                CareSession(Phase.Unseal, MinigameDifficulty.Normal));

            Assert.AreEqual(NextAction.ContinueRestore, result.Next);
            Assert.AreEqual(EntryRoute.Care, result.NextPlan.Route);
            Assert.AreEqual(Phase.Recall, result.NextPlan.Phase);
            Assert.AreEqual(Clarity.Faded, result.NextPlan.Clarity);
        }

        [Test]
        public void Settlement_FirstFailure_RetriesSamePhase()
        {
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'FAILURE','perfect':false,'relic':{'relicId':3},'status':'IN_PROGRESS','clarity':'CLEAR',
                'next':{'action':'RETRY'}}"), FirstSession(Phase.Recall));

            Assert.AreEqual(NextAction.Retry, result.Next);
            Assert.AreEqual(EntryRoute.First, result.NextPlan.Route);
            CollectionAssert.AreEqual(new[] { Phase.Recall, Phase.Guard }, result.NextPlan.RemainingPhases);
            Assert.IsNull(result.NextPlan.Clarity);
        }

        [Test]
        public void Settlement_CareFailure_RetriesWithCurrentClarity()
        {
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'FAILURE','perfect':false,'relic':{'relicId':2},'status':'COMPLETED','careCount':0,
                'clarity':'RESEALED','next':{'action':'RETRY'}}"), CareSession(Phase.Unseal, MinigameDifficulty.Hard));

            Assert.AreEqual(Phase.Unseal, result.NextPlan.Phase);
            Assert.AreEqual(Clarity.Resealed, result.NextPlan.Clarity);
            CollectionAssert.AreEqual(new[] { MinigameDifficulty.Hard }, result.NextPlan.AllowedDifficulties);
        }

        [Test]
        public void Settlement_CareFailureAfterResealDuringPlay_RetriesFromUnseal()
        {
            // 기억 회복(흐려짐) 도중 재봉인 경계를 넘어 실패하면 다음 판은 봉인 해제부터다.
            SettlementResult result = WorkbenchServerMapper.Settlement(Data(@"{
                'result':'FAILURE','perfect':false,'relic':{'relicId':2},'status':'COMPLETED','careCount':0,
                'clarity':'RESEALED','next':{'action':'RETRY'}}"), CareSession(Phase.Recall, MinigameDifficulty.Normal));

            Assert.AreEqual(Phase.Unseal, result.NextPlan.Phase);
            CollectionAssert.AreEqual(new[] { Phase.Unseal, Phase.Recall }, result.NextPlan.RemainingPhases);
            Assert.AreEqual(Clarity.Resealed, result.NextPlan.Clarity);
        }

        [Test]
        public void Settlement_SuccessWithRetry_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => WorkbenchServerMapper.Settlement(Data(@"{
                'result':'SUCCESS','relic':{'relicId':3},'status':'IN_PROGRESS','next':{'action':'RETRY'}}"),
                FirstSession()));
        }

        // ---------- 이름 ----------

        [Test]
        public void SpiritName_ReadsResolvedNameFromCard()
        {
            Assert.AreEqual("새이름", WorkbenchServerMapper.SpiritName(Data(
                "{'relic':{'relicId':2,'revealLevel':'NAMED','spiritName':'새이름'}}")));
        }

        // ---------- 요청 본문 ----------

        [Test]
        public void ReportBody_Failure_IncludesFailReason()
        {
            JObject body = WorkbenchServerMapper.ReportBody(
                new PlayReport(PlayResult.Failure, FailReason.DamageFull, 12, 1, 100, 0));

            Assert.AreEqual("FAILURE", (string)body["result"]);
            Assert.AreEqual("DAMAGE_FULL", (string)body["failReason"]);
            Assert.AreEqual(12, (int)body["tapsUsed"]);
            Assert.AreEqual(1, (int)body["scoutsUsed"]);
            Assert.AreEqual(100, (int)body["damage"]);
            Assert.AreEqual(0, (int)body["bonusTiles"]["jeongseong"]);
        }

        [Test]
        public void ReportBody_Success_OmitsFailReason()
        {
            JObject body = WorkbenchServerMapper.ReportBody(new PlayReport(PlayResult.Success, null, 8, 0, 20, 2));
            Assert.IsNull(body["failReason"]);
            Assert.AreEqual(2, (int)body["bonusTiles"]["jeongseong"]);
        }

        [Test]
        public void StartBody_OmitsDifficultyWhenNotChosen()
        {
            Assert.IsNull(WorkbenchServerMapper.StartBody(3, null)["difficulty"]);
            Assert.AreEqual("HARD", (string)WorkbenchServerMapper.StartBody(3, MinigameDifficulty.Hard)["difficulty"]);
        }

        [Test]
        public void RenameBody_NullResetsToDefault()
        {
            Assert.AreEqual(JTokenType.Null, WorkbenchServerMapper.RenameBody(null)["name"].Type);
            Assert.AreEqual("솔이", (string)WorkbenchServerMapper.RenameBody("솔이")["name"]);
        }

        [Test]
        public void BoxOpenBody_UsesServerSourceName()
        {
            JObject body = WorkbenchServerMapper.BoxOpenBody(BoxSource.Gift, "3f2504e0-4f89-11d3-9a0c-0305e82c3301");
            Assert.AreEqual("GIFT", (string)body["source"]);
            Assert.AreEqual("3f2504e0-4f89-11d3-9a0c-0305e82c3301", (string)body["requestId"]);
        }
    }
}
