using MobilePrototype.Exhibition;
using NUnit.Framework;
using UnityEngine;

namespace MobilePrototype.Tests.EditMode
{
    public sealed class HousingLayoutTests
    {
        private ExhibitionGrid _grid;
        private HousingLayout _layout;

        [SetUp]
        public void SetUp()
        { _grid = ScriptableObject.CreateInstance<ExhibitionGrid>(); _layout = new HousingLayout(_grid); }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_grid);

        [Test]
        public void ItemIdentitySurvivesEditingAndDistinctInstances()
        {
            _layout.TryPlace("catalog-item", new Vector2Int(2, 2), new Vector2Int(2, 1), out var item);
            _layout.TryPlace("catalog-item", new Vector2Int(5, 5), Vector2Int.one, out var other);
            Assert.That(other.Id, Is.Not.EqualTo(item.Id));
            Assert.That(_layout.TryMove(item.Id, new Vector2Int(1, 2)), Is.True);
            Assert.That(_layout.TryRotate(item.Id), Is.True);
            Assert.That(item.ItemId, Is.EqualTo("catalog-item"));
            Assert.That(other.ItemId, Is.EqualTo(item.ItemId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void MissingItemIdentityRejectsPlacement(string itemId)
        {
            Assert.That(_layout.TryPlace(itemId, Vector2Int.zero, Vector2Int.one, out _), Is.False);
            Assert.That(_layout.Placements, Is.Empty);
        }

        [Test]
        public void MultiCellPlacementRejectsOverlapAndBounds()
        {
            Assert.That(_layout.TryPlace("test-item", new Vector2Int(2, 2), new Vector2Int(2, 2), out var item), Is.True);
            Assert.That(_layout.At(new Vector2Int(3, 3)), Is.SameAs(item));
            Assert.That(_layout.TryPlace("test-item", new Vector2Int(3, 3), Vector2Int.one, out _), Is.False);
            Assert.That(_layout.TryPlace("test-item", new Vector2Int(6, 6), new Vector2Int(2, 2), out _), Is.False);
            Assert.That(_layout.TryPlace("test-item", Vector2Int.zero, Vector2Int.zero, out _), Is.False);
        }

        [Test]
        public void InvalidMoveKeepsOldFootprintAndValidMoveReleasesIt()
        {
            _layout.TryPlace("test-item", new Vector2Int(2, 2), new Vector2Int(2, 2), out var item);
            _layout.TryPlace("test-item", new Vector2Int(4, 4), Vector2Int.one, out _);
            Assert.That(_layout.TryMove(item.Id, new Vector2Int(3, 3)), Is.False);
            Assert.That(item.Cell, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(_layout.TryMove(item.Id, new Vector2Int(1, 2)), Is.True);
            Assert.That(_layout.At(new Vector2Int(3, 3)), Is.Null);
            Assert.That(_layout.At(new Vector2Int(1, 3)), Is.SameAs(item));
        }

        [Test]
        public void RemovalReleasesEveryCellAndIdsAreNotReused()
        {
            _layout.TryPlace("test-item", Vector2Int.zero, new Vector2Int(2, 2), out var item);
            Assert.That(_layout.Remove(item.Id), Is.True);
            Assert.That(_layout.Remove(item.Id), Is.False);
            Assert.That(_layout.TryPlace("test-item", Vector2Int.zero, new Vector2Int(2, 2), out var replacement), Is.True);
            Assert.That(replacement.Id, Is.GreaterThan(item.Id));
        }

        [Test]
        public void WalkerReservationsBlockPlacementAndMoveWithoutLosingOldReservation()
        {
            _layout.TryPlace("test-item", new Vector2Int(2, 2), Vector2Int.one, out var item);
            Assert.That(_layout.TryReserveTraversal(Vector2Int.zero, Vector2Int.right), Is.True);
            Assert.That(_layout.CanPlace(Vector2Int.zero, Vector2Int.one), Is.False);
            Assert.That(_layout.TryMove(item.Id, Vector2Int.right), Is.False);
            Assert.That(_layout.TryReserveTraversal(Vector2Int.right, item.Cell), Is.False);
            Assert.That(_layout.CanPlace(Vector2Int.zero, Vector2Int.one), Is.False);
            _layout.ClearTraversal();
            Assert.That(_layout.CanPlace(Vector2Int.zero, Vector2Int.one), Is.True);
        }

        [Test]
        public void RotationRejectsOverlapAndPreservesOrientationThenReleasesOldFootprint()
        {
            _layout.TryPlace("test-item", new Vector2Int(2, 2), new Vector2Int(2, 1), out var item);
            _layout.TryPlace("test-item", new Vector2Int(2, 3), Vector2Int.one, out var blocker);
            Assert.That(_layout.TryRotate(item.Id), Is.False);
            Assert.That(item.QuarterTurns, Is.Zero);
            Assert.That(item.Size, Is.EqualTo(new Vector2Int(2, 1)));
            _layout.Remove(blocker.Id);
            Assert.That(_layout.TryRotate(item.Id), Is.True);
            Assert.That(_layout.At(new Vector2Int(3, 2)), Is.Null);
            Assert.That(_layout.At(new Vector2Int(2, 3)), Is.SameAs(item));
            for (int i = 0; i < 3; i++) Assert.That(_layout.TryRotate(item.Id), Is.True);
            Assert.That(item.QuarterTurns, Is.Zero);
            Assert.That(item.Size, Is.EqualTo(item.BaseSize));
        }

        [Test]
        public void RotationRejectsBoundaryAndTraversalWithoutChangingPlacement()
        {
            _layout.TryPlace("test-item", new Vector2Int(5, 6), new Vector2Int(2, 1), out var edge);
            Assert.That(_layout.TryRotate(edge.Id), Is.False);
            _layout.TryPlace("test-item", new Vector2Int(2, 2), new Vector2Int(2, 1), out var item);
            _layout.TryReserveTraversal(new Vector2Int(2, 3), new Vector2Int(2, 3));
            Assert.That(_layout.TryRotate(item.Id), Is.False);
            Assert.That(item.QuarterTurns, Is.Zero);
            Assert.That(item.Cell, Is.EqualTo(new Vector2Int(2, 2)));
        }

        [Test]
        public void WalkerCannotCrossDiagonalOccupiedOrOutsideCells()
        {
            _layout.TryPlace("test-item", Vector2Int.up, Vector2Int.one, out _);
            Assert.That(_layout.TryReserveTraversal(Vector2Int.zero, Vector2Int.one), Is.False);
            Assert.That(_layout.TryReserveTraversal(Vector2Int.zero, Vector2Int.up), Is.False);
            Assert.That(_layout.TryReserveTraversal(Vector2Int.zero, Vector2Int.left), Is.False);
            Assert.That(_layout.TryReserveTraversal(Vector2Int.zero, Vector2Int.zero), Is.True);
        }
    }
}
