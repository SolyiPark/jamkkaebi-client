using System;
using MobilePrototype.Friends;
using NUnit.Framework;

namespace MobilePrototype.Tests
{
    public sealed class FriendsPreviewStateTests
    {
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
