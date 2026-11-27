using System;
using System.Collections;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Workbench;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// ServerWorkbenchBackend의 요청 경로·본문·실패 처리를 전송 경계를 바꿔 끼워 검사합니다.
    /// 실제 HTTP가 아니므로 서버 검증이 아닙니다(실제 서버는 WorkbenchServerContractTests).
    /// </summary>
    public sealed class ServerWorkbenchBackendTests
    {
        private sealed class Sent
        {
            public string Method;
            public string Path;
            public JObject Body;
            public bool Authenticated;
        }

        private readonly List<Sent> _sent = new List<Sent>();
        private readonly Queue<ServerApiResult> _responses = new Queue<ServerApiResult>();
        private readonly List<string> _logs = new List<string>();
        private ServerWorkbenchBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _responses.Clear();
            _logs.Clear();
            JamkkaebiApiClient client = new JamkkaebiApiClient("http://127.0.0.1:1", "access", Transport);
            _backend = new ServerWorkbenchBackend(client, null, _logs.Add);
        }

        private IEnumerator Transport(string method, string path, JObject body, bool authenticated,
            Action<ServerApiResult> completed)
        {
            _sent.Add(new Sent { Method = method, Path = path, Body = body, Authenticated = authenticated });
            completed(_responses.Dequeue());
            yield break;
        }

        private void Respond(string dataJson) =>
            _responses.Enqueue(ServerApiResult.Parse(
                "{\"success\":true,\"code\":\"S000\",\"message\":\"성공\",\"data\":" + dataJson.Replace('\'', '"') + "}", 200));

        private void RespondError(string code, string message, long status) =>
            _responses.Enqueue(new ServerApiResult(false, code, message, status: status));

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator inner) Drain(inner);
            }
        }

        private static (T value, string error, int calls) Run<T>(Func<Action<T>, Action<string>, IEnumerator> call)
        {
            T value = default;
            string error = null;
            int calls = 0;
            Drain(call(v => { value = v; calls++; }, e => { error = e; calls++; }));
            return (value, error, calls);
        }

        private const string StartJson = @"{
            'sessionToken':'tok/en','expiresAt':'2026-11-27T10:30:00','entryRoute':'FIRST','phase':'UNSEAL','difficulty':'NORMAL',
            'sealMedium':'ROOT',
            'board':{'seed':42,'width':7,'height':8,'threatCount':6,'reinforcedOnShape':2,'reinforcedOnField':1,
                     'helperCount':1,'bonusJeongseongCount':2,'shapeCount':3,'shapeCellTotal':12,'tapBudget':19,'scoutLimit':3},
            'rewardMultiplier':1.2,'perfectBonusEligible':false}";

        private const string NextPhaseJson = @"{
            'result':'SUCCESS','perfect':false,'relic':{'relicId':3},'status':'IN_PROGRESS','revealed':'FORM','clarity':'CLEAR',
            'next':{'action':'NEXT_PHASE','phase':'RECALL','remainingPhases':['RECALL','GUARD']}}";

        private static readonly PlayReport Success = new PlayReport(PlayResult.Success, null, 8, 1, 20, 1);

        [Test]
        public void GetWorkbench_SendsAuthenticatedGet()
        {
            Respond("{'capacity':3,'slots':[],'spirits':[],'lockedCount':9}");

            var (snapshot, error, calls) = Run<WorkbenchSnapshot>((done, fail) => _backend.GetWorkbench(done, fail));

            Assert.IsNull(error);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(9, snapshot.LockedCount);
            Assert.AreEqual("GET", _sent[0].Method);
            Assert.AreEqual("/api/workbench", _sent[0].Path);
            Assert.IsNull(_sent[0].Body);
            Assert.IsTrue(_sent[0].Authenticated);
        }

        [Test]
        public void OpenBox_NetworkFailure_ReportsOnceWithoutResending()
        {
            RespondError("NETWORK_ERROR", "서버에 연결할 수 없어요.", 0);

            var (_, error, calls) = Run<BoxOpenOutcome>((done, fail) =>
                _backend.OpenBox(BoxSource.Free, "3f2504e0-4f89-11d3-9a0c-0305e82c3301", done, fail));

            Assert.AreEqual("서버에 연결할 수 없어요.", error);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, _sent.Count, "변경 요청은 네트워크 실패로 다시 보내지 않는다.");
            Assert.AreEqual("POST", _sent[0].Method);
            Assert.AreEqual("/api/box/open", _sent[0].Path);
            Assert.AreEqual("FREE", (string)_sent[0].Body["source"]);
        }

        [Test]
        public void OpenBox_RuleFailure_PassesServerMessage()
        {
            RespondError("B002", "작업대가 가득 찼습니다.", 409);

            var (_, error, _) = Run<BoxOpenOutcome>((done, fail) =>
                _backend.OpenBox(BoxSource.Free, "3f2504e0-4f89-11d3-9a0c-0305e82c3301", done, fail));

            Assert.AreEqual("작업대가 가득 찼습니다.", error);
            StringAssert.Contains("B002", _logs[0]);
        }

        [Test]
        public void StartThenReport_UsesStartedSessionAndEscapesToken()
        {
            Respond(StartJson);
            var (session, startError, _) = Run<PurificationSession>((done, fail) =>
                _backend.StartPurification(3, null, done, fail));
            Assert.IsNull(startError);
            Assert.AreEqual(3, (int)_sent[0].Body["relicId"]);
            Assert.IsNull(_sent[0].Body["difficulty"]);

            Respond(NextPhaseJson);
            var (settlement, error, _) = Run<SettlementResult>((done, fail) =>
                _backend.ReportPurification(session.SessionToken, Success, done, fail));

            Assert.IsNull(error);
            Assert.AreEqual(NextAction.NextPhase, settlement.Next);
            Assert.AreEqual("/api/purifications/tok%2Fen/result", _sent[1].Path);
            Assert.AreEqual("SUCCESS", (string)_sent[1].Body["result"]);
        }

        [Test]
        public void Report_NetworkFailure_CanBeResentBySameToken()
        {
            Respond(StartJson);
            var (session, _, _) = Run<PurificationSession>((done, fail) => _backend.StartPurification(3, null, done, fail));

            RespondError("NETWORK_ERROR", "서버에 연결할 수 없어요.", 0);
            var (_, firstError, _) = Run<SettlementResult>((done, fail) =>
                _backend.ReportPurification(session.SessionToken, Success, done, fail));
            Assert.IsNotNull(firstError);
            Assert.AreEqual(2, _sent.Count, "실패해도 스스로 다시 보내지 않는다.");

            // 사용자가 재전송을 누르면 같은 토큰·같은 결과로 한 번 더 보낸다.
            Respond(NextPhaseJson);
            var (settlement, secondError, _) = Run<SettlementResult>((done, fail) =>
                _backend.ReportPurification(session.SessionToken, Success, done, fail));
            Assert.IsNull(secondError);
            Assert.IsNotNull(settlement);
            Assert.AreEqual(_sent[1].Body.ToString(), _sent[2].Body.ToString());
        }

        [Test]
        public void Report_AfterSuccess_CanBeResentBySameToken()
        {
            Respond(StartJson);
            var (session, _, _) = Run<PurificationSession>((done, fail) => _backend.StartPurification(3, null, done, fail));
            Respond(NextPhaseJson);
            Run<SettlementResult>((done, fail) => _backend.ReportPurification(session.SessionToken, Success, done, fail));

            // 정산 응답을 화면이 버린 경우: 같은 토큰으로 다시 보내면 서버가 같은 정산을 돌려준다.
            Respond(NextPhaseJson);
            var (settlement, error, _) = Run<SettlementResult>((done, fail) =>
                _backend.ReportPurification(session.SessionToken, Success, done, fail));

            Assert.IsNull(error);
            Assert.AreEqual(NextAction.NextPhase, settlement.Next);
            Assert.AreEqual(3, _sent.Count);
        }

        [Test]
        public void Report_UnknownToken_FailsWithoutRequest()
        {
            var (_, error, calls) = Run<SettlementResult>((done, fail) =>
                _backend.ReportPurification("never-started", Success, done, fail));

            Assert.IsNotNull(error);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, _sent.Count);
        }

        [Test]
        public void Report_FailureLog_DoesNotContainSessionToken()
        {
            Respond(StartJson);
            var (session, _, _) = Run<PurificationSession>((done, fail) => _backend.StartPurification(3, null, done, fail));
            RespondError("P002", "정화 세션이 없거나 만료되었습니다.", 404);

            Run<SettlementResult>((done, fail) => _backend.ReportPurification(session.SessionToken, Success, done, fail));

            Assert.AreEqual(1, _logs.Count);
            StringAssert.DoesNotContain("tok/en", _logs[0]);
            StringAssert.DoesNotContain("tok%2Fen", _logs[0]);
            StringAssert.Contains("P002", _logs[0]);
        }

        [Test]
        public void InvalidResponse_FailsWithReadableMessage()
        {
            Respond("{'capacity':'three'}");

            var (_, error, calls) = Run<WorkbenchSnapshot>((done, fail) => _backend.GetWorkbench(done, fail));

            Assert.AreEqual("서버 응답을 읽을 수 없어요.", error);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void RenameSpirit_TooShort_FailsWithoutRequest()
        {
            var (_, error, _) = Run<string>((done, fail) => _backend.RenameSpirit(2, "솔", done, fail));

            Assert.AreEqual($"이름은 {SpiritNameRule.MinLength}자 이상이어야 해요.", error);
            Assert.AreEqual(0, _sent.Count);
        }

        [Test]
        public void RenameSpirit_Blank_SendsNullAndReturnsServerName()
        {
            Respond("{'relic':{'relicId':2,'spiritName':'기본이'}}");

            var (name, error, _) = Run<string>((done, fail) => _backend.RenameSpirit(2, "   ", done, fail));

            Assert.IsNull(error);
            Assert.AreEqual("기본이", name);
            Assert.AreEqual("PATCH", _sent[0].Method);
            Assert.AreEqual("/api/relics/2/spirit-name", _sent[0].Path);
            Assert.AreEqual(JTokenType.Null, _sent[0].Body["name"].Type);
        }

        [Test]
        public void RenameSpirit_TrimsBeforeSending()
        {
            Respond("{'relic':{'relicId':2,'spiritName':'솔이'}}");

            Run<string>((done, fail) => _backend.RenameSpirit(2, "  솔이 ", done, fail));

            Assert.AreEqual("솔이", (string)_sent[0].Body["name"]);
        }
    }
}
