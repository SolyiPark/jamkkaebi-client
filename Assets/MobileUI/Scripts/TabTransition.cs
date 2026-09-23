using UnityEngine;
using UnityEngine.UI;
using MobilePrototype.Exhibition;

namespace MobilePrototype
{
    public sealed class TabTransition : MonoBehaviour
    {
        [SerializeField] private float _settleDuration = .24f;
        [SerializeField] private float _distanceThreshold = .22f;
        [SerializeField] private float _velocityThreshold = .8f;
        private TabHost _host;
        private RawImage _current;
        private RawImage _next;
        private CanvasGroup _profile;
        private int _target;
        private int _direction;
        private float _progress;
        private float _startProgress;
        private float _destination;
        private float _elapsed;
        private Vector2 _screenSize;
        private bool _settling;
        public bool IsActive { get; private set; }
        public float Progress => _progress;

        public void Initialize(TabHost host)
        {
            _host = host;
            // Keep the original viewport as a fixed raycast surface, move only its children.
            host.mirror.color = Color.clear;
            if (!host.viewport.GetComponent<RectMask2D>()) host.viewport.gameObject.AddComponent<RectMask2D>();
            _current = UIFactory.Rect("Current tab", host.viewport, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            _current.raycastTarget = false;
            _next = UIFactory.Rect("Incoming tab", host.viewport, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            _next.raycastTarget = false;
            _next.gameObject.SetActive(false);
            host.mirror = _current;
            _profile = host.profileBar.GetComponent<CanvasGroup>();
            if (!_profile) _profile = host.profileBar.AddComponent<CanvasGroup>();
        }
        public bool Begin(int target)
        {
            if (IsActive || !_host.IsReady) return false;
            _target = target;
            _direction = target > _host.ActiveIndex ? 1 : -1;
            _progress = 0;
            _settling = false;
            _screenSize = new Vector2(Screen.width, Screen.height);
            IsActive = true;
            bool valid = target >= 0 && target < _host.catalog.tabs.Count;
            _next.gameObject.SetActive(valid);
            if (valid)
            {
                var root = _host.GetRoot(target);
                _next.texture = root.sceneCamera.targetTexture;
                // Paused scenes use their last frame; never resume gameplay just for a preview.
                if (root.gameObject.activeInHierarchy) root.sceneCamera.enabled = true;
            }
            Apply();
            return true;
        }
        public void Drag(float distanceInViewportWidths)
        {
            if (!IsActive || _settling) return;
            float progress = Mathf.Clamp01(-distanceInViewportWidths * _direction);
            if (_target < 0 || _target >= _host.catalog.tabs.Count) progress = Mathf.Min(.1f, progress * .18f);
            _progress = progress;
            Apply();
        }
        public void Release(float velocityInViewportWidths)
        {
            if (!IsActive || _settling) return;
            bool valid = _target >= 0 && _target < _host.catalog.tabs.Count;
            bool commit = valid && (_progress >= _distanceThreshold ||
                (_progress > .025f && -velocityInViewportWidths * _direction >= _velocityThreshold));
            Settle(commit);
        }
        public void Settle(bool commit)
        {
            _settling = true;
            _startProgress = _progress;
            _destination = commit ? 1 : 0;
            _elapsed = 0;
        }
        private void Update()
        {
            if (!IsActive) return;
            if (_screenSize != new Vector2(Screen.width, Screen.height))
            { Cancel(); return; }
            if (!_settling) return;
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _settleDuration));
            _progress = Mathf.Lerp(_startProgress, _destination, 1 - Mathf.Pow(1 - t, 3));
            Apply();
            if (t >= 1) Finish(_destination > .5f);
        }
        private void Apply()
        {
            float width = _host.viewport.rect.width;
            _current.rectTransform.anchoredPosition = new Vector2(-_direction * _progress * width, 0);
            _next.rectTransform.anchoredPosition = new Vector2(_direction * (1 - _progress) * width, 0);
            SetParallax(_host.ActiveIndex, -_direction * _progress);
            bool valid = _target >= 0 && _target < _host.catalog.tabs.Count;
            if (valid) SetParallax(_target, _direction * (1 - _progress));
            float from = _host.catalog.tabs[_host.ActiveIndex].showProfile ? 1 : 0;
            float to = valid ? (_host.catalog.tabs[_target].showProfile ? 1 : 0) : from;
            float profile = Mathf.Lerp(from, to, _progress);
            _host.profileBar.SetActive(profile > 0);
            _profile.alpha = profile;
            _profile.blocksRaycasts = false;
            _host.viewport.offsetMax = new Vector2(0, -_host.headerHeight * profile);
        }
        private void SetParallax(int index, float offset)
        {
            var view = _host.GetRoot(index)?.GetComponent<ExhibitionView>();
            if (view) view.SetTransitionOffset(offset);
        }
        private void Finish(bool commit)
        {
            int target = commit ? _target : _host.ActiveIndex;
            SetParallax(_host.ActiveIndex, 0);
            if (_target >= 0 && _target < _host.catalog.tabs.Count) SetParallax(_target, 0);
            IsActive = false;
            _settling = false;
            _current.rectTransform.anchoredPosition = Vector2.zero;
            _next.gameObject.SetActive(false);
            _profile.alpha = 1;
            _profile.blocksRaycasts = true;
            _host.CompleteTransition(target);
        }
        public void Cancel() { if (IsActive) Finish(false); }
        private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
        private void OnDisable() { if (_host && IsActive) Cancel(); }
    }
}
