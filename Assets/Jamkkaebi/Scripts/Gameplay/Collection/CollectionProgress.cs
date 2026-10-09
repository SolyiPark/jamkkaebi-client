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

        /// <summary>최초 복원 등록이나 신규 확인으로 기록이 실제 변경되면 발생합니다.</summary>
        public event Action Changed;

        /// <summary>이 기록을 구성한 카탈로그 인스턴스이며 화면에 주입할 때 동일성을 검사합니다.</summary>
        public CollectionCatalog Catalog => _catalog;
        /// <summary>최종 복원을 완료한 서로 다른 유물 종류 수입니다.</summary>
        public int RestoredCount => _restoredIds.Count;
        /// <summary>카탈로그에 정의된 전체 유물 종류 수이며 건물은 포함하지 않습니다.</summary>
        public int TotalCount => _catalog.Relics.Count;
        /// <summary>복원한 종류 수를 전체 종류 수로 나눈 0~1 비율이며 전체가 비어 있으면 0입니다.</summary>
        public float CollectionRate => TotalCount == 0 ? 0f : (float)RestoredCount / TotalCount;

        /// <summary>모든 유물 종류의 복원을 완료한 시대의 건물 수입니다.</summary>
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

        /// <summary>카탈로그를 검증하고 수집·신규 기록이 비어 있는 메모리 모델을 만듭니다.</summary>
        /// <param name="catalog">기록과 화면이 함께 사용할 고정 정의 자산입니다.</param>
        /// <exception cref="ArgumentNullException">카탈로그가 null이거나 파괴된 Unity 자산인 경우입니다.</exception>
        /// <exception cref="ArgumentException">카탈로그의 목록·ID·시대 참조가 유효하지 않은 경우입니다.</exception>
        public CollectionProgress(CollectionCatalog catalog)
        {
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _catalog.Validate();
        }

        /// <summary>유물 종류의 최종 복원이 도감에 등록되었는지 확인합니다.</summary>
        /// <param name="relicId">조회할 유물 종류 ID입니다.</param>
        /// <returns>등록된 종류이면 true이며 null과 미등록 ID에는 false입니다.</returns>
        public bool IsRestored(string relicId)
        {
            return relicId != null && _restoredIds.Contains(relicId);
        }

        /// <summary>복원 등록 후 아직 상세를 확인하지 않은 유물 종류인지 확인합니다.</summary>
        /// <param name="relicId">조회할 유물 종류 ID입니다.</param>
        /// <returns>신규 확인이 남아 있으면 true이며 null과 미등록 ID에는 false입니다.</returns>
        public bool IsUnread(string relicId)
        {
            return relicId != null && _unreadIds.Contains(relicId);
        }

        /// <summary>
        /// 전체 복원 페이즈를 완료한 유물 종류를 최초 등록하고 신규 상태로 표시합니다.
        /// 개별 페이즈 성공과 중복 인스턴스 획득은 이 호출의 완료 조건이 아닙니다.
        /// </summary>
        /// <param name="relicId">카탈로그에 존재하는 최종 복원 완료 유물의 종류 ID입니다.</param>
        /// <returns>처음 등록하면 true이며 모르는 ID와 이미 등록한 종류에는 false입니다.</returns>
        public bool RegisterRestorationCompleted(string relicId)
        {
            if (_catalog.FindRelic(relicId) == null || !_restoredIds.Add(relicId))
                return false;

            _unreadIds.Add(relicId);
            Changed?.Invoke();
            return true;
        }

        /// <summary>유물 상세 확인으로 신규 표시를 해제하고 복원 완료 기록은 유지합니다.</summary>
        /// <param name="relicId">상세를 확인한 유물 종류 ID입니다.</param>
        /// <returns>신규 상태를 해제했으면 true이며 신규가 아니거나 ID가 null이면 false입니다.</returns>
        public bool MarkViewed(string relicId)
        {
            if (relicId == null || !_unreadIds.Remove(relicId))
                return false;

            Changed?.Invoke();
            return true;
        }

        /// <summary>지정한 시대에서 최종 복원을 완료한 유물 종류 수를 계산합니다.</summary>
        /// <param name="eraId">조회할 시대 ID입니다.</param>
        /// <returns>해당 시대의 완료 종류 수이며 존재하지 않는 시대는 0입니다.</returns>
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

        /// <summary>지정한 시대의 수집 대상 유물 종류 수를 계산합니다.</summary>
        /// <param name="eraId">조회할 시대 ID입니다.</param>
        /// <returns>해당 시대에 정의된 종류 수이며 존재하지 않는 시대는 0입니다.</returns>
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

        /// <summary>유물이 있는 시대의 모든 종류를 복원했는지 확인해 건물 해금 표시를 결정합니다.</summary>
        /// <param name="eraId">건물 해금을 확인할 시대 ID입니다.</param>
        /// <returns>모든 종류를 복원했으면 true이며 빈 시대와 존재하지 않는 시대는 false입니다.</returns>
        public bool IsBuildingUnlocked(string eraId)
        {
            int total = TotalInEra(eraId);
            return total > 0 && RestoredInEra(eraId) == total;
        }

        /// <summary>지정한 시대에 상세 확인이 남아 있는 신규 유물이 있는지 확인합니다.</summary>
        /// <param name="eraId">신규 표시를 확인할 시대 ID입니다.</param>
        /// <returns>확인하지 않은 등록 유물이 하나라도 있으면 true이며 그 외에는 false입니다.</returns>
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
