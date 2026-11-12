using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public class MinigameGrid
    {
        public int Width { get; }
        public int Height { get; }

        private readonly Tile[,] _tiles;
        
        public IReadOnlyList<PolyominoGroup> PolyominoGroups { get; }

        public MinigameGrid(Tile[,] tiles, IReadOnlyList<PolyominoGroup> groups)
        {
            _tiles = tiles;
            Width = tiles.GetLength(0);
            Height = tiles.GetLength(1);
            PolyominoGroups = new List<PolyominoGroup>(groups);
        }

        public Tile GetTile(int x, int y)
        {
            if (!InBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x),
                    $"({x}, {y})는 잘못된 좌표입니다. {Width}x{Height} 범위 안에서 타일을 조회해주세요.");
            }
            
            return _tiles[x, y];
        }

        public IReadOnlyList<Vector2Int> GetRow(int y)
        {
            List<Vector2Int> coordinates = new List<Vector2Int>();
            
            for (int i = 0; i < Width; i++) {
                coordinates.Add(new Vector2Int(i, y));
            }
            
            return coordinates;
        }
        
        public IReadOnlyList<Vector2Int> GetColumn(int x)
        {
            List<Vector2Int> coordinates = new List<Vector2Int>();
            
            for (int i = 0; i < Height; i++) {
                coordinates.Add(new Vector2Int(x, i));
            }
            
            return coordinates;
        }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>
        /// 좌표가 어느 폴리오미노의 몇 번째 칸인지 찾는다.
        /// </summary>
        /// <param name="coordinate">찾을 그리드 좌표.</param>
        /// <param name="group">좌표가 속한 폴리오미노. 없으면 default.</param>
        /// <param name="cellIndex"><see cref="PolyominoShapes.Definitions"/> 기준의 칸 인덱스. 없으면 -1.</param>
        /// <returns>좌표가 폴리오미노에 속하면 true.</returns>
        public bool TryGetPolyominoCell(Vector2Int coordinate, out PolyominoGroup group, out int cellIndex)
        {
            foreach (PolyominoGroup candidate in PolyominoGroups)
            {
                for (int i = 0; i < candidate.Coordinates.Count; i++)
                {
                    if (candidate.Coordinates[i] == coordinate)
                    {
                        group = candidate;
                        cellIndex = i;
                        return true;
                    }
                }
            }

            group = default;
            cellIndex = -1;
            return false;
        }

        public bool IsExcavated(PolyominoGroup group)
        {
            return group.Coordinates.All(coord => GetTile(coord.x, coord.y).IsRevealed);
        }

        public bool AreAllPolyominoesExcavated()
        {
            return PolyominoGroups.All(IsExcavated);
        }
        
        public IEnumerable<Vector2Int> AllCoordinates()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }
    }
}