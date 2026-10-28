using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    [Serializable]
    public class MinigameBoardConfigData
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private int _shapeCount;
        [SerializeField] private int _shapeCellTotal;
        [SerializeField] private int _threatTileCount;
        [SerializeField] private int _helperTileCount;
        [SerializeField] private int _bonusTileCount;
        [SerializeField] private int _reinforcedOnShapeCount;
        [SerializeField] private int _reinforcedOnFieldCount;
        [SerializeField] private int _destructiveToolLimit;
        [SerializeField] private int _scoutToolLimit;

        public MinigameBoardConfig ToConfig()
        {
            MinigameBoardConfig config = new MinigameBoardConfig(_width, _height, _shapeCount, _shapeCellTotal, _threatTileCount, _helperTileCount, _bonusTileCount, _reinforcedOnShapeCount, _reinforcedOnFieldCount, _destructiveToolLimit, _scoutToolLimit, MinigameRules.AllowedThreatHits);
            return config;
        }
    }
}