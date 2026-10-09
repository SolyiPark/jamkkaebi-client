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

        public IReadOnlyList<CollectionEraDefinition> Eras =>
            _readOnlyEras ?? (_readOnlyEras = Array.AsReadOnly(_eras));

        public IReadOnlyList<CollectionRelicDefinition> Relics =>
            _readOnlyRelics ?? (_readOnlyRelics = Array.AsReadOnly(_relics));

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

        public void Validate()
        {
            ValidateDefinitions(_eras, _relics);
        }

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

        private void OnEnable()
        {
            _readOnlyEras = null;
            _readOnlyRelics = null;
        }
    }
}
