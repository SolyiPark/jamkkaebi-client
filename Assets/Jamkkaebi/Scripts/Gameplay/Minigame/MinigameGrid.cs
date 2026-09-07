using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
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
            PolyominoGroups = groups;
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

        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }
    }
}