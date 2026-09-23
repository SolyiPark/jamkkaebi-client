using UnityEngine;

namespace MobilePrototype.Exhibition
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ExhibitionLayer : MonoBehaviour
    {
        [SerializeField] private float _parallax = .1f;
        [SerializeField] private float _introDelay;
        [SerializeField, Range(0, 1)] private float _opacity = 1;
        [SerializeField] private bool _coverBackground;
        [SerializeField] private bool _fillSceneryWidth;
        private SpriteRenderer _renderer;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        public float Parallax => _parallax;
        public void Configure(float parallax, float introDelay, float opacity, bool cover, bool fillSceneryWidth = false)
        { _parallax = parallax; _introDelay = introDelay; _opacity = opacity; _coverBackground = cover; _fillSceneryWidth = fillSceneryWidth; }
        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _basePosition = transform.localPosition;
            _baseScale = transform.localScale;
        }
        public void Apply(float introTime, float transitionOffset, Camera camera)
        {
            float appearance = Mathf.SmoothStep(0, 1, Mathf.Clamp01((introTime - _introDelay) / .65f));
            transform.localPosition = _basePosition + new Vector3(transitionOffset * _parallax,
                _coverBackground ? 0 : -.18f * (1 - appearance), 0);
            var color = _renderer.color;
            color.a = _opacity * (_coverBackground ? 1 : appearance);
            _renderer.color = color;
            if (_coverBackground)
            {
                // Paper covers even tablet/very tall viewports and the small parallax overscan.
                float worldHeight = camera.orthographicSize * 2;
                var size = _renderer.sprite.bounds.size;
                float parentScale = transform.parent.lossyScale.x;
                float scale = Mathf.Max(worldHeight / size.y, worldHeight * camera.aspect / size.x) / parentScale;
                transform.localScale = _baseScale * scale * 1.12f;
            }
            else if (_fillSceneryWidth)
            {
                // Extend decorative scenery uniformly, while the platform and its grid retain their scale.
                float width = camera.orthographicSize * 2 * camera.aspect;
                float scale = Mathf.Max(1, (width + .8f) / (_renderer.sprite.bounds.size.x * transform.parent.lossyScale.x));
                transform.localScale = _baseScale * scale;
            }
        }
    }
}
