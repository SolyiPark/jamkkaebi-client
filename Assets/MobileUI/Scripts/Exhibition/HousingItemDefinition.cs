using UnityEngine;

namespace MobilePrototype.Exhibition
{
    [CreateAssetMenu(menuName = "Jamkkaebi/Exhibition/Item")]
    public sealed class HousingItemDefinition : ScriptableObject
    {
        [SerializeField] private string _itemId;
        [SerializeField] private string _displayName;
        [SerializeField] private Vector2Int _footprint = Vector2Int.one;
        [SerializeField] private GameObject _visualPrefab;
        [SerializeField] private Vector2 _hitSize = new Vector2(.4f, .48f);
        [SerializeField] private Vector2 _hitOffset = new Vector2(0, .2f);
        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public Vector2Int Footprint => _footprint;
        public GameObject VisualPrefab => _visualPrefab;
        public Vector2 HitSize => _hitSize;
        public Vector2 HitOffset => _hitOffset;
        public bool IsValid => !string.IsNullOrWhiteSpace(_itemId) && _footprint.x > 0 && _footprint.y > 0 &&
            _hitSize.x > 0 && _hitSize.y > 0;
    }
}
