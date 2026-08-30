using Jamkkaebi.Scripts.Gameplay.Minigame;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    [CreateAssetMenu(fileName = "RelicData", menuName = "Jamkkaebi/Relic Data")]
    public class RelicData : ScriptableObject
    {
        [SerializeField] private MinigamePhaseConfigData[] phases;

        public MinigamePhaseConfig GetPhaseConfig(int phaseIndex)
        {
            return phases[phaseIndex].ToConfig();
        }
    }
}