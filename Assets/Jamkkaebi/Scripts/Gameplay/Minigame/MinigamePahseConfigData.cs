using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    [System.Serializable]
    public class MinigamePhaseConfigData
    {
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private List<PolyominoShape> shapes;
        [SerializeField] private int threatTileCount;
        [SerializeField] private int helperTileCount;
        [SerializeField] private int reinforcedTileCount;

        public MinigamePhaseConfig ToConfig()
        {
            MinigamePhaseConfig config = new MinigamePhaseConfig(width, height, shapes, threatTileCount, helperTileCount,reinforcedTileCount);
            return config;
        }
    }
}