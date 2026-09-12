using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public readonly struct TileRevealResult
    {
        public Vector2Int Coordinate { get; }
        public TileContent Content { get; }
        public RevealOutcome Outcome { get; }

        public TileRevealResult(Vector2Int coordinate, TileContent tileContent, RevealOutcome outcome)
        {
            Coordinate = coordinate;
            Content = tileContent;
            Outcome = outcome;
        }
    }
}