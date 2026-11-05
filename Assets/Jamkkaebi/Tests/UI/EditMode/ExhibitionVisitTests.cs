using System.Linq;
using Jamkkaebi.Scripts.UI.Friends;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
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

        /// <summary>지원 여부는 객체 ID의 크기나 서버 모드가 아니라 주입된 카탈로그 조회 결과를 따릅니다.</summary>
        [Test]
        public void KnownObjectsFollowInjectedCatalogIncludingHighAndUnregisteredIds()
        {
            var snapshot = new ExhibitionSnapshot("A", new[]
            {
                new SnapshotPlacement(false, 8, "말발굽", new Vector2Int(1, 1)),
                new SnapshotPlacement(false, 1, "미등록", new Vector2Int(2, 2)),
                new SnapshotPlacement(true, 99, "등록 건물", new Vector2Int(3, 3))
            });
            var scene = new ExhibitionVisitScene(snapshot, (building, id) => building ? id == 99 : id == 8);
            Assert.IsFalse(scene.Objects[0].Unknown);
            Assert.IsTrue(scene.Objects[1].Unknown);
            Assert.IsFalse(scene.Objects[2].Unknown);
            Assert.AreEqual(1, scene.Warnings.Count);
        }

        /// <summary>원본 겹침을 유지해 경고하되 한 객체가 이동해도 다른 객체의 시작 셀 점유를 잃지 않습니다.</summary>
        [Test]
        public void OverlappingObjectsKeepOtherOccupantsAndReserveEveryStartCell()
        {
            var rows = new[]
            {
                new SnapshotPlacement(true, 1, "건물", new Vector2Int(3, 3)),
                new SnapshotPlacement(false, 1, "겹친 도깨비", new Vector2Int(3, 3)),
                new SnapshotPlacement(false, 2, "인접 도깨비", new Vector2Int(3, 2)),
                new SnapshotPlacement(false, 3, "다른 도깨비", new Vector2Int(2, 3))
            };
            var snapshot = new ExhibitionSnapshot("A", rows);
            var scene = new ExhibitionVisitScene(snapshot, seed: 71);
            Assert.AreEqual(4, scene.Objects.Count);
            Assert.AreEqual(1, scene.Warnings.Count);
            for (int step = 0; step < 100; step++)
            {
                var start = scene.Objects.Select(obj => obj.Cell).ToArray();
                scene.Walk(step);
                for (int i = 0; i < scene.Objects.Count; i++)
                {
                    var obj = scene.Objects[i];
                    if (obj.Cell == start[i]) continue;
                    Assert.IsFalse(start.Where((cell, index) => index != i).Contains(obj.Cell), "보간 중 다른 객체의 시작 셀로 진입했습니다.");
                    Assert.AreEqual(1, scene.Objects.Count(other => other.Cell == obj.Cell));
                }
            }
            CollectionAssert.AreEqual(rows.Select(row => row.Anchor), snapshot.Placements.Select(row => row.Anchor));
        }

        /// <summary>같은 시드·걸음은 동일한 이동을 만들고 시드가 바뀌면 경로도 달라집니다.</summary>
        [Test]
        public void WalkingIsReproducibleForTheSameSeed()
        {
            var snapshot = MockExhibitionBackend.CreateSnapshot("A", ExhibitionPreviewScenario.Normal);
            var first = new ExhibitionVisitScene(snapshot, seed: 12);
            var same = new ExhibitionVisitScene(snapshot, seed: 12);
            var different = new ExhibitionVisitScene(snapshot, seed: 99);
            bool diverged = false;
            for (int step = 0; step < 20; step++)
            {
                first.Walk(step); same.Walk(step); different.Walk(step);
                CollectionAssert.AreEqual(first.Objects.Select(obj => obj.Cell), same.Objects.Select(obj => obj.Cell));
                diverged |= !first.Objects.Select(obj => obj.Cell).SequenceEqual(different.Objects.Select(obj => obj.Cell));
            }
            Assert.IsTrue(diverged);
        }
    }
}
