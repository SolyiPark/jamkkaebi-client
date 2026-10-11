using System;
using System.Collections.Generic;
using System.Linq;

namespace MobilePrototype.Friends
{
    /// <summary>화면 검토용 메모리 상태입니다. 게임 보상이나 서버 저장을 변경하지 않습니다.</summary>
    public sealed class FriendsPreviewState
    {
        public const string OwnCode = "K7QM-3PVX";
        public string MyCode { get; private set; } = OwnCode;
        public bool ServerOwned { get; private set; }
        public int FriendLimit { get; private set; } = 30;
        public DateTime? NextResetAt { get; private set; }
        private int _serverRemaining;
        public List<FriendProfile> Friends { get; } = new List<FriendProfile>();
        public List<FriendProfile> Received { get; } = new List<FriendProfile>();
        public List<FriendProfile> Sent { get; } = new List<FriendProfile>();
        public List<FriendProfile> Recommendations { get; } = new List<FriendProfile>();
        public List<FriendProfile> Blocked { get; } = new List<FriendProfile>();
        public HashSet<string> Visited { get; } = new HashSet<string>();
        public bool VisitClaimed { get; private set; }
        public string GiftRecipient { get; private set; }
        public int ReceivedGifts { get; private set; } = 1;
        public int RemainingRecommendations => ServerOwned ? _serverRemaining : 3 - Recommendations.Count(p => Sent.Contains(p));
        public DateTime Day { get; private set; }
        private readonly Dictionary<string, string> _blockDates = new Dictionary<string, string>();
        /// <summary>프로필의 차단 날짜 안내를 반환하고 기록이 없으면 기본 안내를 사용합니다.</summary>
        public string BlockLabel(FriendProfile profile) => _blockDates.TryGetValue(profile.Code, out string label) ? label : profile.LastSeen;

        /// <summary>프론트엔드 검토용 모의 친구·요청·추천·차단 상태를 지정 날짜로 초기화합니다.</summary>
        public FriendsPreviewState(DateTime now)
        {
            Day = now.Date;
            string[] names = { "달항아리", "청자빛", "수막새", "솟을대문", "처마끝", "단청", "기와소리", "백자구름", "고운흙", "돌담길", "솔바람", "푸른기와" };
            string[] codes = { "H2RX-9KTA", "C4BJ-7XPA", "S8MK-2YRA", "D2MN-4KPT", "E3ND-8KQA", "D7CH-6PXA", "K2WA-5MPT", "B2JA-6MRT", "G3UN-7NXT", "D4AM-8QPT", "S5QM-9RKT", "P6RN-2STX" };
            int[] percentages = { 66, 44, 33, 22, 11, 0, 0, 11, 22, 33, 44, 55 };
            for (int i = 0; i < names.Length; i++)
                Friends.Add(new FriendProfile(names[i], codes[i], i == 0 ? 21 : i == 1 ? 12 : i == 2 ? 4 : 0,
                    percentages[i], i < 3 ? "오늘 접속" : $"{(i == 3 ? 2 : i == 4 ? 5 : 13)}일 전 접속", i == 2));
            Received.Add(new FriendProfile("기와지붕", "G7WA-3ABC", 8, 33, "2시간 전 · 13일 후 만료"));
            Received.Add(new FriendProfile("반가사유", "B7GA-3ABC", 3, 22, "어제 · 13일 후 만료"));
            Received.Add(new FriendProfile("석등", "S7DG-3ABC", 0, 11, "5일 전 · 9일 후 만료"));
            Recommendations.Add(new FriendProfile("고려청자", "G2RY-4ABC", 9, 44, restored: new[] { 2, 2, 0 }));
            Recommendations.Add(new FriendProfile("첨성대", "C2SM-4ABC", 3, 33, restored: new[] { 3, 0, 0 }));
            Recommendations.Add(new FriendProfile("앙부일구", "A2BG-4ABC", 1, 22, restored: new[] { 0, 0, 2 }));
            Sent.Add(Recommendations[1]);
            Sent.Add(new FriendProfile("한옥마루", "H3MR-4ABC", 2, 11));
            Blocked.Add(new FriendProfile("무명도깨비", "M3XA-8VQE", 0, 0, "2026.10.09 차단"));
            Blocked.Add(new FriendProfile("빈집", "T6PM-2KJD", 0, 0, "2026.10.02 차단"));
            Blocked.Add(new FriendProfile("흙담", "Z9RH-4NCW", 0, 0, "2026.09.21 차단"));
            Visited.Add(Friends[0].Code);
        }

