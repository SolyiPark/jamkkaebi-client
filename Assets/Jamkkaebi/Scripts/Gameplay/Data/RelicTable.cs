using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 서버 없이 에디터에서 쓸 유물 목록. relicId로 유물 데이터를 찾는다.
    /// </summary>
    /// <remarks>
    /// 서버 연동 후에는 서버가 내려주는 유물 목록과 relicId로 대응시킨다. 지금은 데모용으로 일부 유물만 등록한다.
    /// </remarks>
    [CreateAssetMenu(fileName = "RelicTable", menuName = "Jamkkaebi/Relic Table")]
    public class RelicTable : ScriptableObject
    {
        [SerializeField] private RelicData[] _relics;

        private Dictionary<int, RelicData> _relicById;

        public IReadOnlyList<RelicData> Relics => _relics ?? Array.Empty<RelicData>();

        /// <summary>
        /// relicId에 해당하는 유물 데이터를 찾는다.
        /// </summary>
        /// <returns>등록되어 있으면 true.</returns>
        public bool TryGetRelic(int relicId, out RelicData relic)
        {
            _relicById ??= BuildLookup();
            return _relicById.TryGetValue(relicId, out relic);
        }

        private Dictionary<int, RelicData> BuildLookup()
        {
            var lookup = new Dictionary<int, RelicData>();
            foreach (RelicData relic in Relics)
            {
                // 빈 슬롯과 중복은 OnValidate에서 경고하고, 여기서는 먼저 등록된 것을 쓴다
                if (relic != null && !lookup.ContainsKey(relic.RelicId)) lookup.Add(relic.RelicId, relic);
            }
            return lookup;
        }

        private void OnValidate()
        {
            _relicById = null;

            var seen = new HashSet<int>();
            for (int i = 0; i < Relics.Count; i++)
            {
                RelicData relic = Relics[i];
                if (relic == null)
                {
                    Debug.LogWarning($"[RelicTable] '{name}'의 Element {i} 슬롯이 비어 있습니다.", this);
                    continue;
                }
                if (!seen.Add(relic.RelicId))
                {
                    Debug.LogWarning($"[RelicTable] '{name}'에 relicId {relic.RelicId}가 중복 등록되어 있습니다. (Element {i}: '{relic.name}')", this);
                }
            }
        }
    }
}
