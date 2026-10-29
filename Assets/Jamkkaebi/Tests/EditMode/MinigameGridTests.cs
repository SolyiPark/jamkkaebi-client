using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameGridTests
    {
        // LShape3를 반시계로 한 번 돌린 모양을 (1, 0)에 놓은 3×2 그리드
        //   y=1: [2][1][ ]
        //   y=0: [ ][0][ ]
        private static readonly Vector2Int[] LShapeCoordinates =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1)
        };

        private static MinigameGrid CreateGrid()
        {
            Tile[,] tiles = new Tile[3, 2];
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    tiles[x, y] = new Tile(TileContent.Empty);
                }
            }

            foreach (Vector2Int coord in LShapeCoordinates)
            {
                tiles[coord.x, coord.y] = new Tile(TileContent.Target);
            }

            PolyominoGroup group = new PolyominoGroup(PolyominoShape.LShape3, LShapeCoordinates, rotationCount: 1);
            return new MinigameGrid(tiles, new List<PolyominoGroup> { group });
        }

        [Test]
        public void TryGetPolyominoCell_CoordinateInGroup_ReturnsGroupAndCellIndex()
        {
            MinigameGrid grid = CreateGrid();

            for (int i = 0; i < LShapeCoordinates.Length; i++)
            {
                bool found = grid.TryGetPolyominoCell(LShapeCoordinates[i], out PolyominoGroup group, out int cellIndex);

                Assert.IsTrue(found, $"cell: {i}");
                Assert.AreEqual(PolyominoShape.LShape3, group.Shape);
                Assert.AreEqual(1, group.RotationCount);
                Assert.AreEqual(i, cellIndex);
            }
        }

        [Test]
        public void TryGetPolyominoCell_CoordinateOutsideGroups_ReturnsFalse()
        {
            MinigameGrid grid = CreateGrid();

            bool found = grid.TryGetPolyominoCell(new Vector2Int(2, 0), out _, out int cellIndex);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, cellIndex);
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void PolyominoGroup_RotationCountOutOfRange_ThrowsArgumentOutOfRangeException(int rotationCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PolyominoGroup(PolyominoShape.Line2, new[] { Vector2Int.zero, Vector2Int.up }, rotationCount));
        }
    }
}
