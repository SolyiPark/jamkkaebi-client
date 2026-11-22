using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>JsonUnwrapped 요약과 중첩 player를 구분하여 서버 상태를 화면 모델로 변환합니다.</summary>
    public static class FriendsServerMapper
    {
        /// <summary>서버 프로필과 시대별 복원 통계를 친구 화면의 프로필 데이터로 변환합니다.</summary>
        public static FriendProfile Profile(JToken player, string lastSeen = "", JToken eras = null)
        {
            string code = (string)player["friendCode"], name = (string)player["nickname"];
            if (!FriendsPreviewState.IsValidCode(code) || string.IsNullOrEmpty(name)) throw new FormatException("프로필 형식 오류");
            int[] restored = new int[3];
            string[] eraNames = { "THREE_KINGDOMS", "GORYEO", "JOSEON" };
            if (eras is JArray) foreach (var row in eras)
            {
                int index = Array.IndexOf(eraNames, (string)row["era"]);
                if (index >= 0) restored[index] = (int)row["completedCount"];
            }
            return new FriendProfile(name, FriendsPreviewState.NormalizeCode(code), (int?)player["streakDays"] ?? 0,
                (int?)player["collectionRate"] ?? 0, lastSeen, (string)player["giftState"] == "RECEIVER_FULL", restored,
                (string)player["giftState"] ?? "AVAILABLE", (bool?)player["requested"] ?? false);
        }
        /// <summary>서버의 친구·요청·추천·차단 응답을 공통 프로필 참조를 사용하는 화면 상태로 변환합니다.</summary>
        public static FriendsPreviewState Create(JToken friends, JToken received, JToken sent, JToken recommendations, JToken blocks)
        {
            var state = new FriendsPreviewState(DateTime.Today); state.ClearForServer();
            var profiles = new Dictionary<string, FriendProfile>();
            foreach (var row in (JArray)friends["friends"])
            {
                int days = (int)row["lastActiveDaysAgo"];
                var profile = Profile(row, days == 0 ? "오늘 접속" : days + "일 전 접속");
                profiles[profile.Code] = profile; state.Friends.Add(profile);
                if ((bool)row["visitedToday"]) state.Visited.Add(profile.Code);
            }
            foreach (var row in (JArray)recommendations["players"])
            {
                var profile = Profile(row, eras: row["restoredByEra"]);
                if (profiles.TryGetValue(profile.Code, out var existing)) profile = existing;
                else profiles[profile.Code] = profile;
                state.Recommendations.Add(profile);
            }
            foreach (var pair in new[] { (received, state.Received), (sent, state.Sent) })
                foreach (var row in (JArray)pair.Item1["requests"])
                {
                    var profile = Profile(row["player"], "만료: " + ServerTime(row["expiresAt"]));
                    if (profiles.TryGetValue(profile.Code, out var existing)) profile = existing;
                    else profiles[profile.Code] = profile;
                    pair.Item2.Add(profile);
                }
            foreach (var row in (JArray)blocks["blocks"]) state.Blocked.Add(Profile(row, ServerTime(row["blockedAt"]) + " 차단"));
            state.ApplyServerSummary(FriendsPreviewState.NormalizeCode((string)friends["myFriendCode"]),
                (bool)friends["today"]["visitRewardClaimed"], (bool)friends["today"]["giftSent"],
                (bool)friends["today"]["giftTicketAvailable"], (int)friends["limit"], (int)recommendations["remaining"],
                DateTime.Parse((string)friends["nextResetAt"], CultureInfo.InvariantCulture, DateTimeStyles.None));
            return state;
        }
        /// <summary>
        /// 서버 시각을 "yyyy-MM-dd HH:mm"으로 표시합니다. 서버는 오프셋 없는 KST 시각을 보내므로 시간대를 옮기지 않습니다.
        /// JSON 파서가 날짜로 바꿔 둔 토큰을 문자열로 바로 바꾸면 "12/04/2026 16:27:01"처럼 보여 형식을 고정합니다.
        /// </summary>
        public static string ServerTime(JToken token)
        {
            if (token?.Type == JTokenType.Date)
            {
                object raw = ((JValue)token).Value;
                return (raw is DateTimeOffset offset ? offset.DateTime : (DateTime)raw).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            if (token?.Type == JTokenType.String && DateTime.TryParse((string)token, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return token?.Type == JTokenType.String ? (string)token : "";
        }
        /// <summary>서버 검색 결과를 프로필로 변환하고 기존 관계 목록의 동일 참조를 재사용합니다.</summary>
        public static FriendProfile Lookup(JToken data, FriendsPreviewState state)
        {
            var profile = Profile(data["player"], eras: data["restoredByEra"]);
            return state.Friends.Concat(state.Sent).Concat(state.Received).FirstOrDefault(p => p.Code == profile.Code) ?? profile;
        }
    }
}
