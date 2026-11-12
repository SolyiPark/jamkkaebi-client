using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary> 페이즈 번호로 해당 페이즈의 연출 데이터를 찾아주는 카탈로그 </summary>
    [CreateAssetMenu(fileName = "PhaseCatalog", menuName = "Jamkkaebi/Phase Catalog")]
    public class PhaseCatalog : ScriptableObject
    {
        [SerializeField] private PhaseData[] _phases;

        private Dictionary<int, PhaseData> _phaseDataByPhase;

        /// <summary>
        /// 페이즈의 연출 데이터를 찾는다.
        /// </summary>
        /// <returns>등록된 데이터. 없으면 null이며, 호출한 쪽은 기본 연출(세기 1, 원래 밝기)로 보여 준다.</returns>
        public PhaseData GetPhaseData(int phase)
        {
            // 딕셔너리 초기화
            if (_phaseDataByPhase == null)
            {
                _phaseDataByPhase = BuildLookup();
            }

            if (_phaseDataByPhase.TryGetValue(phase, out PhaseData data)) return data;

            // 연출 값이 없다고 게임을 멈출 일은 아니므로 경고만 남긴다
            Debug.LogWarning($"[PhaseCatalog] '{name}'에 {phase}페이즈 데이터가 등록되어 있지 않아 기본 연출을 씁니다.", this);
            return null;
        }

        private Dictionary<int, PhaseData> BuildLookup()
        {
            Dictionary<int, PhaseData> lookup = new Dictionary<int, PhaseData>();
            if (_phases == null) return lookup;

            foreach (PhaseData data in _phases)
            {
                // 중복은 OnValidate에서 경고하고, 여기서는 먼저 등록된 것을 쓴다
                if (data != null && !lookup.ContainsKey(data.Phase))
                {
                    lookup.Add(data.Phase, data);
                }
            }

            return lookup;
        }

        private void OnValidate()
        {
            // Inspector에서 값이 바뀌면 다음 조회 때 다시 만든다
            _phaseDataByPhase = null;

            if (_phases == null) return;

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < _phases.Length; i++)
            {
                PhaseData data = _phases[i];
                if (data == null) continue;

                if (!seen.Add(data.Phase))
                {
                    Debug.LogWarning($"[PhaseCatalog] '{name}'에 {data.Phase}페이즈가 중복 등록되어 있습니다. (Element {i})", this);
                }
            }
        }
    }
}
