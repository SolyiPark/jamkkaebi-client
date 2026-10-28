using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameGridGeneratorTests
    {
        MinigamePhaseConfig _config = new MinigamePhaseConfig(
            width: 7,
            height: 8,
            shapes: new List<PolyominoShape> { PolyominoShape.Stairs4, PolyominoShape.Line2, PolyominoShape.LShape3 },
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            timeLimit: 60f,
            destructiveToolLimit: 19,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );
        
        MinigamePhaseConfig _oneShapeConfig = new MinigamePhaseConfig(
            width: 7,
            height: 3,
            shapes: new List<PolyominoShape> { PolyominoShape.Line5 },
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            timeLimit: 60f,
            destructiveToolLimit: 19,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );
        
        MinigamePhaseConfig _ruleBasedConfig = new MinigamePhaseConfig(
            width: 7,
            height: 8,
            shapes: new List<PolyominoShape>(),
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            timeLimit: 60f,
            destructiveToolLimit: 19,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );

        private const int ShapeCount = 2;

        [Test]
        public void Generate_SameSeed_CreatesSameGrid()
        {
            // Arrange & Act
            long seed = 12345678;
            MinigameGrid a = MinigameGridGenerator.Generate(_config, seed);
            MinigameGrid b = MinigameGridGenerator.Generate(_config, seed);
            
            // Assert
            Assert.IsTrue(AreSameGrid(a, b));
        }

        [Test]
        public void Generate_DifferentSeed_CreatesDifferentGrid()
        {
            long seed = 12345678;
            MinigameGrid target = MinigameGridGenerator.Generate(_config, seed);

            bool foundDifferentGrid = false;
            
            for (int i = 0; i < 20; i++)
            {
                MinigameGrid source = MinigameGridGenerator.Generate(_config, seed + i + 1);
                if (!AreSameGrid(target, source))
                {
                    foundDifferentGrid = true;
                    break;
                }
            }
            
            Assert.IsTrue(foundDifferentGrid);
        }
        
        [Test]
        public void Generate_ShapeWithOnlyOneValidRotation_NeverThrows()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.DoesNotThrow(() => MinigameGridGenerator.Generate(_oneShapeConfig, i), $"seed: {i}");
            }
        }

        [Test]
        public void GenerateWithRule_SameSeed_ReturnsSameGrid()
        {
            // Arrange
            long seed = 12345678;
            PolyominoSetRule rule = CreateRule();
            
            // Act
            MinigameGrid a = MinigameGridGenerator.Generate(_ruleBasedConfig, rule, ShapeCount, seed);
            MinigameGrid b = MinigameGridGenerator.Generate(_ruleBasedConfig, rule, ShapeCount, seed);
            
            // Assert
            Assert.IsTrue(AreSameGrid(a, b));
        }

        [Test]
        public void GenerateWithRule_AnySeed_FollowsRule()
        {
            // Arrange
            long seed = 12345678;
            PolyominoSetRule rule = CreateRule();
            
            for (int i = 0; i < 50; i++) {
                // Act
                MinigameGrid grid = MinigameGridGenerator.Generate(_ruleBasedConfig, rule, ShapeCount, seed + i);

                // Assert
                Assert.AreEqual(ShapeCount, grid.PolyominoGroups.Count, $"seed: {seed + i}");

                List<PolyominoShape> shapes = new List<PolyominoShape>();
                int tileSum = 0;
                foreach (PolyominoGroup group in grid.PolyominoGroups)
                {
                    shapes.Add(group.Shape);
                    tileSum += group.Coordinates.Count;
                }
                
                CollectionAssert.AllItemsAreUnique(shapes, $"seed: {seed + i}");
                CollectionAssert.IsSubsetOf(shapes, rule.ShapePool, $"seed: {seed + i}");
                Assert.That(tileSum, Is.InRange(rule.MinTileSum, rule.MaxTileSum), $"seed: {seed + i}");
            }
        }

        [Test]
        public void GenerateWithRule_ConfigHasShapes_IgnoresThem()
        {
            // Arrange
            long seed = 12345678;
            PolyominoSetRule rule = new PolyominoSetRule(4, 4,
                new List<PolyominoShape> { PolyominoShape.Dot, PolyominoShape.LShape3 });
            
            // Act
            MinigameGrid grid = MinigameGridGenerator.Generate(_oneShapeConfig, rule, ShapeCount, seed);

            List<PolyominoShape> shapes = new List<PolyominoShape>();
            foreach (PolyominoGroup group in grid.PolyominoGroups)
            {
                shapes.Add(group.Shape);
            }
            
            // Assert
            CollectionAssert.AreEqual(new List<PolyominoShape> { PolyominoShape.Dot, PolyominoShape.LShape3 }, shapes);
        }

        [Test]
        public void GenerateWithRule_RequiredTileExceedsGridTileCount_ThrowsInvalidOperationException()
        {
            // Arrange
            long seed = 12345678;
            PolyominoSetRule rule = CreateRule();
            MinigamePhaseConfig smallGridConfig =
                new MinigamePhaseConfig(3, 3, new List<PolyominoShape>(), 5, 2, 3, 60f, 19, 3, 3);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => MinigameGridGenerator.Generate(smallGridConfig, rule, ShapeCount, seed));
        }

        private bool AreSameGrid(MinigameGrid a, MinigameGrid b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return false;
            }

            for (int i = 0; i < a.Width; i++)
            {
                for (int j = 0; j < a.Height; j++)
                {
                    if (a.GetTile(i, j).Content != b.GetTile(i, j).Content)
                    {
                        return false;
                    }

                    if (a.GetTile(i, j).IsReinforced != b.GetTile(i, j).IsReinforced)
                    {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        private static PolyominoSetRule CreateRule()
        {
            return new PolyominoSetRule(5, 8,
                new List<PolyominoShape>
                {
                    PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3, PolyominoShape.LShape4,
                    PolyominoShape.Stairs4, PolyominoShape.Square4, PolyominoShape.Line5
                });
        }
    }
}