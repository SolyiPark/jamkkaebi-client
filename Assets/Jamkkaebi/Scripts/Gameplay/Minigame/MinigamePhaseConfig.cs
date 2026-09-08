using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public readonly struct MinigamePhaseConfig
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<PolyominoShape> Shapes { get; }
        public int ThreatTileCount { get; }
        public int HelperTileCount { get; }
        public int ReinforcedTileCount { get; }
        public float TimeLimit { get; }
        public int DestructiveToolLimit { get; }
        public int ScoutToolLimit { get; }
        public int AllowedThreatHits { get; }

        public MinigamePhaseConfig(int width, int height, IReadOnlyList<PolyominoShape> shapes, int threatTileCount,
            int helperTileCount, int reinforcedTileCount, float timeLimit, int destructiveToolLimit, int scoutToolLimit, int allowedThreatHits)
        {
            Width = width;
            Height = height;
            Shapes = shapes;
            ThreatTileCount = threatTileCount;
            HelperTileCount = helperTileCount;
            ReinforcedTileCount = reinforcedTileCount;
            TimeLimit = timeLimit;
            DestructiveToolLimit = destructiveToolLimit;
            ScoutToolLimit = scoutToolLimit;
            AllowedThreatHits = allowedThreatHits;
        }
    }
}