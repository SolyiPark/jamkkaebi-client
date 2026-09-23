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
        /// <summary>
        /// 플랫폼 로컬 Y가 낮을수록 앞에 표시되도록 정렬 순서를 계산하며, 깊이 기여는 ±800, 보정값은 ±50으로 제한합니다.
        /// </summary>
        public static int OrderFor(float localY, int bias = 0) =>
            1000 + Mathf.Clamp(Mathf.RoundToInt(-localY * 100), -800, 800) + Mathf.Clamp(bias, -50, 50);
        /// <summary>
        /// 깊이 계산의 기준 플랫폼과 월드 위치를 읽을 바닥 접점을 연결합니다. 접점이 없으면 자신의 위치를 사용합니다.
        /// </summary>
        public void Configure(ExhibitionSurface surface, Transform groundAnchor)
        { _surface = surface; _groundAnchor = groundAnchor; }
        /// <summary>
        /// 하위 렌더러를 함께 정렬할 SortingGroup을 캐시합니다.
        /// </summary>
        private void Awake() => _group = GetComponent<SortingGroup>();
        /// <summary>
        /// 바닥 접점을 플랫폼 로컬 좌표로 변환해 줌과 시차에 독립적인 정렬 순서를 갱신합니다.
        /// </summary>
        private void LateUpdate()
        {
            if (!_surface) return;
            var anchor = _groundAnchor ? _groundAnchor : transform;
            _group.sortingOrder = OrderFor(_surface.transform.InverseTransformPoint(anchor.position).y, _bias);
        }
    }
}
