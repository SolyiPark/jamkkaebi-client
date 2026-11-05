using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>서버 유물·건물 ID와 방문 전시 표현을 연결하는 대응표입니다.</summary>
    [CreateAssetMenu(menuName = "Jamkkaebi/Exhibition/Visual Catalog")]
    public sealed class ExhibitionVisualCatalog : ScriptableObject
    {
        [SerializeField] private ExhibitionVisualDefinition[] _relics = Array.Empty<ExhibitionVisualDefinition>();
        [SerializeField] private ExhibitionVisualDefinition[] _buildings = Array.Empty<ExhibitionVisualDefinition>();
        public IReadOnlyList<ExhibitionVisualDefinition> Relics => Array.AsReadOnly(_relics);
        public IReadOnlyList<ExhibitionVisualDefinition> Buildings => Array.AsReadOnly(_buildings);

        /// <summary>표현용 프리팹 유무와 무관하게 해당 종류 ID의 등록 여부를 반환합니다.</summary>
        public bool IsKnown(bool isBuilding, int id) => TryGet(isBuilding, id, out _);

        /// <summary>종류와 서버 ID가 일치하는 정의를 찾습니다. 미등록 ID는 false입니다.</summary>
        public bool TryGet(bool isBuilding, int id, out ExhibitionVisualDefinition definition)
        {
            foreach (var item in isBuilding ? _buildings : _relics)
                if (item != null && item.Id == id) { definition = item; return true; }
            definition = null;
            return false;
        }

        /// <summary>입력 배열을 복사하고 ID·크기·터치 영역의 유효성을 검사한 뒤 교체합니다.</summary>
        public void Configure(ExhibitionVisualDefinition[] relics, ExhibitionVisualDefinition[] buildings)
        {
            var relicCopy = (ExhibitionVisualDefinition[])(relics ?? throw new ArgumentNullException(nameof(relics))).Clone();
            var buildingCopy = (ExhibitionVisualDefinition[])(buildings ?? throw new ArgumentNullException(nameof(buildings))).Clone();
            ValidateDefinitions(relicCopy); ValidateDefinitions(buildingCopy);
            _relics = relicCopy; _buildings = buildingCopy;
        }

        /// <summary>직렬화된 카탈로그의 중복 ID와 유효하지 않은 표현 크기를 거부합니다.</summary>
        public void Validate() { ValidateDefinitions(_relics); ValidateDefinitions(_buildings); }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateDefinitions(ExhibitionVisualDefinition[] definitions)
        {
            if (definitions == null) throw new InvalidOperationException("전시 표현 목록이 필요합니다.");
            var ids = new HashSet<int>();
            foreach (var item in definitions)
            {
                if (item == null || item.Id <= 0 || !ids.Add(item.Id))
                    throw new InvalidOperationException("전시 표현 ID는 양수이며 종류별로 중복될 수 없습니다.");
                var scale = item.Scale; var offset = item.FootOffset; var rect = item.TouchRect;
                if (!Finite(scale.x) || !Finite(scale.y) || !Finite(scale.z) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0 ||
                    !Finite(offset.x) || !Finite(offset.y) || !Finite(rect.x) || !Finite(rect.y) ||
                    !Finite(rect.width) || !Finite(rect.height) || rect.width <= 0 || rect.height <= 0)
                    throw new InvalidOperationException("전시 표현의 크기·바닥 접점·터치 영역이 올바르지 않습니다.");
            }
        }
    }
}
