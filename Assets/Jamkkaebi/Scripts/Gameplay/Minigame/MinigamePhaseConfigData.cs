using System.Collections.Generic;
using UnityEngine;
using System;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
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

        public MinigamePhaseConfig ToConfig()
        {
            MinigamePhaseConfig config = new MinigamePhaseConfig(_width, _height, _shapes, _threatTileCount, _helperTileCount, _reinforcedTileCount);
            return config;
        }
    }
}