using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameBoardConfigDataTests
    {
        [Test]
        public void ToConfig_AllFieldsDistinct_CopiesEachFieldToMatchingProperty()
        {
            // Arrange: 필드 값을 모두 다르게(허용 위협 상수 3과도 다르게) 넣어 필드끼리 바꿔치기된 경우도 잡는다
            MinigameBoardConfigData data = new MinigameBoardConfigData();
            SetPrivateField(data, "_width", 11);
            SetPrivateField(data, "_height", 12);
            SetPrivateField(data, "_shapeCount", 2);
            SetPrivateField(data, "_shapeCellTotal", 13);
            SetPrivateField(data, "_threatTileCount", 4);
            SetPrivateField(data, "_helperTileCount", 5);
            SetPrivateField(data, "_bonusTileCount", 6);
            SetPrivateField(data, "_reinforcedOnShapeCount", 7);
            SetPrivateField(data, "_reinforcedOnFieldCount", 8);
            SetPrivateField(data, "_destructiveToolLimit", 9);
            SetPrivateField(data, "_scoutToolLimit", 10);

            // Act
            MinigameBoardConfig config = data.ToConfig();

            // Assert
            Assert.AreEqual(11, config.Width);
            Assert.AreEqual(12, config.Height);
            Assert.AreEqual(2, config.ShapeCount);
            Assert.AreEqual(13, config.ShapeCellTotal);
            Assert.AreEqual(4, config.ThreatTileCount);
            Assert.AreEqual(5, config.HelperTileCount);
            Assert.AreEqual(6, config.BonusTileCount);
            Assert.AreEqual(7, config.ReinforcedOnShapeCount);
            Assert.AreEqual(8, config.ReinforcedOnFieldCount);
            Assert.AreEqual(9, config.DestructiveToolLimit);
            Assert.AreEqual(10, config.ScoutToolLimit);
            Assert.AreEqual(MinigameRules.AllowedThreatHits, config.AllowedThreatHits);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
            field.SetValue(target, value);
        }
    }
}
