using System.Linq;
using MobilePrototype.Friends;
using NUnit.Framework;
using UnityEngine;

namespace MobilePrototype.Tests
{
    public sealed class ExhibitionVisitTests
    {
        /// <summary>모의 GET의 무보상과 별도 보상 명령의 하루 한 번 수령을 검증합니다.</summary>
        [Test]
        public void SnapshotQueryDoesNotClaimRewardAndRepeatedClaimReturnsZero()
        {
            var state = new FriendsPreviewState(System.DateTime.Today);
            var backend = new MockExhibitionBackend(ExhibitionPreviewScenario.Normal, state);
            bool received = false;
            var query = backend.GetExhibition(state.Friends[0].Code, snapshot => received = true, error => Assert.Fail(error));
            while (query.MoveNext()) { }
            Assert.IsTrue(received); Assert.IsFalse(state.VisitClaimed);
            int gained = -1;
            var claim = backend.ClaimVisitReward(state.Friends[0].Code, amount => gained = amount, error => Assert.Fail(error));
            while (claim.MoveNext()) { }
            Assert.AreEqual(5, gained);
            claim = backend.ClaimVisitReward(state.Friends[1].Code, amount => gained = amount, error => Assert.Fail(error));
            while (claim.MoveNext()) { }
            Assert.AreEqual(0, gained);
        }
        /// <summary>방문 복귀와 새 요청 이후 이전 응답이 장면을 변경하지 않는지 검증합니다.</summary>
        [Test]
        public void LeavingAndNewerRequestsRejectLateResponses()
        {
            var session = new ExhibitionVisitSession();
            int old = session.Begin("OLD");
            int latest = session.Begin("NEW");
            Assert.IsTrue(session.Complete(latest, MockExhibitionBackend.CreateSnapshot("NEW", ExhibitionPreviewScenario.Normal)));
            Assert.IsFalse(session.Complete(old, MockExhibitionBackend.CreateSnapshot("OLD", ExhibitionPreviewScenario.Normal)));
            Assert.AreEqual("NEW", session.Scene.Snapshot.OwnerCode);
            session.Leave();
            Assert.IsFalse(session.Complete(latest, MockExhibitionBackend.CreateSnapshot("NEW", ExhibitionPreviewScenario.Normal)));
            Assert.IsNull(session.Scene);
            Assert.IsFalse(session.CanEdit);
        }

        /// <summary>같은 친구 재시도의 실패·소유자 오류가 기존 장면을 보존하는지 검증합니다.</summary>
        [Test]
        public void FailedOrWrongOwnerResponsesPreserveCurrentScene()
        {
            var session = new ExhibitionVisitSession();
            int ticket = session.Begin("A");
            Assert.IsTrue(session.Complete(ticket, MockExhibitionBackend.CreateSnapshot("A", ExhibitionPreviewScenario.Normal)));
            var scene = session.Scene;
            ticket = session.Begin("A");
            Assert.IsFalse(session.Complete(ticket, MockExhibitionBackend.CreateSnapshot("C", ExhibitionPreviewScenario.Normal)));
            Assert.AreSame(scene, session.Scene);
            ticket = session.Begin("A");
            Assert.IsFalse(session.Complete(ticket, MockExhibitionBackend.CreateSnapshot("A", ExhibitionPreviewScenario.Mismatch)));
            Assert.AreSame(scene, session.Scene);
            Assert.IsNotEmpty(session.Error);
        }

        /// <summary>다른 친구 방문 실패 후 이전 친구의 장면과 조작이 남지 않는지 검증합니다.</summary>
        [Test]
        public void FailedVisitToDifferentFriendCannotExposePreviousFriendsActions()
        {
            var session = new ExhibitionVisitSession();
            int ticket = session.Begin("A");
            Assert.IsTrue(session.Complete(ticket, MockExhibitionBackend.CreateSnapshot("A", ExhibitionPreviewScenario.Normal)));
            ticket = session.Begin("B");
            Assert.IsNull(session.Scene);
            session.Fail(ticket, "연결 실패");
            Assert.IsNull(session.Scene); Assert.IsFalse(session.Loading);
        }

        /// <summary>빈 전시·미지원 객체·잘못된 배치·중복 배치의 재현과 경고를 검증합니다.</summary>
        [TestCase(ExhibitionPreviewScenario.Empty, 0, 0)]
        [TestCase(ExhibitionPreviewScenario.Unknown, 6, 1)]
        [TestCase(ExhibitionPreviewScenario.Invalid, 5, 1)]
        [TestCase(ExhibitionPreviewScenario.Duplicate, 5, 1)]
        public void SpecialSnapshotsAreReconstructedWithoutInvalidObjects(ExhibitionPreviewScenario scenario, int count, int warnings)
        {
            var scene = new ExhibitionVisitScene(MockExhibitionBackend.CreateSnapshot("A", scenario));
            Assert.AreEqual(count, scene.Objects.Count);
            Assert.AreEqual(warnings, scene.Warnings.Count);
            if (scenario == ExhibitionPreviewScenario.Unknown) Assert.IsTrue(scene.Objects.Single(o => o.Placement.Id == 999).Unknown);
        }

        /// <summary>방문 NPC 이동의 범위·충돌 회피와 원본 배치 불변성을 검증합니다.</summary>
        [Test]
        public void RuntimeWalkingAvoidsBuildingsAndNeverMutatesSnapshotAnchors()
        {
            var snapshot = MockExhibitionBackend.CreateSnapshot("A", ExhibitionPreviewScenario.Normal);
            var saved = snapshot.Placements.Select(p => p.Anchor).ToArray();
            var scene = new ExhibitionVisitScene(snapshot);
            for (int i = 0; i < 100; i++)
            {
                scene.Walk(i);
                foreach (var obj in scene.Objects)
                {
                    Assert.That(obj.Cell.x, Is.InRange(0, 6)); Assert.That(obj.Cell.y, Is.InRange(0, 6));
                    if (!obj.Placement.IsBuilding)
                        Assert.IsFalse(scene.Objects.Any(b => b.Placement.IsBuilding && b.Cell == obj.Cell));
                }
                Assert.AreEqual(scene.Objects.Count, scene.Objects.Select(o => o.Cell).Distinct().Count());
            }
            CollectionAssert.AreEqual(saved, snapshot.Placements.Select(p => p.Anchor));
            Assert.IsTrue(scene.Objects.Any(o => !o.Placement.IsBuilding && o.Cell != o.Placement.Anchor));
            var again = new ExhibitionVisitScene(snapshot);
            CollectionAssert.AreEqual(saved, again.Objects.Select(o => o.Cell));
        }
    }
}
