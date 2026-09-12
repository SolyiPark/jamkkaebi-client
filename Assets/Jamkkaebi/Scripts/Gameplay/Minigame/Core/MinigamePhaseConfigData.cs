using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    [Serializable]
    public class MinigamePhaseConfigData
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private List<PolyominoShape> _shapes;
        [SerializeField] private int _threatTileCount;
        [SerializeField] private int _helperTileCount;
        [SerializeField] private int _reinforcedTileCount;
        [SerializeField] private float _timeLimit;
        [SerializeField] private int _destructiveToolLimit;
        [SerializeField] private int _scoutToolLimit;
        [SerializeField] private int _allowedThreatHits;

        public MinigamePhaseConfig ToConfig()
        {
            MinigamePhaseConfig config = new MinigamePhaseConfig(_width, _height, _shapes, _threatTileCount, _helperTileCount, _reinforcedTileCount, _timeLimit,  _destructiveToolLimit, _scoutToolLimit, _allowedThreatHits);
            return config;
        }
    }
}