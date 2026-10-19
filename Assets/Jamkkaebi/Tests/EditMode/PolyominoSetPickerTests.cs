using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class PolyominoSetPickerTests
    {
        [Test]
        public void Pick_SameSeed_ReturnsSameSet()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
            {
                PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3, PolyominoShape.LShape4,
                PolyominoShape.Stairs4, PolyominoShape.Square4, PolyominoShape.Line5
            };
            PolyominoSetRule rule = new PolyominoSetRule(12, 14, shapePool);
            
            // Act
            Random rngA = new Random(12345);
            Random rngB = new Random(12345);
            IReadOnlyList<PolyominoShape> setA = PolyominoSetPicker.Pick(rule, 4, rngA);
            IReadOnlyList<PolyominoShape> setB = PolyominoSetPicker.Pick(rule, 4, rngB);
            
            // Assert
            Assert.AreEqual(setA, setB);
        }

        [Test]
        public void Pick_AnySeeds_ReturnsValidSet()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
            {
                PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3, PolyominoShape.LShape4,
                PolyominoShape.Stairs4, PolyominoShape.Square4, PolyominoShape.Line5
            };
            PolyominoSetRule rule = new PolyominoSetRule(12, 14, shapePool);
            
            // Act
            for (int seed = 0; seed < 100; seed++)
            {
                IReadOnlyList<PolyominoShape> set = PolyominoSetPicker.Pick(rule, 4, new Random(seed));
                
                // Assert: 타일 합 범위[Min, Max]를 만족한다
                int sum = 0;
                foreach (PolyominoShape shape in set)
                {
                    sum += PolyominoShapes.Definitions[shape].Count;
                }
                Assert.IsTrue(sum >= rule.MinTileSum && sum <= rule.MaxTileSum, $"추출된 set이 [Min, Max] 범위를 만족하지 않습니다. seed: {seed}");
                
                // Assert: 중복되는 모양이 없다
                Assert.That(set, Is.Unique, $"추출된 set이 중복되는 모양을 포함합니다. seed: {seed}");
                
                // Assert: 결과 개수 = shapeCount
                Assert.AreEqual(4, set.Count, $"추출된 set의 도형 개수가 규칙(shapeCount)과 다릅니다. seed: {seed}");
                
                // Assert: 모든 모양은 풀에 속한다
                Assert.That(set, Is.SubsetOf(shapePool), $"추출된 set에 풀에 속하지 않는 도형이 포함되어 있습니다. seed: {seed}");
            }
        }

        [Test]
        public void Pick_DifferentSeeds_VariousSet()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
            {
                PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3, PolyominoShape.LShape4,
                PolyominoShape.Stairs4, PolyominoShape.Square4, PolyominoShape.Line5
            };
            PolyominoSetRule rule = new PolyominoSetRule(12, 14, shapePool);
            
            HashSet<String> sets = new HashSet<string>();
            
            // Act
            for (int seed = 0; seed < 100; seed++)
            {
                IReadOnlyList<PolyominoShape> set = PolyominoSetPicker.Pick(rule, 4, new Random(seed));
                sets.Add(string.Join(", ", set));
            }
            
            // Assert
            Assert.That(sets.Count, Is.GreaterThan(1), "시드를 바꿔도 항상 같은 세트가 나옵니다.");
        }

        [Test]
        public void Pick_NoCandidate_ThrowsInvalidOperationException()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2 };
            PolyominoSetRule rule = new PolyominoSetRule(12, 14, shapePool);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => PolyominoSetPicker.Pick(rule, 2, new Random(12345)));
        }

        [Test]
        public void Pick_ShapeCountLargerThanShapePool_ThrowsInvalidOperationException()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2 };
            PolyominoSetRule rule = new PolyominoSetRule(1, 3, shapePool);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => PolyominoSetPicker.Pick(rule, 3, new Random(12345)));
        }

        [Test]
        public void Pick_BoundaryTileSum_ReturnsDeterminedSet()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            PolyominoSetRule ruleSum3 = new PolyominoSetRule(3, 3, shapePool);
            PolyominoSetRule ruleSum4 = new PolyominoSetRule(4, 4, shapePool);
            
            // Act
            IReadOnlyList<PolyominoShape> setSum3 = PolyominoSetPicker.Pick(ruleSum3, 2, new Random(12345));
            IReadOnlyList<PolyominoShape> setSum4 = PolyominoSetPicker.Pick(ruleSum4, 2, new Random(12345));
            
            // Assert
            CollectionAssert.AreEqual(new [] {PolyominoShape.Dot, PolyominoShape.Line2}, setSum3);
            CollectionAssert.AreEqual(new [] {PolyominoShape.Dot, PolyominoShape.LShape3}, setSum4);
        }

        [Test]
        public void Pick_BoundaryShapeCount_ReturnsDeterminedSet()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            PolyominoSetRule rule = new PolyominoSetRule(1, 6, shapePool);
            
            // Act
            IReadOnlyList<PolyominoShape> set = PolyominoSetPicker.Pick(rule, shapePool.Count, new Random(12345));
            
            // Assert
            CollectionAssert.AreEqual(new [] {PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3}, set);
        }

        [Test]
        public void Pick_RngNull_ThrowsArgumentNullException()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            PolyominoSetRule rule = new PolyominoSetRule(3, 3, shapePool);
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => PolyominoSetPicker.Pick(rule, 2, null));
        }

        [Test]
        public void Pick_ShapeCountLessThanOrEqualToZero_ThrowsArgumentException()
        {
            // Arrange
            List<PolyominoShape> shapePool = new List<PolyominoShape>
                { PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3 };
            PolyominoSetRule rule = new PolyominoSetRule(3, 3, shapePool);
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PolyominoSetPicker.Pick(rule, 0, new Random(12345)));
            Assert.Throws<ArgumentException>(() => PolyominoSetPicker.Pick(rule, -1, new Random(12345)));
        }

        [Test]
        public void Pick_DefaultRule_ThrowsArgumentException()
        {
            // Arrange
            PolyominoSetRule defaultRule = default(PolyominoSetRule);
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PolyominoSetPicker.Pick(defaultRule, 2, new Random(12345)));
        }
    }
}