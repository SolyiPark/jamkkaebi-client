using System;
using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameDifficultyTableTests
    {
        private const string LocalTablePath = "Assets/Jamkkaebi/ScriptableObjects/LocalMinigameDifficultyTable.asset";
        private const int SeedCount = 200;

        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _createdObjects)
            {
                Object.DestroyImmediate(o);
            }

            _createdObjects.Clear();
        }

        // ==============================
        //      룩업 동작
        // ==============================

        [Test]
        public void GetConfig_RegisteredDifficulty_ReturnsMatchingBoard()
        {
            // Arrange: 난이도마다 가로 크기를 다르게 해서 바꿔치기를 잡는다
            MinigameDifficultyTable table = CreateTable(
                CreateEntry(MinigameDifficulty.VeryEasy, 5),
                CreateEntry(MinigameDifficulty.Normal, 7),
                CreateEntry(MinigameDifficulty.Hard, 8));

            // Act
            MinigameBoardConfig config = table.GetConfig(MinigameDifficulty.Normal);

            // Assert
            Assert.AreEqual(7, config.Width);
        }

        [Test]
        public void GetConfig_AnyEntry_UsesFixedAllowedThreatHits()
        {
            // Arrange
            MinigameDifficultyTable table = CreateTable(CreateEntry(MinigameDifficulty.Normal, 7));

            // Act
            MinigameBoardConfig config = table.GetConfig(MinigameDifficulty.Normal);

            // Assert
            Assert.AreEqual(MinigameRules.AllowedThreatHits, config.AllowedThreatHits);
        }

        [Test]
        public void GetConfig_UnregisteredDifficulty_ThrowsInvalidOperationException()
        {
            // Arrange
            MinigameDifficultyTable table = CreateTable(CreateEntry(MinigameDifficulty.Normal, 7));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => table.GetConfig(MinigameDifficulty.Hard));
        }

        [Test]
        public void GetConfig_DuplicateDifficulty_ThrowsInvalidOperationException()
        {
            // Arrange
            MinigameDifficultyTable table = CreateTable(
                CreateEntry(MinigameDifficulty.Normal, 7),
                CreateEntry(MinigameDifficulty.Normal, 8));

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => table.GetConfig(MinigameDifficulty.Normal));
        }

        [Test]
        public void GetConfig_EmptyTable_ThrowsInvalidOperationException()
        {
            // Arrange
            MinigameDifficultyTable table = CreateTable();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => table.GetConfig(MinigameDifficulty.Normal));
        }

        // ==============================
        //      로컬 테이블 에셋 검증
        // ==============================

        [TestCase(MinigameDifficulty.VeryEasy)]
        [TestCase(MinigameDifficulty.Easy)]
        [TestCase(MinigameDifficulty.Normal)]
        [TestCase(MinigameDifficulty.Hard)]
        public void LocalTable_EveryDifficulty_GeneratesGridForAnySeed(MinigameDifficulty difficulty)
        {
            // Arrange
            MinigameDifficultyTable table = AssetDatabase.LoadAssetAtPath<MinigameDifficultyTable>(LocalTablePath);
            Assert.NotNull(table, $"{LocalTablePath}에서 테이블을 찾지 못했습니다.");
            MinigameBoardConfig config = table.GetConfig(difficulty);

            for (long seed = 0; seed < SeedCount; seed++)
            {
                // Act
                MinigameGrid grid = null;
                Assert.DoesNotThrow(() => grid = MinigameGridGenerator.Generate(config, seed), $"seed: {seed}");

                // Assert
                Assert.AreEqual(config.Width, grid.Width, $"seed: {seed}");
                Assert.AreEqual(config.Height, grid.Height, $"seed: {seed}");
                Assert.AreEqual(config.ShapeCount, grid.PolyominoGroups.Count, $"seed: {seed}");
                Assert.AreEqual(config.BonusTileCount, CountBonus(grid), $"seed: {seed}");
            }
        }

        // ==============
        //      헬퍼들
        // ==============

        private static int CountBonus(MinigameGrid grid)
        {
            int count = 0;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (grid.GetTile(x, y).Content == TileContent.Bonus)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private MinigameDifficultyTable CreateTable(params MinigameDifficultyEntry[] entries)
        {
            MinigameDifficultyTable table = ScriptableObject.CreateInstance<MinigameDifficultyTable>();
            SetPrivateField(table, "_entries", entries);
            _createdObjects.Add(table);
            return table;
        }

        private static MinigameDifficultyEntry CreateEntry(MinigameDifficulty difficulty, int width)
        {
            MinigameBoardConfigData board = new MinigameBoardConfigData();
            SetPrivateField(board, "_width", width);

            MinigameDifficultyEntry entry = new MinigameDifficultyEntry();
            SetPrivateField(entry, "_difficulty", difficulty);
            SetPrivateField(entry, "_board", board);
            return entry;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
            field.SetValue(target, value);
        }
    }
}
