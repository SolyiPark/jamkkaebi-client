using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Collection
{
    /// <summary>
    /// 복원이 모두 끝난 유물 종류의 누적 도감 등록을 관리합니다.
    /// 작업대 연결 계층은 개별 페이즈 성공이 아니라 최종 복원 완료 시 등록해야 합니다.
    /// 저장과 개별 유물 인스턴스의 복원 진행은 호출 측의 책임입니다.
    /// </summary>
    public sealed class CollectionProgress
    {
        private readonly CollectionCatalog _catalog;
        private readonly HashSet<string> _restoredIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unreadIds = new HashSet<string>(StringComparer.Ordinal);

        public event Action Changed;

        public CollectionCatalog Catalog => _catalog;
        public int RestoredCount => _restoredIds.Count;
        public int TotalCount => _catalog.Relics.Count;
        public float CollectionRate => TotalCount == 0 ? 0f : (float)RestoredCount / TotalCount;

        public int CompletedBuildingCount
        {
            get
            {
                int completed = 0;
                foreach (CollectionEraDefinition era in _catalog.Eras)
                {
                    if (IsBuildingUnlocked(era.Id))
                        completed++;
                }

                return completed;
            }
        }

        public CollectionProgress(CollectionCatalog catalog)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _catalog.Validate();
        }

        public bool IsRestored(string relicId)
        {
            return relicId != null && _restoredIds.Contains(relicId);
        }

        public bool IsUnread(string relicId)
        {
            return relicId != null && _unreadIds.Contains(relicId);
        }

        public bool RegisterRestorationCompleted(string relicId)
        {
            if (_catalog.FindRelic(relicId) == null || !_restoredIds.Add(relicId))
                return false;

            _unreadIds.Add(relicId);
            Changed?.Invoke();
            return true;
        }

        public bool MarkViewed(string relicId)
        {
            if (relicId == null || !_unreadIds.Remove(relicId))
                return false;

            Changed?.Invoke();
            return true;
        }

        public int RestoredInEra(string eraId)
        {
            int restored = 0;
            foreach (CollectionRelicDefinition relic in _catalog.Relics)
            {
                if (string.Equals(relic.EraId, eraId, StringComparison.Ordinal) && IsRestored(relic.Id))
                    restored++;
            }

            return restored;
        }

        public int TotalInEra(string eraId)
        {
            int total = 0;
            foreach (CollectionRelicDefinition relic in _catalog.Relics)
            {
                if (string.Equals(relic.EraId, eraId, StringComparison.Ordinal))
                    total++;
            }

            return total;
        }

        public bool IsBuildingUnlocked(string eraId)
        {
            int total = TotalInEra(eraId);
            return total > 0 && RestoredInEra(eraId) == total;
        }

        public bool HasUnreadInEra(string eraId)
        {
            foreach (CollectionRelicDefinition relic in _catalog.Relics)
            {
                if (string.Equals(relic.EraId, eraId, StringComparison.Ordinal) && IsUnread(relic.Id))
                    return true;
            }

            return false;
        }
    }
}
