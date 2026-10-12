using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameGridGeneratorTests
    {
        MinigamePhaseConfig config = new MinigamePhaseConfig(
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
        
        MinigamePhaseConfig oneShapeConfig = new MinigamePhaseConfig(
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

        [Test]
        public void Generate_SameSeed_CreatesSameGrid()
        {
            // Arrange & Act
            long seed = 12345678;
            MinigameGrid a = MinigameGridGenerator.Generate(config, seed);
            MinigameGrid b = MinigameGridGenerator.Generate(config, seed);
            
            // Assert
            Assert.IsTrue(AreSameGrid(a, b));
        }

        [Test]
        public void Generate_DifferentSeed_CreatesDifferentGrid()
        {
            long seed = 12345678;
            MinigameGrid target = MinigameGridGenerator.Generate(config, seed);

            bool foundDifferentGrid = false;
            
            for (int i = 0; i < 20; i++)
            {
                MinigameGrid source = MinigameGridGenerator.Generate(config, seed + i + 1);
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
                Assert.DoesNotThrow(() => MinigameGridGenerator.Generate(oneShapeConfig, i), $"seed: {i}");
            }
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