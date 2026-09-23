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

        /// <summary>
        /// 입력 뷰포트는 고정한 채 현재·다음 탭을 표시할 이미지를 생성하고 프로필 전환 상태를 준비합니다.
        /// </summary>
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
        /// <summary>
        /// 진행 중 전환이 없고 호스트가 준비되었을 때 전환을 시작합니다. 숨김 탭은 저장된 화면을 사용하며 범위 밖 대상은 끝 경계 연출로 처리합니다.
        /// </summary>
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
        /// <summary>
        /// 뷰포트 너비 단위의 수평 이동을 전환 진행률로 반영합니다. 오른쪽 이동은 양수이며 끝 경계에서는 이동량을 제한합니다.
        /// </summary>
        public void Drag(float distanceInViewportWidths)
        {
            if (!IsActive || _settling) return;
            float progress = Mathf.Clamp01(-distanceInViewportWidths * _direction);
            if (_target < 0 || _target >= _host.catalog.tabs.Count) progress = Mathf.Min(.1f, progress * .18f);
            _progress = progress;
            Apply();
        }
        /// <summary>
        /// 뷰포트 너비/초 단위의 수평 속도와 누적 이동량으로 전환 확정 여부를 결정합니다. 범위 밖 대상은 항상 복귀합니다.
        /// </summary>
        public void Release(float velocityInViewportWidths)
        {
            if (!IsActive || _settling) return;
            bool valid = _target >= 0 && _target < _host.catalog.tabs.Count;
            bool commit = valid && (_progress >= _distanceThreshold ||
                (_progress > .025f && -velocityInViewportWidths * _direction >= _velocityThreshold));
            Settle(commit);
        }
        /// <summary>
        /// 현재 진행률에서 확정이면 1, 취소이면 0으로 정착할 보간 상태를 준비합니다. 시작된 전환에서 호출합니다.
        /// </summary>
        public void Settle(bool commit)
        {
            _settling = true;
            _startProgress = _progress;
            _destination = commit ? 1 : 0;
            _elapsed = 0;
        }
        /// <summary>
        /// 시간 배율과 무관하게 정착 애니메이션을 진행하고 완료 시 탭을 확정합니다. 화면 크기가 바뀌면 전환을 취소합니다.
        /// </summary>
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
        /// <summary>
        /// 전환 진행률로 두 탭 이미지의 위치·홈 시차·프로필 투명도와 뷰포트 높이를 함께 갱신합니다.
        /// </summary>
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
        /// <summary>
        /// 해당 탭에 전시관 뷰가 있으면 시차 오프셋을 전달하며 다른 탭에는 영향을 주지 않습니다.
        /// </summary>
        private void SetParallax(int index, float offset)
        {
            var view = _host.GetRoot(index)?.GetComponent<ExhibitionView>();
            if (view) view.SetTransitionOffset(offset);
        }
        /// <summary>
        /// 시차와 이미지·프로필 상태를 정리하고 호스트에 목적 탭 또는 취소 시 원래 탭의 표시를 요청합니다.
        /// </summary>
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
        /// <summary>
        /// 진행 중인 전환을 즉시 취소해 원래 탭으로 복귀합니다. 전환이 없으면 아무 작업도 하지 않습니다.
        /// </summary>
        public void Cancel() { if (IsActive) Finish(false); }
        /// <summary>
        /// 앱이 포커스를 잃으면 진행 중 전환을 취소합니다.
        /// </summary>
        private void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
        /// <summary>
        /// 호스트가 연결된 전환 컴포넌트가 비활성화되면 미완료 전환을 취소합니다.
        /// </summary>
        private void OnDisable() { if (_host && IsActive) Cancel(); }
    }
}
