using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public readonly struct PolyominoGroup
    {
        public PolyominoShape Shape { get; }
        public IReadOnlyList<Vector2Int> Coordinates { get; }

        public PolyominoGroup(PolyominoShape shape, IReadOnlyList<Vector2Int> coordinates)
        {
            Shape = shape;
            Coordinates = new List<Vector2Int>(coordinates);
        }
    }
}