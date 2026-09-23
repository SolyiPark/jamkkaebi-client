using UnityEngine;

namespace MobilePrototype.Exhibition
{
    [RequireComponent(typeof(ExhibitionIntro))]
    public sealed class ExhibitionView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _zoom = 1;
        [SerializeField] private Vector2 _center;
        private ExhibitionIntro _intro;
        private ExhibitionLayer[] _layers;
        public float TransitionOffset { get; private set; }
        public ExhibitionIntro Intro => _intro;
        public float Zoom => _zoom;
        private void Awake()
        {
            _intro = GetComponent<ExhibitionIntro>();
            _layers = GetComponentsInChildren<ExhibitionLayer>(true);
        }
        public void NotifyVisible() => _intro.Begin();
        public void SetTransitionOffset(float offset)
        {
            TransitionOffset = offset;
            if (Mathf.Abs(offset) > .0001f) _intro.Complete();
        }
        // Future pinch input calls this; grid data stays in platform local space.
        public void SetView(Vector2 center, float zoom)
        { _center = center; _zoom = Mathf.Clamp(zoom, .75f, 2); }
        private void LateUpdate()
        {
            _camera.orthographicSize = Mathf.Max(3.84f, 2.32f / Mathf.Max(.1f, _camera.aspect)) / _zoom;
            _camera.transform.localPosition = new Vector3(_center.x, _center.y, -10);
            foreach (var layer in _layers) layer.Apply(_intro.Elapsed, TransitionOffset, _camera);
        }
    }
}
