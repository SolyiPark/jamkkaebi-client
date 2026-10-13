using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary> 봉인 매개체 enum으로 해당 매개체의 스킨 데이터를 찾아주는 카탈로그 </summary>
    [CreateAssetMenu(fileName = "SealMediumCatalog", menuName = "Jamkkaebi/Seal Medium Catalog")]
    public class SealMediumCatalog : ScriptableObject
    {
        [SerializeField] private SealMediumData[] _sealMediums;
        
        private Dictionary<SealMedium, SealMediumData> _sealMediumDataBySealMedium;

        public SealMediumData GetSealMediumData(SealMedium sealMedium)
        {
            // 딕셔너리 초기화
            if (_sealMediumDataBySealMedium == null)
            {
                _sealMediumDataBySealMedium = BuildLookup();
            }

            if (!_sealMediumDataBySealMedium.TryGetValue(sealMedium, out SealMediumData data))
            {
                throw new InvalidOperationException(
                    $"[SealMediumCatalog] '{name}'에 봉인 매개체 {sealMedium}의 스킨 데이터가 등록되어 있지 않습니다. 해당 SealMediumData를 목록에 추가하세요.");
            }

            return data;
        }

        private Dictionary<SealMedium, SealMediumData> BuildLookup()
        {
            Dictionary<SealMedium, SealMediumData> lookup = new Dictionary<SealMedium, SealMediumData>();

            if (_sealMediums == null || _sealMediums.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[SealMediumCatalog] '{name}'의 스킨 데이터 목록이 비어 있습니다. Inspector에서 SealMediumData를 등록하세요.");
            }
                
            for (int i = 0; i < _sealMediums.Length; i++)
            {
                SealMediumData data = _sealMediums[i];
                
                if (data == null)
                {
                    throw new InvalidOperationException(
                        $"[SealMediumCatalog] '{name}'의 스킨 데이터 목록에서 Element {i} 슬롯이 비어 있습니다. SealMediumData를 할당하거나 슬롯을 제거하세요.");
                }
                
                if (lookup.ContainsKey(data.SealMedium))
                {
                    throw new InvalidOperationException(
                        $"[SealMediumCatalog] '{name}'의 스킨 데이터 목록에 같은 봉인 매개체({data.SealMedium})가 중복 등록되어 있습니다. (Element {i}: '{data.name}')");
                }
                    
                lookup.Add(data.SealMedium, data);
            }
            
            return lookup;
        }
    }
}