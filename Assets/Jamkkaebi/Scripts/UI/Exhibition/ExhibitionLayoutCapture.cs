using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    public static class ExhibitionLayoutCapture
    {
        /// <summary>소유권 확인 후 받은 ID 바인딩을 사용한다. 회수 후에도 바인딩 목록을 유지해야 한다.</summary>
        /// <param name="buildingIds">Owned building IDs keyed by local item kind; retain bindings after recall so unplaced buildings remain managed.</param>
        /// <param name="managedRelicIds">
        /// 홈이 관리하는 도깨비의 relicId. 여기 있는데 놓여 있지 않은 도깨비는 회수한 것으로 보내 서버 배치에서 빠진다.
        /// 숨김 도깨비처럼 홈이 다루지 않는 정령은 넣지 않아야 서버 배치가 보존된다.
        /// </param>
        /// <returns>
        /// A draft containing placed buildings, unplaced bindings, and every managed or placed dokkaebi;
        /// missing building bindings use ID zero for later rejection. 도깨비 자리는 기준 자리(마지막으로 도착한 칸)이며 칸 사이를 걷는 중의 위치가 아니다.
        /// </returns>
        /// <exception cref="InvalidOperationException">The housing or its layout is unavailable.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="buildingIds"/> 또는 <paramref name="managedRelicIds"/>가 null인 경우.</exception>
        public static ExhibitionLayoutDraft Capture(ExhibitionHousing housing,
            IReadOnlyDictionary<string, int> buildingIds, IEnumerable<int> managedRelicIds)
        {
            if (!housing || housing.Layout == null) throw new InvalidOperationException("홈 초기화가 필요합니다.");
            if (buildingIds == null) throw new ArgumentNullException(nameof(buildingIds));
            if (managedRelicIds == null) throw new ArgumentNullException(nameof(managedRelicIds));
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

            var placedRelics = new HashSet<int>();
            foreach (var wanderer in housing.Wanderers)
            {
                if (!wanderer || !placedRelics.Add(wanderer.RelicId)) continue;
                objects.Add(new ExhibitionNpcData("home-dokkaebi/" + wanderer.RelicId, wanderer.RelicId, true,
                    wanderer.PlacementCell));
            }
            foreach (int relicId in managedRelicIds)
                if (placedRelics.Add(relicId))
                    objects.Add(new ExhibitionNpcData("home-dokkaebi/" + relicId, relicId, false, Vector2Int.zero));
            return new ExhibitionLayoutDraft(housing.Surface.Grid.Size, objects);
        }
    }
}
