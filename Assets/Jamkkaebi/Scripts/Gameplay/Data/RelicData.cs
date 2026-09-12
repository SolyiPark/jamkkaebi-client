using Jamkkaebi.Scripts.Gameplay.Minigame;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    [CreateAssetMenu(fileName = "RelicData", menuName = "Jamkkaebi/Relic Data")]
    public class RelicData : ScriptableObject
    {
        [FormerlySerializedAs("phases")] [SerializeField] private MinigamePhaseConfigData[] _phases;

        public MinigamePhaseConfig GetPhaseConfig(int phaseIndex)
        {
            return _phases[phaseIndex].ToConfig();
        }
    }
}