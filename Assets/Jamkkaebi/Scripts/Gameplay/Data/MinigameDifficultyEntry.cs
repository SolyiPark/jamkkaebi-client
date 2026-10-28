using System;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 난이도 하나와 그 난이도의 보드 설정을 묶은 테이블 항목.
    /// </summary>
    [Serializable]
    public class MinigameDifficultyEntry
    {
        [SerializeField] private MinigameDifficulty _difficulty;
        [SerializeField] private MinigameBoardConfigData _board;

        public MinigameDifficulty Difficulty => _difficulty;
        public MinigameBoardConfigData Board => _board;
    }
}
