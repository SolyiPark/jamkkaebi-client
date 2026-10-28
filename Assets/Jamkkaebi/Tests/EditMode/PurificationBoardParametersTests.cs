using System;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class PurificationBoardParametersTests
    {
        [Test]
        public void ToConfig_AllValuesDistinct_MapsEachServerFieldToMatchingProperty()
        {
            // Arrange: 값을 모두 다르게(허용 위협 상수 3과도 다르게) 넣어 필드끼리 바꿔치기된 경우도 잡는다
            PurificationBoardParameters board = CreateBoard(width: 11, height: 12, threatCount: 4, reinforcedOnShape: 5,
                reinforcedOnField: 6, helperCount: 7, bonusJeongseongCount: 8, bonusCrystalCount: 9, shapeCount: 2,
                shapeCellTotal: 13, tapBudget: 14, scoutLimit: 15);

            // Act
            MinigameBoardConfig config = board.ToConfig();

            // Assert
            Assert.AreEqual(11, config.Width);
            Assert.AreEqual(12, config.Height);
            Assert.AreEqual(4, config.ThreatTileCount);
            Assert.AreEqual(5, config.ReinforcedOnShapeCount);
            Assert.AreEqual(6, config.ReinforcedOnFieldCount);
            Assert.AreEqual(7, config.HelperTileCount);
            Assert.AreEqual(8, config.BonusTileCount);
            Assert.AreEqual(2, config.ShapeCount);
            Assert.AreEqual(13, config.ShapeCellTotal);
            Assert.AreEqual(14, config.DestructiveToolLimit);
            Assert.AreEqual(15, config.ScoutToolLimit);
            Assert.AreEqual(MinigameRules.AllowedThreatHits, config.AllowedThreatHits);
        }

        [Test]
        public void ToConfig_DifferentCrystalCount_IgnoresCrystal()
        {
            // Arrange
            PurificationBoardParameters noCrystal = CreateBoard(bonusCrystalCount: 0);
            PurificationBoardParameters withCrystal = CreateBoard(bonusCrystalCount: 5);

            // Act & Assert: struct의 Equals는 모든 필드 값을 비교한다
            Assert.AreEqual(noCrystal.ToConfig(), withCrystal.ToConfig());
        }

        [TestCase("width", 0)]
        [TestCase("height", 0)]
        [TestCase("threatCount", -1)]
        [TestCase("reinforcedOnShape", -1)]
        [TestCase("reinforcedOnField", -1)]
        [TestCase("helperCount", -1)]
        [TestCase("bonusJeongseongCount", -1)]
        [TestCase("tapBudget", 0)]
        [TestCase("scoutLimit", -1)]
        public void ToConfig_OutOfRangeValue_ThrowsInvalidOperationException(string field, int value)
        {
            // Arrange: 기본값에서 한 필드만 범위 밖으로 바꾼다
            PurificationBoardParameters board = field switch
            {
                "width" => CreateBoard(width: value),
                "height" => CreateBoard(height: value),
                "threatCount" => CreateBoard(threatCount: value),
                "reinforcedOnShape" => CreateBoard(reinforcedOnShape: value),
                "reinforcedOnField" => CreateBoard(reinforcedOnField: value),
                "helperCount" => CreateBoard(helperCount: value),
                "bonusJeongseongCount" => CreateBoard(bonusJeongseongCount: value),
                "tapBudget" => CreateBoard(tapBudget: value),
                "scoutLimit" => CreateBoard(scoutLimit: value),
                _ => throw new ArgumentException($"테스트에 없는 필드입니다: {field}")
            };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => board.ToConfig());
        }

        [Test]
        public void ToConfig_DefaultStruct_ThrowsInvalidOperationException()
        {
            // Arrange: default는 생성자를 거치지 않아 모든 값이 0이다
            PurificationBoardParameters board = default;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => board.ToConfig());
        }

        [Test]
        public void ToConfig_ServerNormalDefaults_GeneratesGridForAnySeed()
        {
            // Arrange: 서버 origin/main(57dc54c) NORMAL 기본값. 결정 1개는 클라이언트가 무시한다
            PurificationBoardParameters board = CreateBoard();
            MinigameBoardConfig config = board.ToConfig();

            for (long seed = 0; seed < 100; seed++)
            {
                // Act & Assert
                Assert.DoesNotThrow(() => MinigameGridGenerator.Generate(config, seed), $"seed: {seed}");
            }
        }

        // 기본값은 서버 NORMAL 난이도 값이다. 필요한 필드만 이름 붙은 인자로 바꿔 쓴다.
        private static PurificationBoardParameters CreateBoard(long seed = 12345678, int width = 7, int height = 8,
            int threatCount = 6, int reinforcedOnShape = 2, int reinforcedOnField = 1, int helperCount = 1,
            int bonusJeongseongCount = 2, int bonusCrystalCount = 1, int shapeCount = 3, int shapeCellTotal = 12,
            int tapBudget = 19, int scoutLimit = 3)
        {
            return new PurificationBoardParameters(seed, width, height, threatCount, reinforcedOnShape,
                reinforcedOnField, helperCount, bonusJeongseongCount, bonusCrystalCount, shapeCount, shapeCellTotal,
                tapBudget, scoutLimit);
        }
    }
}
