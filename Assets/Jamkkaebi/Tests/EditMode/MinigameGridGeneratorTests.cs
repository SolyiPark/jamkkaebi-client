using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameGridGeneratorTests
    {
        MinigameBoardConfig _config = new MinigameBoardConfig(
            width: 7,
            height: 8,
            shapeCount: 3,
            shapeCellTotal: 9,
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            destructiveToolLimit: 19,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );
        
        MinigameBoardConfig _oneShapeConfig = new MinigameBoardConfig(
            width: 7,
            height: 3,
            shapeCount: 1,
            shapeCellTotal: 5,      // 풀에서 5칸짜리는 Line5뿐이다
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            destructiveToolLimit: 19,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );
        
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
        public void Generate_AnySeed_PicksDistinctShapesMatchingConfig()
        {
            // Arrange
            long seed = 12345678;
            
            for (int i = 0; i < 50; i++) {
                // Act
                MinigameGrid grid = MinigameGridGenerator.Generate(_config, seed + i);

                // Assert
                Assert.AreEqual(_config.ShapeCount, grid.PolyominoGroups.Count, $"seed: {seed + i}");

                List<PolyominoShape> shapes = new List<PolyominoShape>();
                int tileSum = 0;
                foreach (PolyominoGroup group in grid.PolyominoGroups)
                {
                    shapes.Add(group.Shape);
                    tileSum += group.Coordinates.Count;
                }
                
                CollectionAssert.AllItemsAreUnique(shapes, $"seed: {seed + i}");
                CollectionAssert.IsSubsetOf(shapes, MinigameRules.ShapePool, $"seed: {seed + i}");
                Assert.AreEqual(_config.ShapeCellTotal, tileSum, $"seed: {seed + i}");
            }
        }

        [Test]
        public void Generate_RequiredTileExceedsGridTileCount_ThrowsInvalidOperationException()
        {
            // Arrange
            long seed = 12345678;
            MinigameBoardConfig smallGridConfig =
                new MinigameBoardConfig(3, 3, 2, 7, 5, 2, 3, 19, 3, 3);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => MinigameGridGenerator.Generate(smallGridConfig, seed));
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
    }
}