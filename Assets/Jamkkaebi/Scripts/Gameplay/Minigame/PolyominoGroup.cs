using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public struct PolyominoGroup
    {
        public int ShapeIndex { get; }
        public IReadOnlyList<Vector2Int> Coordinates { get; }

        public PolyominoGroup(int shapeIndex, List<Vector2Int> coordinates)
        {
            ShapeIndex = shapeIndex;
            Coordinates = coordinates;
        }
    }
}