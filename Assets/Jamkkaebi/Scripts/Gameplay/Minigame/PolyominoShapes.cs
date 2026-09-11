using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public static class PolyominoShapes
    {
        public static readonly IReadOnlyDictionary<PolyominoShape, IReadOnlyList<Vector2Int>> Definitions =
            new Dictionary<PolyominoShape, IReadOnlyList<Vector2Int>>
            {
                { PolyominoShape.Dot, new[] { new Vector2Int(0, 0) } },
                { PolyominoShape.Line2, new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) } },
                { PolyominoShape.LShape3, new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1) } },
                {
                    PolyominoShape.LShape4,
                    new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(2, 1) }
                },
                {
                    PolyominoShape.Stairs4,
                    new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) }
                },
                {
                    PolyominoShape.Square4,
                    new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) }
                },
                {
                    PolyominoShape.Line5,
                    new[]
                    {
                        new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3),
                        new Vector2Int(0, 4)
                    }
                }
            };
    }
}