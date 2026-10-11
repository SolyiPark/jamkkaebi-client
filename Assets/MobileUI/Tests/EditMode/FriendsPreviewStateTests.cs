using System;
using MobilePrototype.Friends;
using NUnit.Framework;

namespace MobilePrototype.Tests
{
    public sealed class FriendsPreviewStateTests
    {
        /// <summary>모의 방문·선물 제한이 친구 간 공유되고 다음 날 초기화되는지 검증합니다.</summary>
        [Test]
        public void DailyActionsAreSharedAcrossFriendsAndResetAtMidnight()
        {
            var state = new FriendsPreviewState(new DateTime(2026, 10, 11, 23, 59, 0));
            Assert.IsTrue(state.ClaimVisit(state.Friends[0]));
            Assert.IsFalse(state.ClaimVisit(state.Friends[1]));
            Assert.IsFalse(state.SendGift(state.Friends[2]));
            Assert.IsTrue(state.SendGift(state.Friends[0]));
            Assert.IsFalse(state.SendGift(state.Friends[1]));
            Assert.IsFalse(state.RefreshDay(new DateTime(2026, 10, 11, 23, 59, 59)));
            Assert.IsTrue(state.RefreshDay(new DateTime(2026, 10, 12)));
            Assert.IsTrue(state.ClaimVisit(state.Friends[1]));
            Assert.IsTrue(state.SendGift(state.Friends[1]));
        }

        /// <summary>모의 차단의 검색·요청 제한과 해제 후 관계 미복구를 검증합니다.</summary>
        [Test]
        public void BlockPreventsLookupAndRequestsAndUnblockDoesNotRestoreFriendship()
        {
            var state = new FriendsPreviewState(DateTime.Today);
            var profile = state.Friends[0];
            Assert.AreSame(profile, state.Find("h2rx9kta"));
            state.Block(profile);
            Assert.IsNull(state.Find(profile.Code));
            Assert.IsFalse(state.Request(profile));
            state.Blocked.Remove(profile);
            Assert.IsFalse(state.Friends.Contains(profile));
        }

        /// <summary>모의 수락의 목록 정리와 중복 요청 거부를 검증합니다.</summary>
        [Test]
        public void AcceptRemovesRequestAndDuplicateSendIsRejected()
        {
            var state = new FriendsPreviewState(DateTime.Today);
            var request = state.Received[0];
            Assert.IsTrue(state.Accept(request));
            Assert.IsFalse(state.Accept(request));
            Assert.IsTrue(state.Friends.Contains(request));
            Assert.IsFalse(state.Request(request));
            var recommendation = state.Recommendations[0];
            Assert.IsTrue(state.Request(recommendation));
            Assert.IsFalse(state.Request(recommendation));
        }

        /// <summary>친구 코드 검색의 길이·문자 형식 검증을 확인합니다.</summary>
        [TestCase("h2rx-9kta", true)]
        [TestCase("H2RX9KTA", true)]
        [TestCase("H2RX", false)]
        [TestCase("H2RX_9KT", false)]
        [TestCase("한글코드1234", false)]
        public void LookupRequiresEightAsciiLettersOrDigits(string code, bool expected)
        {
            Assert.AreEqual(expected, FriendsPreviewState.IsValidCode(code));
        }
    }
}
