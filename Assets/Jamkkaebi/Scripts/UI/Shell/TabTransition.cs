using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Exhibition;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Shell
{
    /// <summary>
    /// 하단 탭 선택으로 시작한 전환을 일정 시간 동안 수평 이동으로 보여 줍니다. 손가락 스와이프 전환은 지원하지 않습니다.
    /// </summary>
    public sealed class TabTransition : MonoBehaviour
    {
        [SerializeField] private float _duration = .24f;
        private TabHost _host;
        private RawImage _current;
        private RawImage _next;
        private CanvasGroup _profile;
        private int _target;
        private int _direction;
        private float _progress;
        private float _elapsed;
        private Vector2 _screenSize;
        public bool IsActive { get; private set; }

        /// <summary>
        /// 입력 뷰포트는 고정한 채 현재·다음 탭을 표시할 이미지를 생성하고 프로필 전환 상태를 준비합니다.
        /// </summary>
        public void Initialize(TabHost host)
        {
            _host = host;
            // 원래 뷰포트는 고정된 입력 표면으로 두고, 자식 이미지만 움직입니다.
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
        /// 진행 중 전환이 없고 대상이 현재와 다른 유효한 탭일 때 전환을 시작합니다. 숨김 탭은 저장된 마지막 화면을 사용합니다.
        /// </summary>
        /// <returns>전환을 시작했으면 true입니다.</returns>
        public bool Play(int target)
        {
            if (IsActive || !_host.IsReady) return false;
            if (target < 0 || target >= _host.catalog.tabs.Count || target == _host.ActiveIndex) return false;
            _target = target;
            _direction = target > _host.ActiveIndex ? 1 : -1;
            _progress = 0;
            _elapsed = 0;
            _screenSize = new Vector2(Screen.width, Screen.height);
            IsActive = true;
            var root = _host.GetRoot(target);
            _host.FitDisplay(_next, target);
            _next.texture = root.sceneCamera.targetTexture;
            _next.gameObject.SetActive(true);
            // 일시정지한 씬은 마지막 화면을 씁니다. 미리보기만을 위해 게임 진행을 재개하지 않습니다.
            if (root.gameObject.activeInHierarchy) root.sceneCamera.enabled = true;
            Apply();
            return true;
        }
        /// <summary>
        /// 시간 배율과 무관하게 전환을 진행하고 완료 시 탭을 확정합니다. 화면 크기가 바뀌면 전환을 취소합니다.
        /// </summary>
        private void Update()
        {
            if (!IsActive) return;
            if (_screenSize != new Vector2(Screen.width, Screen.height))
            { Cancel(); return; }
            _elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _duration));
            _progress = 1 - Mathf.Pow(1 - t, 3);
            Apply();
            if (t >= 1) Finish(true);
        }
        /// <summary>
        /// 전환 진행률로 두 탭 이미지의 수평 위치·홈 시차·프로필 투명도를 갱신합니다.
        /// 뷰포트 크기는 바꾸지 않으므로 렌더 텍스처를 다시 만들지 않습니다.
        /// </summary>
        private void Apply()
        {
            float width = _host.viewport.rect.width;
            SetX(_current, -_direction * _progress * width);
            SetX(_next, _direction * (1 - _progress) * width);
            SetParallax(_host.ActiveIndex, -_direction * _progress);
            SetParallax(_target, _direction * (1 - _progress));
            float from = _host.catalog.tabs[_host.ActiveIndex].showProfile ? 1 : 0;
            float to = _host.catalog.tabs[_target].showProfile ? 1 : 0;
            float profile = Mathf.Lerp(from, to, _progress);
            _host.profileBar.SetActive(profile > 0);
            _profile.alpha = profile;
            _profile.blocksRaycasts = false;
        }
        /// <summary>
        /// 이미지의 세로 여백은 그대로 두고 수평 위치만 바꿉니다.
        /// 위쪽 여백이 있는 이미지에 anchoredPosition 전체를 대입하면 세로 위치가 함께 어긋나기 때문입니다.
        /// </summary>
        private static void SetX(RawImage image, float x)
        {
            var position = image.rectTransform.anchoredPosition;
            position.x = x;
            image.rectTransform.anchoredPosition = position;
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
            SetParallax(_target, 0);
            IsActive = false;
            SetX(_current, 0);
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