        /// <summary>친구 코드의 공백·하이픈을 제거하고 대문자로 통일합니다.</summary>
        public static string NormalizeCode(string value) => new string((value ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        /// <summary>정규화한 코드가 서버가 허용하는 문자로 된 8자리 식별자인지 검사합니다.</summary>
        public static bool IsValidCode(string value)
        {
            string code = NormalizeCode(value);
            return code.Length == 8 && code.All(c => "23456789ABCDEFGHJKMNPQRSTUVWXYZ".Contains(c));
        }
        /// <summary>모의 사용자 중 자기 자신·차단 사용자를 제외한 코드 일치 프로필을 찾습니다.</summary>
        public FriendProfile Find(string code)
        {
            if (!IsValidCode(code)) return null;
            string normalized = NormalizeCode(code);
            if (Blocked.Any(p => NormalizeCode(p.Code) == normalized)) return null;
            return Friends.Concat(Received).Concat(Sent).Concat(Recommendations)
                .FirstOrDefault(p => NormalizeCode(p.Code) == normalized);
        }
        /// <summary>모의 상태에서 자기 자신·차단·중복 친구 요청을 거부하고 새 요청을 추가합니다.</summary>
        public bool Request(FriendProfile profile)
        {
            if (profile == null || Sent.Count >= 10 || Friends.Contains(profile) || Sent.Contains(profile) ||
                Blocked.Contains(profile) || NormalizeCode(profile.Code) == NormalizeCode(OwnCode)) return false;
            Sent.Add(profile); return true;
        }
        /// <summary>모의 받은 요청을 친구로 옮기고 받은·보낸 요청 목록을 정리합니다.</summary>
        public bool Accept(FriendProfile profile)
        {
            if (Friends.Count >= 30 || !Received.Remove(profile)) return false;
            Friends.Add(profile); Sent.Remove(profile); return true;
        }
        /// <summary>모의 상태의 하루 공통 방문 보상을 한 번만 수령 처리합니다.</summary>
        public bool ClaimVisit(FriendProfile profile)
        {
            if (!Friends.Contains(profile) || VisitClaimed) return false;
            VisitClaimed = true; Visited.Add(profile.Code); return true;
        }
        /// <summary>모의 상태의 수신함 여유와 하루 공통 선물 제한을 확인한 뒤 수신자를 기록합니다.</summary>
        public bool SendGift(FriendProfile profile)
        {
            if (!Friends.Contains(profile) || profile.GiftFull || GiftRecipient != null) return false;
            GiftRecipient = profile.Code; return true;
        }
        /// <summary>모의 친구·요청 관계를 제거하고 차단 목록과 날짜를 기록합니다.</summary>
        public void Block(FriendProfile profile)
        {
            Friends.Remove(profile); Received.Remove(profile); Sent.Remove(profile);
            if (!Blocked.Contains(profile)) Blocked.Add(profile);
            _blockDates[profile.Code] = DateTime.Now.ToString("yyyy.MM.dd") + " 차단";
        }
        /// <summary>모의 모드에서 날짜가 넘어가면 일일 상태를 초기화합니다. 서버 상태는 기기 날짜로 변경하지 않습니다.</summary>
        public bool RefreshDay(DateTime now)
        {
            if (ServerOwned) return false;
            if (now.Date <= Day) return false;
            Day = now.Date; VisitClaimed = false; GiftRecipient = null; Visited.Clear(); return true;
        }
        /// <summary>서버 친구 코드·일일 행동·한도·다음 초기화 시각을 화면 상태에 반영합니다.</summary>
        public void ApplyServerSummary(string code, bool visitClaimed, bool giftSent, bool giftAvailable, int limit, int remaining, DateTime nextReset)
        {
            ServerOwned = true; MyCode = code; VisitClaimed = visitClaimed;
            GiftRecipient = giftSent ? "SERVER_SENT" : null; ReceivedGifts = giftAvailable ? 1 : 0;
            FriendLimit = limit; _serverRemaining = remaining; NextResetAt = nextReset;
        }
        /// <summary>더미 친구·요청·보상 상태를 제거해 서버 조회 실패가 모의 성공으로 보이지 않게 합니다.</summary>
        public void ClearForServer()
        {
            ServerOwned = true; MyCode = ""; Friends.Clear(); Received.Clear(); Sent.Clear(); Recommendations.Clear(); Blocked.Clear(); Visited.Clear();
            VisitClaimed = false; GiftRecipient = null; ReceivedGifts = 0;
        }
    }
}
