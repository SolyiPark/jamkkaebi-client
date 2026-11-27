using System;
using System.Linq;
using Jamkkaebi.Scripts.UI.Friends;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

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
    }
}
