using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobilePrototype.Exhibition
{
    public static class ExhibitionLayoutCapture
    {
        /// <summary>소유권 확인 후 받은 ID 바인딩을 사용한다. 회수 후에도 바인딩 목록을 유지해야 한다.</summary>
        public static ExhibitionLayoutDraft Capture(ExhibitionHousing housing,
            IReadOnlyDictionary<string, int> buildingIds, int artifactId)
        {
            if (!housing || housing.Layout == null) throw new InvalidOperationException("홈 초기화가 필요합니다.");
            if (buildingIds == null) throw new ArgumentNullException(nameof(buildingIds));
            var objects = new List<ExhibitionObjectData>();
            var placedKinds = new HashSet<string>();
            foreach (var placement in housing.Layout.Placements)
            {
                buildingIds.TryGetValue(placement.ItemId, out int serverId);
                objects.Add(new ExhibitionBuildingData(placement.ItemId + "/" + placement.Id, serverId,
                    true, placement.Cell, placement.BaseSize, placement.QuarterTurns));
                placedKinds.Add(placement.ItemId);
            }
            foreach (var binding in buildingIds)
                if (!placedKinds.Contains(binding.Key))
                    objects.Add(new ExhibitionBuildingData(binding.Key, binding.Value, false,
                        Vector2Int.zero, Vector2Int.one));
            objects.Add(new ExhibitionNpcData("home-dokkaebi", artifactId, housing.Wanderer != null,
                housing.Wanderer ? housing.Wanderer.PlacementCell : Vector2Int.zero));
            return new ExhibitionLayoutDraft(housing.Surface.Grid.Size, objects);
        }
    }
}
