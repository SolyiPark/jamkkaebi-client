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
            bonusTileCount: 0,
            reinforcedOnShapeCount: 2,
            reinforcedOnFieldCount: 1,
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
            bonusTileCount: 0,
            reinforcedOnShapeCount: 2,
            reinforcedOnFieldCount: 1,
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
                new MinigameBoardConfig(3, 3, 2, 7, 5, 2, 0, 2, 1, 19, 3, 3);
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => MinigameGridGenerator.Generate(smallGridConfig, seed));
        }

        [TestCase(0, 9)]
        [TestCase(-1, 9)]
        [TestCase(3, 0)]
        [TestCase(3, -1)]
        public void Generate_NonPositiveShapeCountOrCellTotal_ThrowsArgumentException(int shapeCount, int shapeCellTotal)
        {
            // Arrange: _config에서 형상 수 또는 칸 합 하나만 바꿔 다른 값이 예외 원인이 되지 않게 한다
            long seed = 12345678;
            MinigameBoardConfig invalidConfig = new MinigameBoardConfig(
                _config.Width, _config.Height, shapeCount, shapeCellTotal,
                _config.ThreatTileCount, _config.HelperTileCount, _config.BonusTileCount, _config.ReinforcedOnShapeCount, _config.ReinforcedOnFieldCount,
                _config.DestructiveToolLimit, _config.ScoutToolLimit, _config.AllowedThreatHits);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MinigameGridGenerator.Generate(invalidConfig, seed));
        }

        [Test]
        public void Generate_NoShapeSetMatchesCountAndTotal_ThrowsInvalidOperationException()
        {
            // Arrange: 서로 다른 모양 3개의 칸 합은 최소 2+3+4=9라서 6칸은 만들 수 없다(서버 수정 전 매우 쉬움 값)
            long seed = 12345678;
            MinigameBoardConfig impossibleConfig = new MinigameBoardConfig(5, 6, 3, 6, 2, 1, 0, 1, 1, 12, 3, 3);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => MinigameGridGenerator.Generate(impossibleConfig, seed));
        }

        // ==============================
        //      강화 타일 배치
        // ==============================

        [Test]
        public void Generate_AnySeed_ReinforcesShapeAndPlainCellsByCount()
        {
            // Arrange
            long seed = 12345678;

            for (int i = 0; i < 50; i++)
            {
                // Act
                MinigameGrid grid = MinigameGridGenerator.Generate(_config, seed + i);

                // Assert
                Assert.AreEqual(_config.ReinforcedOnShapeCount, CountReinforced(grid, TileContent.Target), $"seed: {seed + i}");
                Assert.AreEqual(_config.ReinforcedOnFieldCount, CountReinforced(grid, TileContent.Empty), $"seed: {seed + i}");
                Assert.AreEqual(0, CountReinforced(grid, TileContent.Threat), $"seed: {seed + i}");
                Assert.AreEqual(0, CountReinforced(grid, TileContent.Helper), $"seed: {seed + i}");
            }
        }

        [Test]
        public void Generate_ReinforceEveryShapeAndPlainCell_DoesNotThrow()
        {
            // Arrange: 경계값. 7*8 그리드에서 목표 9칸, 일반 칸 56 - 9 - 5 - 2 = 40칸을 모두 강화한다
            long seed = 12345678;
            MinigameBoardConfig fullConfig = WithReinforced(9, 40);

            // Act
            MinigameGrid grid = MinigameGridGenerator.Generate(fullConfig, seed);

            // Assert
            Assert.AreEqual(9, CountReinforced(grid, TileContent.Target));
            Assert.AreEqual(40, CountReinforced(grid, TileContent.Empty));
        }

        [TestCase(10, 1)]   // 형상 안 강화가 목표 9칸을 넘음
        [TestCase(2, 41)]   // 형상 밖 강화가 일반 칸 40칸을 넘음
        public void Generate_ReinforcedCountExceedsCandidateCells_ThrowsInvalidOperationException(int onShape, int onField)
        {
            // Arrange
            long seed = 12345678;
            MinigameBoardConfig overConfig = WithReinforced(onShape, onField);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => MinigameGridGenerator.Generate(overConfig, seed));
        }

        [TestCase(-1, 1)]   // 형상 안 강화가 음수
        [TestCase(2, -1)]   // 형상 밖 강화가 음수
        public void Generate_NegativeReinforcedCount_ThrowsArgumentException(int onShape, int onField)
        {
            // Arrange
            long seed = 12345678;
            MinigameBoardConfig negativeConfig = WithReinforced(onShape, onField);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MinigameGridGenerator.Generate(negativeConfig, seed));
        }

        // ==============================
        //      정성 보너스 타일 배치
        // ==============================

        [Test]
        public void Generate_AnySeed_PlacesBonusTilesWithoutReplacingOtherTiles()
        {
            // Arrange
            long seed = 12345678;
            MinigameBoardConfig bonusConfig = WithBonus(3, _config.ReinforcedOnFieldCount);

            for (int i = 0; i < 50; i++)
            {
                // Act
                MinigameGrid grid = MinigameGridGenerator.Generate(bonusConfig, seed + i);

                // Assert: 보너스 수가 맞고, 목표·위협·도움 칸 수는 그대로다
                Assert.AreEqual(3, CountContent(grid, TileContent.Bonus), $"seed: {seed + i}");
                Assert.AreEqual(bonusConfig.ShapeCellTotal, CountContent(grid, TileContent.Target), $"seed: {seed + i}");
                Assert.AreEqual(bonusConfig.ThreatTileCount, CountContent(grid, TileContent.Threat), $"seed: {seed + i}");
                Assert.AreEqual(bonusConfig.HelperTileCount, CountContent(grid, TileContent.Helper), $"seed: {seed + i}");
            }
        }

        [Test]
        public void Generate_SameSeedWithBonus_CreatesSameGrid()
        {
            // Arrange: 보너스 배치와 형상 밖 강화가 모두 난수를 소비하는 설정
            long seed = 12345678;
            MinigameBoardConfig bonusConfig = WithBonus(3, 2);

            // Act
            MinigameGrid a = MinigameGridGenerator.Generate(bonusConfig, seed);
            MinigameGrid b = MinigameGridGenerator.Generate(bonusConfig, seed);

            // Assert: 칸 종류(보너스 위치 포함)와 강화 상태가 모두 같다
            Assert.IsTrue(AreSameGrid(a, b));
        }

        [Test]
        public void Generate_ReinforceEveryPlainCell_AlsoReinforcesBonusTiles()
        {
            // Arrange: 일반 칸 40칸을 모두 강화하면, 그중 보너스 3칸도 강화되어야 한다(겹침 허용)
            long seed = 12345678;
            MinigameBoardConfig overlapConfig = WithBonus(3, 40);

            // Act
            MinigameGrid grid = MinigameGridGenerator.Generate(overlapConfig, seed);

            // Assert
            Assert.AreEqual(3, CountReinforced(grid, TileContent.Bonus));
            Assert.AreEqual(37, CountReinforced(grid, TileContent.Empty));
        }

        [Test]
        public void Generate_BonusFillsEveryPlainCell_DoesNotThrow()
        {
            // Arrange: 경계값. 일반 칸 40칸을 모두 보너스로 채운다
            long seed = 12345678;
            MinigameBoardConfig fullConfig = WithBonus(40, 0);

            // Act
            MinigameGrid grid = MinigameGridGenerator.Generate(fullConfig, seed);

            // Assert
            Assert.AreEqual(40, CountContent(grid, TileContent.Bonus));
            Assert.AreEqual(0, CountContent(grid, TileContent.Empty));
        }

        [Test]
        public void Generate_BonusExceedsPlainCells_ThrowsInvalidOperationException()
        {
            // Arrange
            long seed = 12345678;
            MinigameBoardConfig overConfig = WithBonus(41, 0);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => MinigameGridGenerator.Generate(overConfig, seed));
        }

        [Test]
        public void Generate_NegativeBonusCount_ThrowsArgumentException()
        {
            // Arrange
            long seed = 12345678;
            MinigameBoardConfig negativeConfig = WithBonus(-1, 0);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MinigameGridGenerator.Generate(negativeConfig, seed));
        }

        private MinigameBoardConfig WithBonus(int bonus, int onField)
        {
            return new MinigameBoardConfig(
                _config.Width, _config.Height, _config.ShapeCount, _config.ShapeCellTotal,
                _config.ThreatTileCount, _config.HelperTileCount, bonus, _config.ReinforcedOnShapeCount, onField,
                _config.DestructiveToolLimit, _config.ScoutToolLimit, _config.AllowedThreatHits);
        }

        private static int CountContent(MinigameGrid grid, TileContent content)
        {
            int count = 0;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid.GetTile(x, y).Content == content)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private MinigameBoardConfig WithReinforced(int onShape, int onField)
        {
            return new MinigameBoardConfig(
                _config.Width, _config.Height, _config.ShapeCount, _config.ShapeCellTotal,
                _config.ThreatTileCount, _config.HelperTileCount, _config.BonusTileCount, onShape, onField,
                _config.DestructiveToolLimit, _config.ScoutToolLimit, _config.AllowedThreatHits);
        }

        private static int CountReinforced(MinigameGrid grid, TileContent content)
        {
            int count = 0;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Tile tile = grid.GetTile(x, y);
                    if (tile.IsReinforced && tile.Content == content)
                    {
                        count++;
                    }
                }
            }

            return count;
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