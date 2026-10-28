using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    [CreateAssetMenu(fileName = "RelicData", menuName = "Jamkkaebi/Relic Data")]
    public class RelicData : ScriptableObject
    {
        [FormerlySerializedAs("phases")] [SerializeField] private MinigameBoardConfigData[] _phases;

        public MinigameBoardConfig GetPhaseConfig(int phaseIndex)
        {
            return _phases[phaseIndex].ToConfig();
        }
    }
}