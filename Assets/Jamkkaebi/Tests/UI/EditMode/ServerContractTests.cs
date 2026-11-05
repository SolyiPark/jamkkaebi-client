using System;
using System.Collections;
using System.IO;
using System.Linq;
using Jamkkaebi.Scripts.UI.Friends;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Jamkkaebi.Tests.UI
{
    public sealed class ServerContractTests
    {
        /// <summary>친구 코드가 서버의 허용 문자 집합과 일치하는지 검증합니다.</summary>
        [TestCase(" H2 RX-9KTA ", true)]
        [TestCase("H2RX0KTA", false)]
        [TestCase("H2RX1KTA", false)]
        [TestCase("H2RXIKTA", false)]
        [TestCase("H2RXLKTA", false)]
        [TestCase("H2RXOKTA", false)]
        public void FriendCodeUsesServerAlphabet(string code, bool valid)
        { Assert.AreEqual(valid, FriendsPreviewState.IsValidCode(code)); }

        /// <summary>API 오류와 선택적인 data 필드를 구분하고 잘못된 응답을 거부하는지 검증합니다.</summary>
        [Test]
        public void EnvelopeSeparatesErrorsFromOptionalData()
        {
            var error = ServerApiResult.Parse("{\"success\":false,\"code\":\"F008\",\"message\":\"친구가 아닙니다\"}", 403);
            Assert.IsFalse(error.Success); Assert.IsNull(error.Data); Assert.AreEqual("F008", error.Code);
            var empty = ServerApiResult.Parse("{\"success\":true,\"code\":\"BLOCK_REMOVED\"}", 200);
            Assert.IsTrue(empty.Success); Assert.IsNull(empty.Data);
            Assert.Throws<FormatException>(() => ServerApiResult.Parse("{\"success\":true,\"code\":\"OK\"}", 500));
            Assert.Throws<FormatException>(() => ServerApiResult.Parse("{\"friends\":[]}", 200));
        }
        /// <summary>미저장 전시의 생략 필드와 소유자 코드 정규화를 검증합니다.</summary>
        [Test]
        public void EmptySnapshotOmitsGridAndCaptureTimeAndCodeIsNormalized()
        {
            var snapshot = ServerExhibitionBackend.ParseSnapshot(JObject.Parse("{\"owner\":{\"friendCode\":\"H2RX9KTA\"},\"spirits\":[],\"buildings\":[],\"giftState\":\"DONE_TODAY\",\"visitRewardAvailable\":false}"));
            var session = new ExhibitionVisitSession();
            Assert.IsTrue(session.Complete(session.Begin("h2rx-9kta"), snapshot));
            Assert.IsFalse(snapshot.HasGrid); Assert.IsNull(snapshot.CapturedAt); Assert.IsFalse(snapshot.VisitRewardAvailable);
            Assert.AreEqual("DONE_TODAY", snapshot.GiftState);
        }

        /// <summary>불완전한 행만 경고와 함께 제외하고 나머지 배치와 7×7 좌표를 유지합니다.</summary>
        [Test]
        public void SnapshotSkipsMalformedRowsWithoutDiscardingOtherObjects()
        {
            var payload = SnapshotPayload();
            ((JArray)payload["spirits"]).Add(new JObject { ["relicId"] = 3 });
            ((JArray)payload["buildings"]).Add(JValue.CreateNull());
            var snapshot = ServerExhibitionBackend.ParseSnapshot(payload);
            Assert.AreEqual(2, snapshot.Placements.Count);
            Assert.AreEqual(2, snapshot.ParseWarnings.Count);
            var scene = new ExhibitionVisitScene(snapshot);
            Assert.AreEqual(2, scene.Objects.Count);
            Assert.AreEqual(2, scene.Warnings.Count);
            Assert.IsFalse(scene.Objects.Any(obj => obj.Unknown));
            Assert.AreEqual(new UnityEngine.Vector2Int(2, 4), snapshot.Placements[0].Anchor);
        }

        /// <summary>문자열 이름만 선택하고 사용할 이름이 없는 행을 제외해도 다른 정상 배치는 보존합니다.</summary>
        [TestCase("{}", "\"정상 유물명\"", "정상 유물명")]
        [TestCase("[]", "\"정상 유물명\"", "정상 유물명")]
        [TestCase("123", "\"정상 유물명\"", "정상 유물명")]
        [TestCase("true", "\"정상 유물명\"", "정상 유물명")]
        [TestCase("null", "\"정상 유물명\"", "정상 유물명")]
        [TestCase("{}", "[]", null)]
        [TestCase("null", "123", null)]
        [TestCase("\"말발굽\"", "\"정상 유물명\"", "말발굽")]
        [TestCase("\"말발굽\"", "{}", "말발굽")]
        public void SnapshotSelectsOnlyStringNamesAndPreservesOtherPlacements(string spiritNameJson, string realNameJson, string expectedName)
        {
            var payload = SnapshotPayload();
            var spirits = (JArray)payload["spirits"];
            var normalSpirit = (JObject)spirits[0].DeepClone();
            normalSpirit["relicId"] = 2; normalSpirit["spiritName"] = "정상 도깨비";
            spirits.Add(normalSpirit);
            spirits[0]["spiritName"] = JToken.Parse(spiritNameJson);
            spirits[0]["realName"] = JToken.Parse(realNameJson);

            var snapshot = ServerExhibitionBackend.ParseSnapshot(payload);
            Assert.AreEqual(expectedName == null ? 2 : 3, snapshot.Placements.Count);
            Assert.AreEqual(expectedName == null ? 1 : 0, snapshot.ParseWarnings.Count);
            Assert.AreEqual("정상 도깨비", snapshot.Placements.Single(row => !row.IsBuilding && row.Id == 2).Name);
            Assert.AreEqual("석굴암", snapshot.Placements.Single(row => row.IsBuilding && row.Id == 1).Name);
            if (expectedName == null) Assert.IsFalse(snapshot.Placements.Any(row => !row.IsBuilding && row.Id == 8));
            else Assert.AreEqual(expectedName, snapshot.Placements.Single(row => !row.IsBuilding && row.Id == 8).Name);
        }

        /// <summary>개별 행 오류와 달리 격자·소유자·배열 등 최상위 계약 오류는 패키지 전체를 거부합니다.</summary>
        [Test]
        public void SnapshotRejectsMalformedTopLevelFields()
        {
            var payload = SnapshotPayload();
            payload["grid"]["width"] = "7";
            Assert.Throws<FormatException>(() => ServerExhibitionBackend.ParseSnapshot(payload));
            payload = SnapshotPayload(); payload["spirits"] = new JObject();
            Assert.Throws<FormatException>(() => ServerExhibitionBackend.ParseSnapshot(payload));
            payload = SnapshotPayload(); payload["visitRewardAvailable"] = "true";
            Assert.Throws<FormatException>(() => ServerExhibitionBackend.ParseSnapshot(payload));
            payload = SnapshotPayload(); payload.Remove("grid");
            // 개별 행이 모두 손상돼도 원래 배치 목록이 있었다면 최상위 격자를 요구합니다.
            payload["spirits"] = new JArray(new JObject()); payload["buildings"] = new JArray();
            Assert.Throws<FormatException>(() => ServerExhibitionBackend.ParseSnapshot(payload));
        }

        /// <summary>JSON 파서가 만든 날짜 토큰과 날짜 문자열 모두 동일한 한국 시간 형식으로 표시합니다.</summary>
        [TestCase("2026-11-05T01:30:00Z", "2026-11-05 10:30")]
        [TestCase("2026-11-05T10:30:00+09:00", "2026-11-05 10:30")]
        [TestCase("2026-11-05T10:30:00", "2026-11-05 10:30")]
        [TestCase("잘못된 시각", null)]
        [TestCase("0001-01-01T00:00:00", null)]
        [TestCase("9999-12-31T23:59:59Z", null)]
        public void SnapshotCaptureTimeHasStableDisplayFormat(string capturedAt, string expected)
        {
            var payload = SnapshotPayload(); payload["capturedAt"] = capturedAt;
            Assert.AreEqual(expected, ServerExhibitionBackend.ParseSnapshot(payload).CapturedAt);
            var parsed = JObject.Parse(payload.ToString());
            Assert.AreEqual(expected, ServerExhibitionBackend.ParseSnapshot(parsed).CapturedAt);
        }

        /// <summary>서버의 최소 정상 전시 응답을 검사마다 독립적으로 만듭니다.</summary>
        private static JObject SnapshotPayload() => JObject.Parse(@"{
            'owner': {'friendCode': 'H2RX9KTA'},
            'spirits': [{'relicId': 8, 'spiritName': '말발굽', 'position': {'x': 2, 'y': 4}, 'awakeningStage': 'AWAKENED'}],
            'buildings': [{'buildingId': 1, 'name': '석굴암', 'position': {'x': 1, 'y': 1}, 'level': 2}],
            'grid': {'width': 7, 'height': 7}, 'giftState': 'AVAILABLE', 'visitRewardAvailable': true
        }");
        /// <summary>실제 만료 응답 후 갱신 소유 요청 취소가 대기 요청에 오류로 전달되는지 검증합니다.</summary>
        [UnityTest]
        public IEnumerator CancelledSharedRefreshCompletesWaitingRequestWithAnError()
        {
            string file = Environment.GetEnvironmentVariable("JAMKKAEBI_CONTRACT_CONFIG");
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) Assert.Ignore("로컬 Spring 계약 검증 서버가 설정되지 않았습니다.");
            var config = JObject.Parse(File.ReadAllText(file));
            var client = new JamkkaebiApiClient((string)config["baseUrl"], (string)config["expiredAccessToken"], (string)config["refreshToken"]);
            var owner = client.Send("GET", "/api/friends", null, value => Assert.Fail("취소한 요청은 완료되면 안 됩니다."));
            Assert.IsTrue(owner.MoveNext()); yield return owner.Current;
            // 갱신 코루틴을 시작하기 직전에 취소한다. 서버 회전 토큰은 소비하지 않는다.
            Assert.IsTrue(owner.MoveNext());
            ServerApiResult response = null;
            var waiter = client.Send("GET", "/api/friends", null, value => response = value);
            Assert.IsTrue(waiter.MoveNext()); yield return waiter.Current;
            Assert.IsTrue(waiter.MoveNext()); Assert.IsNull(waiter.Current);
            ((IDisposable)owner).Dispose();
            Assert.IsFalse(waiter.MoveNext());
            Assert.IsNotNull(response); Assert.IsFalse(response.Success);
            Assert.AreEqual("AUTH_REFRESH_INTERRUPTED", response.Code);
        }

        /// <summary>로컬 Spring 서버의 인증·친구·요청·차단·보상·선물 계약을 Unity HTTP로 검증합니다.</summary>
        [UnityTest]
        public IEnumerator UnityHttpTalksToRealSpringServer()
        {
            string file = Environment.GetEnvironmentVariable("JAMKKAEBI_CONTRACT_CONFIG");
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) Assert.Ignore("로컬 Spring 계약 검증 서버가 설정되지 않았습니다.");
            var config = JObject.Parse(File.ReadAllText(file));
            var client = new JamkkaebiApiClient((string)config["baseUrl"], (string)config["expiredAccessToken"], (string)config["refreshToken"]);
            ServerApiResult response = null;
            var data = new JToken[5];
            string[] paths = { "/api/friends", "/api/friend-requests?direction=RECEIVED", "/api/friend-requests?direction=SENT", "/api/friends/recommendations", "/api/blocks" };
            for (int i = 0; i < paths.Length; i++)
            {
                yield return client.Send("GET", paths[i], null, value => response = value);
                Assert.IsTrue(response.Success, response.Code + " HTTP=" + response.Status + " " + response.Message); data[i] = response.Data;
            }
            var state = FriendsServerMapper.Create(data[0], data[1], data[2], data[3], data[4]);
            Assert.AreEqual(2, state.Friends.Count); Assert.AreEqual((string)config["myCode"], state.MyCode);
            Assert.IsTrue(client.Authenticated); // 만료 Access → 실제 reissue → 원 요청 재실행
            var reused = new JamkkaebiApiClient((string)config["baseUrl"], (string)config["expiredAccessToken"], (string)config["refreshToken"]);
            // 재사용 검사는 마지막에 수행: 서버는 해당 계정의 모든 Refresh를 폐기합니다.
            string friend = (string)config["friendCode"], large = (string)config["largeGridCode"], target = (string)config["targetCode"];
            var backend = new ServerExhibitionBackend(client);
            ExhibitionSnapshot snapshot = null;
            yield return backend.GetExhibition(friend, value => snapshot = value, error => Assert.Fail(error));
            Assert.AreEqual(2, snapshot.Placements.Count); Assert.AreEqual("별이", snapshot.Placements[0].Name);
            Assert.AreEqual(8, snapshot.Placements[0].Id); Assert.AreEqual(9, snapshot.Placements[1].Id);
            Assert.AreEqual(2, new ExhibitionVisitScene(snapshot).Objects.Count);
            yield return client.Send("GET", "/api/friends", null, value => response = value);
            Assert.IsFalse((bool)response.Data["today"]["visitRewardClaimed"]);
            foreach (var row in response.Data["friends"]) Assert.IsFalse((bool)row["visitedToday"]);
            yield return backend.GetExhibition(large, value => snapshot = value, error => Assert.Fail(error));
            Assert.AreEqual(20, snapshot.Width); Assert.Throws<ArgumentException>(() => new ExhibitionVisitScene(snapshot));
            int amount = -1;
            yield return backend.ClaimVisitReward(friend, value => amount = value, error => Assert.Fail(error)); Assert.AreEqual(5, amount);
            yield return backend.ClaimVisitReward(friend, value => amount = value, error => Assert.Fail(error)); Assert.AreEqual(0, amount);
            yield return client.Send("GET", "/api/friends", null, value => response = value);
            Assert.IsTrue((bool)response.Data["today"]["visitRewardClaimed"]);
            Assert.IsTrue((bool)Find((JArray)response.Data["friends"], friend)["visitedToday"]);
            yield return client.Send("POST", "/api/friends/" + friend + "/gift", null, value => response = value);
            Assert.IsTrue(response.Success); Assert.AreEqual(3, (int)response.Data["jeongseongGained"]);
            yield return client.Send("POST", "/api/friends/" + friend + "/gift", null, value => response = value);
            Assert.IsTrue(response.Success); Assert.AreEqual(0, (int)response.Data["jeongseongGained"]);
            yield return client.Send("POST", "/api/friends/" + large + "/gift", null, value => response = value); Assert.AreEqual("F009", response.Code);
            yield return client.Send("GET", "/api/friends/lookup/" + target, null, value => response = value);
            Assert.IsTrue(response.Success); Assert.AreEqual("NONE", (string)response.Data["relation"]);
            yield return client.Send("POST", "/api/friend-requests", new JObject { ["friendCode"] = target }, value => response = value);
            Assert.IsTrue(response.Success); Assert.AreEqual(201, response.Status);
            var other = new JamkkaebiApiClient((string)config["baseUrl"], (string)config["otherToken"]);
            yield return other.Send("POST", "/api/friend-requests", new JObject { ["friendCode"] = state.MyCode }, value => response = value);
            Assert.IsTrue(response.Success); Assert.AreEqual("FRIEND_ADDED", response.Code);
            yield return client.Send("POST", "/api/blocks", new JObject { ["friendCode"] = target }, value => response = value); Assert.IsTrue(response.Success);
            yield return client.Send("GET", "/api/blocks", null, value => response = value); Assert.AreEqual(1, ((JArray)response.Data["blocks"]).Count);
            yield return client.Send("GET", "/api/friends/lookup/" + target, null, value => response = value); Assert.IsFalse(response.Success);
            yield return client.Send("DELETE", "/api/blocks/" + target, null, value => response = value); Assert.IsTrue(response.Success);
            yield return other.Send("POST", "/api/friend-requests", new JObject { ["friendCode"] = state.MyCode }, value => response = value); Assert.IsTrue(response.Success);
            yield return client.Send("GET", "/api/friend-requests?direction=RECEIVED", null, value => response = value); Assert.AreEqual(1, ((JArray)response.Data["requests"]).Count);
            yield return client.Send("POST", "/api/friend-requests/" + target + "/accept", null, value => response = value); Assert.IsTrue(response.Success);
            string extra = (string)config["extraCode"];
            var extraClient = new JamkkaebiApiClient((string)config["baseUrl"]);
            yield return extraClient.Exchange((string)config["handoff"], (string)config["verifier"], value => response = value);
            Assert.IsTrue(response.Success); Assert.IsTrue(extraClient.Authenticated);
            Assert.AreEqual(extra, (string)response.Data["friendCode"]);
            yield return extraClient.Send("POST", "/api/friend-requests", new JObject { ["friendCode"] = state.MyCode }, value => response = value); Assert.IsTrue(response.Success);
            yield return client.Send("POST", "/api/friend-requests/" + extra + "/reject", null, value => response = value); Assert.IsTrue(response.Success);
            yield return client.Send("POST", "/api/friend-requests", new JObject { ["friendCode"] = extra }, value => response = value); Assert.IsTrue(response.Success);
            Assert.AreEqual("FRIEND_ADDED", response.Code); // 거절한 쪽이 역방향 요청하면 서버는 즉시 수락합니다.
            yield return client.Send("DELETE", "/api/friend-requests/" + extra, null, value => response = value); Assert.IsTrue(response.Success);
            Assert.AreEqual("FRIEND", (string)response.Data["relation"]); // 요청 취소로 이미 맺어진 친구 관계는 삭제되지 않습니다.
            yield return client.Send("DELETE", "/api/friends/" + extra, null, value => response = value); Assert.IsTrue(response.Success);
            yield return client.StartLogin("GOOGLE", JamkkaebiApiClient.CreateVerifier(), value => response = value); Assert.IsTrue(response.Success);
            Assert.IsNotEmpty((string)response.Data["authorizeUrl"]);
            yield return client.StartLogin("KAKAO", JamkkaebiApiClient.CreateVerifier(), value => response = value); Assert.IsTrue(response.Success);
            Assert.IsTrue(((string)response.Data["authorizeUrl"]).Contains("/oauth2/authorization/kakao"));
            yield return reused.Send("GET", "/api/friends", null, value => response = value); Assert.AreEqual("A005", response.Code); Assert.IsFalse(reused.Authenticated);
            yield return client.Logout(value => response = value); Assert.IsTrue(response.Success); Assert.IsFalse(client.Authenticated);
            var anonymous = new JamkkaebiApiClient((string)config["baseUrl"]);
            yield return anonymous.Send("GET", "/api/friends", null, value => response = value, false);
            Assert.IsFalse(response.Success); Assert.AreEqual(401, response.Status);
        }
        /// <summary>서버 응답 배열에서 친구 코드가 일치하는 항목을 찾습니다.</summary>
        private static JToken Find(JArray rows, string code)
        { foreach (var row in rows) if ((string)row["friendCode"] == code) return row; throw new Exception("친구 없음"); }
    }
}
