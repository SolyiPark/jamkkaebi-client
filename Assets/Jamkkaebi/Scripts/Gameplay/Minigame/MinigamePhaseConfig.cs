using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public struct MinigamePhaseConfig
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<PolyominoShape> Shapes { get; }
        public int ThreatTileCount { get; }
        public int HelperTileCount { get; }
        public int ReinforcedTileCount { get; }

        public MinigamePhaseConfig(int width, int height, IReadOnlyList<PolyominoShape> shapes, int threatTileCount,
            int helperTileCount, int reinforcedTileCount)
        {
            Width = width;
            Height = height;
            Shapes = shapes;
            ThreatTileCount =  threatTileCount;
            HelperTileCount = helperTileCount;
            ReinforcedTileCount = reinforcedTileCount;
        }
    }
}