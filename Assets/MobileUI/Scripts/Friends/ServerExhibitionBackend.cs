using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MobilePrototype.Friends
{
    public sealed class ServerExhibitionBackend : IExhibitionBackend
    {
        private readonly JamkkaebiApiClient _client;
        public ServerExhibitionBackend(JamkkaebiApiClient client) { _client = client; }
        public IEnumerator GetExhibition(string friendCode, Action<ExhibitionSnapshot> completed, Action<string> failed)
        {
            return _client.Send("GET", "/api/friends/" + JamkkaebiApiClient.CodePath(friendCode) + "/exhibition", null, result =>
            {
                if (!result.Success) { failed(result.Code + ": " + result.Message); return; }
                ExhibitionSnapshot snapshot;
                try { snapshot = ParseSnapshot(result.Data); }
                catch (Exception) { failed("전시 스냅샷 형식이 올바르지 않아요."); return; }
                completed(snapshot);
            });
        }
        public IEnumerator ClaimVisitReward(string friendCode, Action<int> completed, Action<string> failed)
        {
            return _client.Send("POST", "/api/friends/" + JamkkaebiApiClient.CodePath(friendCode) + "/visit-reward", null, result =>
            {
                if (!result.Success) { failed(result.Code + ": " + result.Message); return; }
                if (result.Data?["jeongseongGained"]?.Type != JTokenType.Integer || result.Data?["visitRewardClaimed"]?.Type != JTokenType.Boolean)
                { failed("방문 보상 응답 형식이 올바르지 않아요."); return; }
                completed((int)result.Data["jeongseongGained"]);
            });
        }
        public static ExhibitionSnapshot ParseSnapshot(JToken data)
        {
            string code = (string)data?["owner"]?["friendCode"];
            if (!FriendsPreviewState.IsValidCode(code) || data?["spirits"] is not JArray spirits || data?["buildings"] is not JArray buildings)
                throw new FormatException("잘못된 스냅샷입니다.");
            var rows = new List<SnapshotPlacement>();
            foreach (var row in spirits) rows.Add(new SnapshotPlacement(false, (int)row["artifactId"],
                (string)row["spiritName"] ?? (string)row["realName"], Position(row), (string)row["awakeningStage"], placeholder: true));
            foreach (var row in buildings) rows.Add(new SnapshotPlacement(true, (int)row["buildingId"],
                (string)row["name"], Position(row), level: (int)row["level"], placeholder: true));
            bool hasGrid = data["grid"] is JObject;
            return new ExhibitionSnapshot(FriendsPreviewState.NormalizeCode(code), rows, hasGrid,
                hasGrid ? (int)data["grid"]["width"] : 0, hasGrid ? (int)data["grid"]["height"] : 0,
                (string)data["capturedAt"], (string)data["giftState"], (bool)data["visitRewardAvailable"]);
        }
        private static Vector2Int Position(JToken row) => new Vector2Int((int)row["position"]["x"], (int)row["position"]["y"]);
    }
}
