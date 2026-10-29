using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    public sealed class ServerExhibitionBackend : IExhibitionBackend
    {
        private readonly JamkkaebiApiClient _client;
        /// <summary>친구 전시 조회와 방문 보상에 사용할 인증 API 클라이언트를 지정합니다.</summary>
        public ServerExhibitionBackend(JamkkaebiApiClient client) { _client = client; }
        /// <summary>대상 친구의 전시 스냅샷을 조회하고 성공 또는 오류 콜백으로 전달합니다.</summary>
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
        /// <summary>전시 조회와 분리한 방문 보상 명령으로 수령 결과를 전달합니다.</summary>
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
        /// <summary>친구 공개 전시 응답을 placeholder 배치 목록과 보상 상태를 포함한 스냅샷으로 변환합니다.</summary>
        public static ExhibitionSnapshot ParseSnapshot(JToken data)
        {
            string code = (string)data?["owner"]?["friendCode"];
            if (!FriendsPreviewState.IsValidCode(code) || data?["spirits"] is not JArray spirits || data?["buildings"] is not JArray buildings)
                throw new FormatException("잘못된 스냅샷입니다.");
            var rows = new List<SnapshotPlacement>();
            foreach (var row in spirits) rows.Add(new SnapshotPlacement(false, (int)row["relicId"],
                (string)row["spiritName"] ?? (string)row["realName"], Position(row), (string)row["awakeningStage"], placeholder: true));
            foreach (var row in buildings) rows.Add(new SnapshotPlacement(true, (int)row["buildingId"],
                (string)row["name"], Position(row), level: (int)row["level"], placeholder: true));
            bool hasGrid = data["grid"] is JObject;
            return new ExhibitionSnapshot(FriendsPreviewState.NormalizeCode(code), rows, hasGrid,
                hasGrid ? (int)data["grid"]["width"] : 0, hasGrid ? (int)data["grid"]["height"] : 0,
                (string)data["capturedAt"], (string)data["giftState"], (bool)data["visitRewardAvailable"]);
        }
        /// <summary>서버 배치 응답의 논리 x·y 좌표를 읽습니다.</summary>
        private static Vector2Int Position(JToken row) => new Vector2Int((int)row["position"]["x"], (int)row["position"]["y"]);
    }
}
