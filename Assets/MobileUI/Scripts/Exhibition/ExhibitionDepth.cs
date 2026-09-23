using UnityEngine;
using UnityEngine.Rendering;

namespace MobilePrototype.Exhibition
{
    [RequireComponent(typeof(SortingGroup))]
    public sealed class ExhibitionDepth : MonoBehaviour
    {
        [SerializeField] private ExhibitionSurface _surface;
        [SerializeField] private Transform _groundAnchor;
        [SerializeField] private int _bias;
        private SortingGroup _group;
        public static int OrderFor(float localY, int bias = 0) =>
            1000 + Mathf.Clamp(Mathf.RoundToInt(-localY * 100), -800, 800) + Mathf.Clamp(bias, -50, 50);
        public void Configure(ExhibitionSurface surface, Transform groundAnchor)
        { _surface = surface; _groundAnchor = groundAnchor; }
        private void Awake() => _group = GetComponent<SortingGroup>();
        private void LateUpdate()
        {
            if (!_surface) return;
            var anchor = _groundAnchor ? _groundAnchor : transform;
            _group.sortingOrder = OrderFor(_surface.transform.InverseTransformPoint(anchor.position).y, _bias);
        }
    }
}
