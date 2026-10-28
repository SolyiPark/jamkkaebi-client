using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class PolyominoSetRuleTests
    {
        [Test]
        public void Constructor_ValidArguments_StoresValues()
        {
            // Arrange
            List<PolyominoShape> pool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            
            // Act
            PolyominoSetRule rule = new PolyominoSetRule(2, 4, pool);
            
            // Assert
            Assert.AreEqual(2, rule.MinTileSum);
            Assert.AreEqual(4, rule.MaxTileSum);
            CollectionAssert.AreEqual(pool, rule.ShapePool);
        }

        [Test]
        public void Constructor_PoolModifiedAfterCreation_DoesNotAffectRule()
        {
            // Arrange
            List<PolyominoShape> pool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            int originalCount = pool.Count; 
            PolyominoSetRule rule = new PolyominoSetRule(2, 4, pool);
            
            // Act
            pool.Add(PolyominoShape.Square4);
            
            // Assert
            Assert.AreEqual(originalCount, rule.ShapePool.Count);
        }

        [Test]
        public void Constructor_BoundaryValues_DoesNotThrow()
        {
            // Arrange
            List<PolyominoShape> pool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            
            // Act & Assert
            Assert.DoesNotThrow(() => new PolyominoSetRule(1, 4, pool));
            Assert.DoesNotThrow(() => new PolyominoSetRule(3, 3, pool));
        }

        [Test]
        public void Constructor_NullShapePool_ThrowsArgumentNullException()
        {
            // Arrange
            List<PolyominoShape> pool = null;
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PolyominoSetRule(2, 4, pool));
        }

        [Test]
        public void Constructor_EmptyShapePool_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new PolyominoSetRule(1, 4, new List<PolyominoShape>()));
        }

        [Test]
        public void Constructor_MinTileSumLowerThan1_ThrowsArgumentException()
        {
            // Arrange
            List<PolyominoShape> pool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new PolyominoSetRule(0, 4, pool));
            Assert.Throws<ArgumentException>(() => new PolyominoSetRule(-1, 4, pool));
        }

        [Test]
        public void Constructor_MaxTileSumLowerThanMinTileSum_ThrowsArgumentException()
        {
            // Arrange
            List<PolyominoShape> pool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new PolyominoSetRule(5, 4, pool));
        }

        [Test]
        public void Constructor_PoolHasDuplicateValue_ThrowsArgumentException()
        {
            // Arrange
            List<PolyominoShape> poolA = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            List<PolyominoShape> poolB = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3, PolyominoShape.Dot };
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new PolyominoSetRule(1, 4, poolA));
            Assert.Throws<ArgumentException>(() => new PolyominoSetRule(1, 4, poolB));
        }
    }
}