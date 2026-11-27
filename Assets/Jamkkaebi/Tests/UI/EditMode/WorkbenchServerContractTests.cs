using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Workbench;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 로컬 Spring 검증 서버(Tools/serve-workbench-contract.sh)와 실제 HTTP로 작업대 계약을 확인합니다.
    /// 서버 세션 파일이 없으면 생략(Ignore)되며, 생략은 서버 검증을 통과한 것이 아닙니다.
    /// </summary>
    /// <remarks>
    /// 세션 파일은 환경 변수 JAMKKAEBI_WORKBENCH_CONTRACT, 없으면 Temp/ServerContract/workbench-session.json입니다.
    /// 검증 서버는 실행마다 새 H2라 이 테스트는 서버를 새로 띄운 뒤 한 번 돌리는 것을 전제로 합니다.
    /// </remarks>
    public sealed class WorkbenchServerContractTests
    {
        private const string ConfigVariable = "JAMKKAEBI_WORKBENCH_CONTRACT";

        private static JObject LoadConfig()
        {
            string path = Environment.GetEnvironmentVariable(ConfigVariable);
            if (string.IsNullOrEmpty(path)) path = ServerConnection.DevSessionPath;
            if (!File.Exists(path)) Assert.Ignore("로컬 작업대 검증 서버가 설정되지 않았습니다(Tools/serve-workbench-contract.sh).");
            return JObject.Parse(File.ReadAllText(path));
        }

        private sealed class Box<T>
        {
            public T Value;
            public string Error;
        }

        private static IEnumerator Await<T>(Func<Action<T>, Action<string>, IEnumerator> call, Box<T> box)
        {
            bool done = false;
            yield return call(value => { box.Value = value; done = true; }, error => { box.Error = error; done = true; });
            Assert.IsTrue(done, "콜백이 오지 않았습니다.");
        }

        private static T Ok<T>(Box<T> box, string step)
        {
            Assert.IsNull(box.Error, step + " 실패: " + box.Error);
            return box.Value;
        }

        // 서버의 최소 플레이 시간(하네스는 1초) 판정을 넘긴다.
        private static IEnumerator WaitMinimumPlay()
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(1.5)) yield return null;
        }

        [UnityTest]
        public IEnumerator FirstPurification_BoxStartReport_FollowsServerContract()
        {
            JObject config = LoadConfig();
            var backend = new ServerWorkbenchBackend(new JamkkaebiApiClient((string)config["baseUrl"], (string)config["boxAccessToken"]));

            var home = new Box<WorkbenchSnapshot>();
            yield return Await<WorkbenchSnapshot>(backend.GetWorkbench, home);
            WorkbenchSnapshot before = Ok(home, "작업대 조회");
            // 유물이 없는 사용자라 첫 뽑기는 항상 새 유물이다.
            Assert.AreEqual(0, before.Slots.Count);
            Assert.AreEqual(0, before.Spirits.Count);

            // 같은 requestId로 다시 보내면 다시 뽑지 않고 같은 결과를 돌려준다.
            string requestId = Guid.NewGuid().ToString();
            var opened = new Box<BoxOpenOutcome>();
            yield return Await<BoxOpenOutcome>((done, fail) => backend.OpenBox(BoxSource.Free, requestId, done, fail), opened);
            BoxOpenOutcome outcome = Ok(opened, "상자 열기");
            Assert.AreEqual(AcquireResult.New, outcome.Result);

            var replay = new Box<BoxOpenOutcome>();
            yield return Await<BoxOpenOutcome>((done, fail) => backend.OpenBox(BoxSource.Free, requestId, done, fail), replay);
            Assert.AreEqual(outcome.RelicId, Ok(replay, "같은 번호로 상자 다시 열기").RelicId);

            var after = new Box<WorkbenchSnapshot>();
            yield return Await<WorkbenchSnapshot>(backend.GetWorkbench, after);
            Assert.AreEqual(1, Ok(after, "작업대 다시 조회").Slots.Count, "같은 번호는 한 번만 뽑는다.");

            int relicId = outcome.RelicId;
            var detail = new Box<RelicDetail>();
            yield return Await<RelicDetail>((done, fail) => backend.GetRelicDetail(relicId, done, fail), detail);
            RelicDetail relic = Ok(detail, "유물 상세");
            Assert.AreEqual(RelicStatus.InProgress, relic.Status);
            Assert.AreEqual(EntryRoute.First, relic.NextPlay.Route);
            Assert.AreEqual(Phase.Unseal, relic.NextPlay.Phase);

            var started = new Box<PurificationSession>();
            yield return Await<PurificationSession>((done, fail) => backend.StartPurification(relicId, null, done, fail), started);
            PurificationSession session = Ok(started, "정화 시작");
            Assert.AreEqual(Phase.Unseal, session.Phase);
            Assert.AreEqual(MinigameDifficulty.Normal, session.Difficulty);
            Assert.IsTrue(session.Board.HasValue);
            Assert.DoesNotThrow(() => Scripts.Gameplay.Minigame.Core.MinigameGridGenerator.Generate(session.Board.Value, session.Seed),
                "서버 보드 값으로 클라이언트가 판을 만들 수 있어야 한다.");

            yield return WaitMinimumPlay();
            var report = new PlayReport(PlayResult.Success, null, 5, 0, 10, 0);
            var settled = new Box<SettlementResult>();
            yield return Await<SettlementResult>((done, fail) => backend.ReportPurification(session.SessionToken, report, done, fail), settled);
            SettlementResult result = Ok(settled, "결과 보고");
            Assert.AreEqual(NextAction.NextPhase, result.Next);
            Assert.AreEqual(Phase.Recall, result.NextPlan.Phase);
            Assert.AreEqual(RevealLevel.Form, result.Revealed);
            Assert.IsFalse(result.CompletedNow);

            // 같은 토큰·같은 결과를 다시 보내면 다시 정산하지 않고 같은 결과를 준다(재전송 버튼).
            var resent = new Box<SettlementResult>();
            yield return Await<SettlementResult>((done, fail) => backend.ReportPurification(session.SessionToken, report, done, fail), resent);
            Assert.AreEqual(NextAction.NextPhase, Ok(resent, "결과 다시 보고").Next);

            var progressed = new Box<RelicDetail>();
            yield return Await<RelicDetail>((done, fail) => backend.GetRelicDetail(relicId, done, fail), progressed);
            Assert.AreEqual(Phase.Recall, Ok(progressed, "진행 뒤 상세").NextPlay.Phase, "한 번만 정산해야 한다.");
        }

        [UnityTest]
        public IEnumerator Spirit_RenameAndCareFailure_FollowServerContract()
        {
            JObject config = LoadConfig();
            var backend = new ServerWorkbenchBackend(new JamkkaebiApiClient((string)config["baseUrl"], (string)config["spiritAccessToken"]));
            int relicId = (int)config["spiritRelicId"];

            var detail = new Box<RelicDetail>();
            yield return Await<RelicDetail>((done, fail) => backend.GetRelicDetail(relicId, done, fail), detail);
            RelicDetail spirit = Ok(detail, "정령 상세");
            Assert.AreEqual(RelicStatus.Completed, spirit.Status);
            Assert.AreEqual(Clarity.Clear, spirit.Clarity);
            Assert.IsNotNull(spirit.LastPurifiedAt);
            Assert.IsNotNull(spirit.ClarityChangesAt);
            Assert.Greater(spirit.ClarityChangesAt.Value, DateTime.UtcNow, "선명도가 바뀌는 시각은 미래여야 한다(시간대 변환 확인).");
            Assert.Less(spirit.ClarityChangesAt.Value, DateTime.UtcNow + TimeSpan.FromDays(7));
            string defaultName = spirit.SpiritName;

            var renamed = new Box<string>();
            yield return Await<string>((done, fail) => backend.RenameSpirit(relicId, " 검증이 ", done, fail), renamed);
            Assert.AreEqual("검증이", Ok(renamed, "이름 바꾸기"));

            var reset = new Box<string>();
            yield return Await<string>((done, fail) => backend.RenameSpirit(relicId, "", done, fail), reset);
            Assert.AreEqual(defaultName, Ok(reset, "기본 이름으로 되돌리기"));

            // 선명한 정령은 어려움을 고를 수 있다. 실패하면 같은 페이즈 재시도이고 돌봄 횟수는 그대로다.
            Assert.IsTrue(spirit.NextPlay.Allows(MinigameDifficulty.Hard));
            var started = new Box<PurificationSession>();
            yield return Await<PurificationSession>((done, fail) =>
                backend.StartPurification(relicId, MinigameDifficulty.Hard, done, fail), started);
            PurificationSession session = Ok(started, "관리 플레이 시작");
            Assert.AreEqual(EntryRoute.Care, session.Route);
            Assert.AreEqual(MinigameDifficulty.Hard, session.Difficulty);

            yield return WaitMinimumPlay();
            var settled = new Box<SettlementResult>();
            yield return Await<SettlementResult>((done, fail) => backend.ReportPurification(session.SessionToken,
                new PlayReport(PlayResult.Failure, FailReason.GaveUp, 1, 0, 0, 0), done, fail), settled);
            SettlementResult result = Ok(settled, "관리 플레이 실패 보고");
            Assert.AreEqual(NextAction.Retry, result.Next);
            Assert.AreEqual(session.Phase, result.NextPlan.Phase);

            var after = new Box<RelicDetail>();
            yield return Await<RelicDetail>((done, fail) => backend.GetRelicDetail(relicId, done, fail), after);
            Assert.AreEqual(spirit.CareCount, Ok(after, "실패 뒤 상세").CareCount);
        }

        [UnityTest]
        public IEnumerator Errors_ArePassedThroughWithoutFallback()
        {
            JObject config = LoadConfig();

            // 토큰이 없으면 요청 전에 로그인 필요로 실패한다(모의로 바꾸지 않는다).
            var anonymous = new ServerWorkbenchBackend(new JamkkaebiApiClient((string)config["baseUrl"]));
            var home = new Box<WorkbenchSnapshot>();
            yield return Await<WorkbenchSnapshot>(anonymous.GetWorkbench, home);
            Assert.IsNotNull(home.Error);

            // 가지지 않은 유물(정령 사용자는 5번만 가짐)은 서버 오류 메시지로 실패한다.
            var backend = new ServerWorkbenchBackend(new JamkkaebiApiClient((string)config["baseUrl"], (string)config["spiritAccessToken"]));
            var missing = new Box<RelicDetail>();
            yield return Await<RelicDetail>((done, fail) => backend.GetRelicDetail(9, done, fail), missing);
            Assert.IsNotNull(missing.Error);
            Assert.IsNull(missing.Value);
        }
    }
}
