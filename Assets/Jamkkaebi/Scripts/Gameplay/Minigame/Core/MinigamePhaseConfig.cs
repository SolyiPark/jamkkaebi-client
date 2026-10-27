using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
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

        /// <summary>
        /// 이 설정에서 모양만 지정한 리스트로 바꾼 새 복사본을 돌려준다.
        /// </summary>
        /// <param name="shapes">새 설정에 들어갈 모양 리스트</param>
        /// <returns>새 설정. 모양 외의 값은 이 인스턴스에서 그대로 복사된다.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="shapes"/>가 null일 때.</exception>
        public MinigamePhaseConfig WithShapes(IReadOnlyList<PolyominoShape> shapes)
        {
            if (shapes == null)
            {
                throw new ArgumentNullException(nameof(shapes));
            }
            
            return new MinigamePhaseConfig(Width, Height, shapes, ThreatTileCount, HelperTileCount, ReinforcedTileCount, TimeLimit, DestructiveToolLimit, ScoutToolLimit, AllowedThreatHits);
        }
    }
}