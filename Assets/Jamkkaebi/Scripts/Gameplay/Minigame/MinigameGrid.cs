using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    
    public class MinigameGrid
    {
        public int Width { get; }
        public int Height { get; }

        private readonly Tile[,] _tiles;
        
        private readonly Dictionary<int, PolyominoGroup> _polyominoGroups;

        public MinigameGrid(Tile[,] tiles, Dictionary<int, PolyominoGroup> groups)
        {
            _tiles = tiles;
            Width = tiles.GetLength(0);
            Height = tiles.GetLength(1);
            _polyominoGroups = groups;
        }

        public Tile GetTile(int x, int y)
        {
            return _tiles[x, y];
        }

        public IReadOnlyList<Vector2Int> GetGroupCoordinates(int groupId)
        {
            return _polyominoGroups.TryGetValue(groupId, out PolyominoGroup group)
                ? group.Coordinates
                : new List<Vector2Int>();
        }
    }
}