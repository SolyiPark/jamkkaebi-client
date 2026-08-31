using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public struct PolyominoGroup
    {
        public PolyominoShape Shape { get; }
        public IReadOnlyList<Vector2Int> Coordinates { get; }

        public PolyominoGroup(PolyominoShape shape, List<Vector2Int> coordinates)
        {
            Shape = shape;
            Coordinates = coordinates;
        }
    }
}