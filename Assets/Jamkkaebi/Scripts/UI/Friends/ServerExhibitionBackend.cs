using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        /// <summary>친구 공개 전시 응답을 배치 목록과 보상 상태로 변환하고 손상된 개별 행은 경고와 함께 제외합니다.</summary>
        public static ExhibitionSnapshot ParseSnapshot(JToken data)
        {
            if (data is not JObject payload || payload["owner"] is not JObject owner)
                throw new FormatException("잘못된 스냅샷입니다.");
            string code = owner["friendCode"]?.Type == JTokenType.String ? (string)owner["friendCode"] : null;
            if (!FriendsPreviewState.IsValidCode(code) || payload["spirits"] is not JArray spirits || payload["buildings"] is not JArray buildings ||
                payload["giftState"]?.Type != JTokenType.String || payload["visitRewardAvailable"]?.Type != JTokenType.Boolean)
                throw new FormatException("잘못된 스냅샷입니다.");
            var rows = new List<SnapshotPlacement>();
            var warnings = new List<string>();
            ReadRows(spirits, false, rows, warnings);
            ReadRows(buildings, true, rows, warnings);
            bool hasGrid = payload["grid"] is JObject;
            if (!hasGrid && (spirits.Count != 0 || buildings.Count != 0))
                throw new FormatException("배치가 있는 전시에는 격자 정보가 필요합니다.");
            if (payload["grid"] != null && payload["grid"].Type != JTokenType.Null && !hasGrid)
                throw new FormatException("잘못된 전시 격자입니다.");
            if (hasGrid && (payload["grid"]["width"]?.Type != JTokenType.Integer || payload["grid"]["height"]?.Type != JTokenType.Integer))
                throw new FormatException("잘못된 전시 격자입니다.");
            return new ExhibitionSnapshot(FriendsPreviewState.NormalizeCode(code), rows, hasGrid,
                hasGrid ? (int)payload["grid"]["width"] : 0, hasGrid ? (int)payload["grid"]["height"] : 0,
                FormatCapturedAt(payload["capturedAt"]), (string)payload["giftState"], (bool)payload["visitRewardAvailable"], warnings);
        }
        /// <summary>객체별 필수 필드를 검사해 정상 행만 추가하며 최상위 패키지 오류와 구분합니다.</summary>
        private static void ReadRows(JArray source, bool isBuilding, List<SnapshotPlacement> rows, List<string> warnings)
        {
            foreach (var token in source)
            {
                if (token is not JObject row || row["position"] is not JObject position ||
                    row[isBuilding ? "buildingId" : "relicId"]?.Type != JTokenType.Integer ||
                    position["x"]?.Type != JTokenType.Integer || position["y"]?.Type != JTokenType.Integer ||
                    (isBuilding && (row["name"]?.Type != JTokenType.String || row["level"]?.Type != JTokenType.Integer)) ||
                    (!isBuilding && ((row["spiritName"]?.Type != JTokenType.String && row["realName"]?.Type != JTokenType.String) ||
                        row["awakeningStage"]?.Type != JTokenType.String)))
                {
                    warnings.Add("필수 정보가 없는 배치는 표시하지 않았어요.");
                    continue;
                }
                try
                {
                    string name = isBuilding ? (string)row["name"] :
                        row["spiritName"]?.Type == JTokenType.String ? (string)row["spiritName"] : (string)row["realName"];
                    rows.Add(new SnapshotPlacement(isBuilding, (int)row[isBuilding ? "buildingId" : "relicId"],
                        name,
                        new Vector2Int((int)position["x"], (int)position["y"]),
                        isBuilding ? "AWAKENED" : (string)row["awakeningStage"], isBuilding ? (int)row["level"] : 1));
                }
                catch (OverflowException) { warnings.Add("값의 범위를 벗어난 배치는 표시하지 않았어요."); }
            }
        }
        /// <summary>문자열과 JSON 날짜 토큰을 한국 시간의 일관된 표시 형식으로 바꾸고 잘못된 값은 생략합니다.</summary>
        private static string FormatCapturedAt(JToken token)
        {
            try { return FormatValidCapturedAt(token); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>표현 가능한 날짜를 정규화하며 시간대 변환이 날짜 범위를 벗어나면 호출 측에서 생략합니다.</summary>
        private static string FormatValidCapturedAt(JToken token)
        {
            DateTimeOffset value;
            if (token?.Type == JTokenType.Date)
            {
                object raw = ((JValue)token).Value;
                if (raw is DateTimeOffset offset) value = offset;
                else
                {
                    var date = (DateTime)raw;
                    value = date.Kind == DateTimeKind.Unspecified ? new DateTimeOffset(date, TimeSpan.FromHours(9)) : new DateTimeOffset(date);
                }
            }
            else
            {
                if (token?.Type != JTokenType.String || !DateTime.TryParse((string)token, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var date)) return null;
                // 오프셋 없는 서버 로컬 시각은 기기의 시간대에 따라 이동시키지 않습니다.
                if (date.Kind == DateTimeKind.Unspecified) value = new DateTimeOffset(date, TimeSpan.FromHours(9));
                else if (!DateTimeOffset.TryParse((string)token, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return null;
            }
            return value.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
