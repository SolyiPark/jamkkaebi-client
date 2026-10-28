using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigamePhaseConfigTests
    {
        private const int BaseWidth = 7;
        private const int BaseHeight = 8;
        private const int BaseThreat = 5;
        private const int BaseHelper = 2;
        private const int BaseReinforced = 3;
        private const int BaseDestructive = 19;
        private const int BaseScout = 4;
        private const int BaseAllowedThreatHits = 1;

        private MinigamePhaseConfig _baseConfig;
        
        [SetUp]
        public void Setup()
        {
            List<PolyominoShape> baseShapes = new List<PolyominoShape>
                { PolyominoShape.Square4, PolyominoShape.Line5 };
            _baseConfig = new MinigamePhaseConfig(BaseWidth, BaseHeight, baseShapes, BaseThreat, BaseHelper, BaseReinforced, BaseDestructive, BaseScout, BaseAllowedThreatHits);
        }
        
        [Test]
        public void WithShapes_ValidShapes_ReplacesOnlyShapes()
        {
            // Arrange
            List<PolyominoShape> shapes = new List<PolyominoShape> 
                {PolyominoShape.Dot, PolyominoShape.Line2, PolyominoShape.LShape3};
            
            // Act
            MinigamePhaseConfig config = _baseConfig.WithShapes(shapes);
            
            // Assert
            CollectionAssert.AreEqual(shapes, config.Shapes);
            Assert.AreEqual(BaseWidth, config.Width);
            Assert.AreEqual(BaseHeight, config.Height);
            Assert.AreEqual(BaseThreat, config.ThreatTileCount);
            Assert.AreEqual(BaseHelper, config.HelperTileCount);
            Assert.AreEqual(BaseReinforced, config.ReinforcedTileCount);
            Assert.AreEqual(BaseDestructive, config.DestructiveToolLimit);
            Assert.AreEqual(BaseScout, config.ScoutToolLimit);
            Assert.AreEqual(BaseAllowedThreatHits, config.AllowedThreatHits);
        }

        [Test]
        public void WithShapes_Null_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _baseConfig.WithShapes(null));
        }
    }
}