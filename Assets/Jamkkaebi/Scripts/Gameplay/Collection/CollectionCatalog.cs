using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Collection
{
    [CreateAssetMenu(fileName = "CollectionCatalog", menuName = "Jamkkaebi/Collection Catalog")]
    public sealed class CollectionCatalog : ScriptableObject
    {
        [SerializeField] private CollectionEraDefinition[] _eras = Array.Empty<CollectionEraDefinition>();
        [SerializeField] private CollectionRelicDefinition[] _relics = Array.Empty<CollectionRelicDefinition>();

        private IReadOnlyList<CollectionEraDefinition> _readOnlyEras;
        private IReadOnlyList<CollectionRelicDefinition> _readOnlyRelics;

        /// <summary>화면에 표시할 순서를 유지하는 읽기 전용 시대 목록입니다.</summary>
        public IReadOnlyList<CollectionEraDefinition> Eras =>
            _readOnlyEras ?? (_readOnlyEras = Array.AsReadOnly(_eras));

        /// <summary>종류별 수집률의 분모와 시대별 목록에 사용하는 읽기 전용 유물 정의입니다.</summary>
        public IReadOnlyList<CollectionRelicDefinition> Relics =>
            _readOnlyRelics ?? (_readOnlyRelics = Array.AsReadOnly(_relics));

        /// <summary>
        /// 새 정의를 검증한 뒤 카탈로그 전체를 교체합니다. 검증에 실패하면 기존 정의를 유지합니다.
        /// 진행 기록을 생성하기 전에 호출하며 사용 중인 카탈로그는 재구성하지 않습니다.
        /// </summary>
        /// <param name="eras">표시 순서대로 구성한 시대 정의입니다.</param>
        /// <param name="relics">표시 순서대로 구성한 유물 종류 정의입니다.</param>
        /// <exception cref="ArgumentNullException">목록 중 하나가 null인 경우입니다.</exception>
        /// <exception cref="ArgumentException">ID가 비어 있거나 중복되거나 유물의 시대 참조가 없는 경우입니다.</exception>
        public void Configure(IEnumerable<CollectionEraDefinition> eras,
            IEnumerable<CollectionRelicDefinition> relics)
        {
            if (eras == null)
                throw new ArgumentNullException(nameof(eras));
            if (relics == null)
                throw new ArgumentNullException(nameof(relics));

            List<CollectionEraDefinition> nextEras = new List<CollectionEraDefinition>(eras);
            List<CollectionRelicDefinition> nextRelics = new List<CollectionRelicDefinition>(relics);
            ValidateDefinitions(nextEras, nextRelics);

            _eras = nextEras.ToArray();
            _relics = nextRelics.ToArray();
            _readOnlyEras = null;
            _readOnlyRelics = null;
        }

        /// <summary>저장 자산을 포함한 현재 정의의 목록, 고유 ID와 시대 참조를 검사합니다.</summary>
        /// <exception cref="ArgumentException">목록이 null이거나 정의의 ID 또는 시대 참조가 유효하지 않은 경우입니다.</exception>
        public void Validate()
        {
            ValidateDefinitions(_eras, _relics);
        }

        /// <summary>구성 후보와 직렬화된 목록에 동일한 ID·시대 참조 검증 규칙을 적용합니다.</summary>
        private static void ValidateDefinitions(IEnumerable<CollectionEraDefinition> eras,
            IEnumerable<CollectionRelicDefinition> relics)
        {
            if (eras == null)
                throw new ArgumentException("시대 목록이 필요합니다.", nameof(eras));
            if (relics == null)
                throw new ArgumentException("유물 목록이 필요합니다.", nameof(relics));

            HashSet<string> eraIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> relicIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (CollectionEraDefinition era in eras)
            {
                if (era == null || string.IsNullOrWhiteSpace(era.Id) || !eraIds.Add(era.Id))
                    throw new ArgumentException("시대에는 중복되지 않는 ID가 필요합니다.", nameof(eras));
            }

            foreach (CollectionRelicDefinition relic in relics)
            {
                if (relic == null || string.IsNullOrWhiteSpace(relic.Id) || !relicIds.Add(relic.Id))
                    throw new ArgumentException("유물에는 중복되지 않는 ID가 필요합니다.", nameof(relics));
                if (string.IsNullOrWhiteSpace(relic.EraId) || !eraIds.Contains(relic.EraId))
                    throw new ArgumentException("유물의 시대 ID가 도감에 존재해야 합니다.", nameof(relics));
            }
        }

        /// <summary>대소문자를 구분하는 고정 ID로 시대 정의를 찾습니다.</summary>
        /// <param name="eraId">검색할 시대 ID입니다.</param>
        /// <returns>일치하는 정의이며, ID가 비어 있거나 없으면 null입니다.</returns>
        public CollectionEraDefinition FindEra(string eraId)
        {
            if (string.IsNullOrWhiteSpace(eraId))
                return null;

            foreach (CollectionEraDefinition era in _eras)
            {
                if (era != null && string.Equals(era.Id, eraId, StringComparison.Ordinal))
                    return era;
            }

            return null;
        }

        /// <summary>대소문자를 구분하는 고정 ID로 유물 종류 정의를 찾습니다.</summary>
        /// <param name="relicId">인스턴스 ID가 아닌 유물 종류 ID입니다.</param>
        /// <returns>일치하는 정의이며, ID가 비어 있거나 없으면 null입니다.</returns>
        public CollectionRelicDefinition FindRelic(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId))
                return null;

            foreach (CollectionRelicDefinition relic in _relics)
            {
                if (relic != null && string.Equals(relic.Id, relicId, StringComparison.Ordinal))
                    return relic;
            }

            return null;
        }

        /// <summary>자산을 불러올 때 이전 읽기 전용 래퍼 캐시를 지워 현재 직렬화 목록을 사용합니다.</summary>
        private void OnEnable()
        {
            _readOnlyEras = null;
            _readOnlyRelics = null;
        }

        /// <summary>Inspector에서 직렬화 목록을 바꿀 때 읽기 전용 래퍼 캐시를 지워 조회와 집계가 같은 배열을 사용하게 합니다.</summary>
        private void OnValidate()
        {
            _readOnlyEras = null;
            _readOnlyRelics = null;
        }
    }
}
