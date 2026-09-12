using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameGridGeneratorTests
    {
        MinigamePhaseConfig config = new MinigamePhaseConfig(
            width: 7,
            height: 3,
            shapes: new List<PolyominoShape> { PolyominoShape.Line5 },
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            timeLimit: 60f,
            destructiveToolLimit: 8,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );

        [Test]
        public void Generate_ShapeWithOnlyOneValidRotation_NeverThrows()
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.DoesNotThrow(() => MinigameGridGenerator.Generate(config));
            }
        }
    }
}