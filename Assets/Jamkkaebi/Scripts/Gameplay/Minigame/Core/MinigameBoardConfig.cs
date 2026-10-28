namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    /// <summary>
    /// 미니게임 한 판의 보드와 자원 설정. 서버 보드 파라미터와 클라이언트 고정 규칙을 합친 값이다.
    /// </summary>
    public readonly struct MinigameBoardConfig
    {
        public int Width { get; }
        public int Height { get; }
        /// <summary>찾아야 할 목표 형상(폴리오미노)의 개수.</summary>
        public int ShapeCount { get; }
        /// <summary>목표 형상들이 차지하는 칸 수의 합.</summary>
        public int ShapeCellTotal { get; }
        public int ThreatTileCount { get; }
        public int HelperTileCount { get; }
        public int ReinforcedTileCount { get; }
        public int DestructiveToolLimit { get; }
        public int ScoutToolLimit { get; }
        public int AllowedThreatHits { get; }

        public MinigameBoardConfig(int width, int height, int shapeCount, int shapeCellTotal, int threatTileCount,
            int helperTileCount, int reinforcedTileCount, int destructiveToolLimit, int scoutToolLimit, int allowedThreatHits)
        {
            Width = width;
            Height = height;
            ShapeCount = shapeCount;
            ShapeCellTotal = shapeCellTotal;
            ThreatTileCount = threatTileCount;
            HelperTileCount = helperTileCount;
            ReinforcedTileCount = reinforcedTileCount;
            DestructiveToolLimit = destructiveToolLimit;
            ScoutToolLimit = scoutToolLimit;
            AllowedThreatHits = allowedThreatHits;
        }
    }
}
