using System;
using System.Collections;
using System.Linq;
using Jamkkaebi.Scripts.UI.Friends;
using Jamkkaebi.Scripts.UI.Shell;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Dev
{
    /// <summary>실제 서버 없이 전송 응답 순서를 제어해 방문 중 변경·실패·재접속 경로를 검사합니다.</summary>
    internal static class FriendVisitFlowVerification
    {
        private const string FirstCode = "H2RX9KTA";
        private const string SecondCode = "C4BJ7XPA";

        /// <summary>모의 UI 검사 이후 호출하며 마지막에는 서버 모드의 친구 목록 화면으로 정리합니다.</summary>
        internal static IEnumerator Run(TabHost host, FriendsPresenter friends, Action<bool, string> check)
        {
            host.SelectTab(3);
            yield return new WaitForSeconds(.4f);
            for (int gift = 0; gift < 2; gift++)
                for (int refreshFailure = 0; refreshFailure < 2; refreshFailure++)
                {
                    var fixture = new Fixture { FailReloadAfterMutation = refreshFailure == 1 };
                    yield return Connect(friends, fixture, check);
                    yield return Visit(friends, FirstCode, check);
                    var scene = friends.VisitSession.Scene;
                    int gets = fixture.ExhibitionGets;
                    if (gift == 0) Press(friends, "VisitReward");
                    else { Press(friends, "VisitGift"); Press(friends, "Confirm"); }
                    yield return WaitFor(() => !friends.ServerBusy, check, "mutation and refresh finish");
                    check(friends.Page == "Visit" && ReferenceEquals(scene, friends.VisitSession.Scene), "successful action preserves visit scene");
                    check(!friends.Buttons[gift == 0 ? "VisitReward" : "VisitGift"].interactable && fixture.Posts == 1,
                        "successful action disables daily button even when refresh fails");
                    check(fixture.ExhibitionGets == gets, "social action never reloads exhibition GET");
                    check(host.GetRoot(3).NavigationLocked, "successful action retains navigation lock");
                    if (refreshFailure == 1) check(Feedback(friends).Contains("갱신하지 못"), "refresh failure remains visible after successful POST");
                    Press(friends, "VisitBack");
                }

            var failed = new Fixture { FailMutation = true };
            yield return Connect(friends, failed, check);
            yield return Visit(friends, FirstCode, check);
            Press(friends, "VisitReward");
            yield return WaitFor(() => !friends.ServerBusy, check, "failed POST finishes");
            check(friends.Page == "Visit" && friends.Buttons["VisitReward"].interactable && failed.Posts == 1,
                "failed reward stays open and permits manual retry without automatic POST");
            check(Feedback(friends).Contains("INJECTED_FAILURE") && host.GetRoot(3).NavigationLocked, "failed action reports error and preserves lock");

            var busy = new Fixture { HoldReloadAfterMutation = true };
            yield return Connect(friends, busy, check);
            yield return Visit(friends, FirstCode, check);
            Press(friends, "VisitReward");
            yield return WaitFor(() => busy.ReloadWaiting, check, "refresh is held after POST");
            Press(friends, "VisitGift"); Press(friends, "Confirm");
            Press(friends, "VisitMenu"); Press(friends, "Confirm");
            check(busy.Posts == 1 && Feedback(friends).Contains("처리 중"), "busy gift and block inputs report progress without extra POST");
            busy.HoldReloadAfterMutation = false;
            yield return WaitFor(() => !friends.ServerBusy, check, "held refresh resumes");

            for (int mode = 0; mode < 4; mode++)
            {
                var block = new Fixture { FailMutation = mode == 1, HoldMutation = mode >= 2 };
                yield return Connect(friends, block, check);
                yield return Visit(friends, FirstCode, check);
                Press(friends, "VisitMenu"); Press(friends, "Confirm");
                if (mode >= 2)
                {
                    yield return WaitFor(() => block.MutationWaiting, check, "block POST is held");
                    Press(friends, "VisitBack");
                    yield return Visit(friends, mode == 2 ? SecondCode : FirstCode, check);
                    block.HoldMutation = false;
                }
                yield return WaitFor(() => !friends.ServerBusy, check, "block response finishes");
                if (mode == 1)
                    check(friends.Page == "Visit" && host.GetRoot(3).NavigationLocked, "failed block retains visit and lock");
                else if (mode == 2)
                    check(friends.Page == "Visit" && friends.VisitSession.Scene?.Snapshot.OwnerCode == SecondCode && host.GetRoot(3).NavigationLocked,
                        "late block of A preserves current B visit");
                else
                    check(friends.Page == "Friends" && friends.VisitSession.Scene == null && !host.GetRoot(3).NavigationLocked,
                        "block of current owner ends visit including reentry to same owner");
            }

            var retry = new Fixture();
            yield return Connect(friends, retry, check);
            var oldProfile = friends.State.Friends[0];
            yield return Visit(friends, FirstCode, check);
            friends.RefreshServer();
            yield return WaitFor(() => !friends.ServerBusy, check, "profile refresh finishes");
            retry.FailExhibition = true;
            friends.Visit(oldProfile);
            yield return WaitFor(() => !friends.VisitSession.Loading, check, "injected exhibition failure finishes");
            check(friends.Page == "Visit" && friends.Buttons.ContainsKey("VisitRetry"), "old profile resolves by code after friend state replacement");
            int beforeRetry = retry.ExhibitionGets;
            retry.FailExhibition = false;
            Press(friends, "VisitRetry");
            yield return WaitFor(() => !friends.VisitSession.Loading, check, "manual exhibition retry finishes");
            check(retry.ExhibitionGets == beforeRetry + 1 && friends.VisitSession.Scene != null, "retry sends GET with refreshed friend profile");

            var reconnect = new Fixture { HoldMutation = true };
            yield return Connect(friends, reconnect, check);
            yield return Visit(friends, FirstCode, check);
            Press(friends, "VisitReward");
            yield return WaitFor(() => reconnect.MutationWaiting, check, "reward POST held before reconnect");
            var replacement = new Fixture();
            yield return Connect(friends, replacement, check);
            reconnect.HoldMutation = false;
            yield return null;
            check(friends.Page == "Friends" && friends.VisitSession.Scene == null && !host.GetRoot(3).NavigationLocked &&
                !friends.State.VisitClaimed && !Feedback(friends).Contains("주입 성공"), "reconnect cancels old visit and prevents late result notification");
        }

        private static IEnumerator Connect(FriendsPresenter friends, Fixture fixture, Action<bool, string> check)
        {
            friends.ConnectServer(new JamkkaebiApiClient("http://injected.invalid", "in-memory-test-token", fixture.Send));
            yield return WaitFor(() => !friends.ServerBusy, check, "injected server connects");
            check(friends.State.Friends.Count == 2, "injected server receives two friends");
        }

        private static IEnumerator Visit(FriendsPresenter friends, string code, Action<bool, string> check)
        {
            friends.Visit(friends.State.Friends.First(p => p.Code == code));
            yield return WaitFor(() => !friends.VisitSession.Loading, check, "injected visit loads");
            check(friends.VisitSession.Scene != null, "injected visit creates scene");
        }

        private static IEnumerator WaitFor(Func<bool> ready, Action<bool, string> check, string label)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            check(ready(), label);
        }

        private static void Press(FriendsPresenter friends, string name) => friends.Buttons[name].onClick.Invoke();
        private static string Feedback(FriendsPresenter friends) => friends.GetComponentsInChildren<Text>(true)
            .LastOrDefault(text => text.name == "Feedback" && text.gameObject.activeInHierarchy)?.text ?? "";

        /// <summary>실제 HTTP 전송 대신 상태와 응답 순서를 제어하며 요청 횟수를 기록합니다.</summary>
        private sealed class Fixture
        {
            internal bool FailMutation;
            internal bool FailReloadAfterMutation;
            internal bool HoldReloadAfterMutation;
            internal bool HoldMutation;
            internal bool FailExhibition;
            internal bool ReloadWaiting;
            internal bool MutationWaiting;
            internal int Posts;
            internal int ExhibitionGets;
            private bool _claimed;
            private bool _giftSent;
            private string _blocked;

            internal IEnumerator Send(string method, string path, JObject body, bool authenticated, Action<ServerApiResult> completed)
            {
                yield return null;
                if (method != "GET")
                {
                    Posts++;
                    MutationWaiting = true;
                    while (HoldMutation) yield return null;
                    MutationWaiting = false;
                    if (FailMutation) { completed(Failure()); yield break; }
                    if (path.EndsWith("/visit-reward", StringComparison.Ordinal)) _claimed = true;
                    if (path.EndsWith("/gift", StringComparison.Ordinal)) _giftSent = true;
                    if (path == "/api/blocks") _blocked = (string)body["friendCode"];
                    completed(Success(new JObject { ["jeongseongGained"] = 5, ["visitRewardClaimed"] = _claimed }));
                    yield break;
                }
                if (path.EndsWith("/exhibition", StringComparison.Ordinal))
                {
                    ExhibitionGets++;
                    if (FailExhibition) { completed(Failure()); yield break; }
                    string code = path.Split('/')[3];
                    completed(Success(new JObject
                    {
                        ["owner"] = Profile(code), ["grid"] = new JObject { ["width"] = 7, ["height"] = 7 },
                        ["spirits"] = new JArray(new JObject { ["relicId"] = 2, ["spiritName"] = "향로깨비", ["awakeningStage"] = "COMPLETED", ["position"] = new JObject { ["x"] = 2, ["y"] = 2 } }),
                        ["buildings"] = new JArray(), ["capturedAt"] = "2026-11-05T09:00:00+09:00",
                        ["giftState"] = "AVAILABLE", ["visitRewardAvailable"] = true
                    }));
                    yield break;
                }
                if (Posts > 0)
                {
                    ReloadWaiting = true;
                    while (HoldReloadAfterMutation) yield return null;
                    ReloadWaiting = false;
                    if (FailReloadAfterMutation) { completed(Failure()); yield break; }
                }
                JObject data;
                if (path == "/api/friends")
                {
                    var profiles = new JArray();
                    foreach (string code in new[] { FirstCode, SecondCode }) if (code != _blocked) profiles.Add(Profile(code));
                    data = new JObject { ["friends"] = profiles, ["myFriendCode"] = "K7QM3PVX", ["limit"] = 30,
                        ["today"] = new JObject { ["visitRewardClaimed"] = _claimed, ["giftSent"] = _giftSent, ["giftTicketAvailable"] = false },
                        ["nextResetAt"] = "2026-11-06T00:00:00+09:00" };
                }
                else if (path.StartsWith("/api/friend-requests", StringComparison.Ordinal)) data = new JObject { ["requests"] = new JArray() };
                else if (path == "/api/friends/recommendations") data = new JObject { ["players"] = new JArray(), ["remaining"] = 3 };
                else data = new JObject { ["blocks"] = new JArray() };
                completed(Success(data));
            }

            private static JObject Profile(string code) => new JObject { ["friendCode"] = code, ["nickname"] = code == FirstCode ? "첫 친구" : "다음 친구",
                ["lastActiveDaysAgo"] = 0, ["visitedToday"] = false, ["giftState"] = "AVAILABLE" };
            private static ServerApiResult Success(JToken data) => new ServerApiResult(true, "OK", "주입 성공", data, 200);
            private static ServerApiResult Failure() => new ServerApiResult(false, "INJECTED_FAILURE", "의도한 검증 실패", status: 503);
        }
    }
}
