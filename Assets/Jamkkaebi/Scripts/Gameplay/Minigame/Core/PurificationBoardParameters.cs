using System;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    /// <summary>
    /// 서버 정화 시작 응답의 보드 파라미터(<c>BoardView</c>). 필드 이름과 의미를 서버와 1:1로 맞춘다.
    /// </summary>
    /// <remarks>
    /// JSON 해석은 통신 계층이 맡고, 이 타입은 해석된 값을 받아 <see cref="MinigameBoardConfig"/>로 바꾸는 일만 한다.
    /// </remarks>
    public readonly struct PurificationBoardParameters
    {
        /// <summary>
        /// 클라이언트가 받는 보드 한 변의 최대 칸 수. 서버 기본값은 가장 큰 어려움이 8×9다.
        /// 칸마다 오브젝트를 만들므로 지나치게 큰 응답은 판을 만들기 전에 거부한다(총 칸 수도 이 값의 제곱 이하).
        /// </summary>
        public const int MaxSide = 16;

        public long Seed { get; }
        public int Width { get; }
        public int Height { get; }
        public int ThreatCount { get; }
        public int ReinforcedOnShape { get; }
        public int ReinforcedOnField { get; }
        public int HelperCount { get; }
        public int BonusJeongseongCount { get; }
        public int ShapeCount { get; }
        public int ShapeCellTotal { get; }
        public int TapBudget { get; }
        public int ScoutLimit { get; }

        public PurificationBoardParameters(long seed, int width, int height, int threatCount, int reinforcedOnShape,
            int reinforcedOnField, int helperCount, int bonusJeongseongCount, int shapeCount,
            int shapeCellTotal, int tapBudget, int scoutLimit)
        {
            Seed = seed;
            Width = width;
            Height = height;
            ThreatCount = threatCount;
            ReinforcedOnShape = reinforcedOnShape;
            ReinforcedOnField = reinforcedOnField;
            HelperCount = helperCount;
            BonusJeongseongCount = bonusJeongseongCount;
            ShapeCount = shapeCount;
            ShapeCellTotal = shapeCellTotal;
            TapBudget = tapBudget;
            ScoutLimit = scoutLimit;
        }

        /// <summary>
        /// 서버 값을 검증하고 미니게임 보드 설정으로 바꾼다.
        /// </summary>
        /// <remarks>
        /// 검증을 생성자가 아니라 여기서 하는 이유: struct는 <c>default</c>로 만들면 생성자를 거치지 않으므로, 쓰는 시점에 검사해야 빠짐이 없다.
        /// 탭 예산은 안전·공격 파괴 공용 횟수(<see cref="MinigameBoardConfig.DestructiveToolLimit"/>)로, 허용 위협 횟수는 클라이언트 고정값으로 채운다.
        /// 형상 수·칸 합의 조합과 칸 수 초과는 그리드 생성기가 검증한다.
        /// </remarks>
        /// <returns>세션 생성에 쓸 보드 설정.</returns>
        /// <exception cref="InvalidOperationException">그리드 크기나 탭 예산이 1 미만이거나, 그리드 한 변이 <see cref="MaxSide"/>를 넘거나, 타일 수·정찰 횟수가 음수일 때.
        /// 매개변수가 없는 메서드라 잘못된 인자(<see cref="ArgumentException"/>)가 아니라 인스턴스 상태가 잘못된 경우로 본다.</exception>
        public MinigameBoardConfig ToConfig()
        {
            RequireAtLeast(Width, 1, nameof(Width));
            RequireAtLeast(Height, 1, nameof(Height));
            RequireAtMost(Width, MaxSide, nameof(Width));
            RequireAtMost(Height, MaxSide, nameof(Height));
            RequireAtLeast(ThreatCount, 0, nameof(ThreatCount));
            RequireAtLeast(ReinforcedOnShape, 0, nameof(ReinforcedOnShape));
            RequireAtLeast(ReinforcedOnField, 0, nameof(ReinforcedOnField));
            RequireAtLeast(HelperCount, 0, nameof(HelperCount));
            RequireAtLeast(BonusJeongseongCount, 0, nameof(BonusJeongseongCount));
            RequireAtLeast(TapBudget, 1, nameof(TapBudget));
            RequireAtLeast(ScoutLimit, 0, nameof(ScoutLimit));

            return new MinigameBoardConfig(
                width: Width,
                height: Height,
                shapeCount: ShapeCount,
                shapeCellTotal: ShapeCellTotal,
                threatTileCount: ThreatCount,
                helperTileCount: HelperCount,
                bonusTileCount: BonusJeongseongCount,
                reinforcedOnShapeCount: ReinforcedOnShape,
                reinforcedOnFieldCount: ReinforcedOnField,
                destructiveToolLimit: TapBudget,
                scoutToolLimit: ScoutLimit,
                allowedThreatHits: MinigameRules.AllowedThreatHits);
        }

        private static void RequireAtMost(int value, int maximum, string name)
        {
            if (value > maximum)
            {
                throw new InvalidOperationException($"서버 보드 값 {name}은 {maximum} 이하여야 합니다. (입력값: {value})");
            }
        }

        private static void RequireAtLeast(int value, int minimum, string name)
        {
            if (value < minimum)
            {
                throw new InvalidOperationException($"서버 보드 값 {name}은 {minimum} 이상이어야 합니다. (입력값: {value})");
            }
        }
    }
}
