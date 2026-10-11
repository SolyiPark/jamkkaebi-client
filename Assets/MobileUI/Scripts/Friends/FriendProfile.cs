using System;
using System.Collections.Generic;

namespace MobilePrototype.Friends
{
    /// <summary>친구 화면에서 사용하는 로컬 미리보기 프로필입니다. 서버 DTO가 아닙니다.</summary>
    public sealed class FriendProfile
    {
        public string Name { get; }
        public string Code { get; }
        public int Streak { get; }
        public int CollectionPercent { get; }
        public string LastSeen { get; }
        public bool GiftFull { get; }
        public string GiftState { get; }
        public bool Requested { get; }
        public IReadOnlyList<int> RestoredByEra { get; }
        public FriendProfile(string name, string code, int streak, int percent, string lastSeen = "오늘 접속", bool giftFull = false, int[] restored = null, string giftState = "AVAILABLE", bool requested = false)
        {
            Name = name; Code = code; Streak = streak; CollectionPercent = percent;
            LastSeen = lastSeen; GiftFull = giftFull;
            GiftState = giftState;
            Requested = requested;
            RestoredByEra = Array.AsReadOnly(restored ?? new[] { Math.Min(3, percent / 11), Math.Min(2, Math.Max(0, percent / 11 - 3)), Math.Max(0, percent / 11 - 5) });
        }
    }
}
