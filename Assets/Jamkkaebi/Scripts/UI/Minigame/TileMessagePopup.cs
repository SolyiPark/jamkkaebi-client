using System.Collections;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using TMPro;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 칸 위에 짧은 말풍선을 띄웠다가, 위로 떠오르며 사라지게 한다.
    /// </summary>
    /// <remarks>
    /// 두 가지 상황에 쓴다.
    /// - 정찰 결과: "주변 위협 N개"
    /// - 누른 칸에서 도구를 쓸 수 없을 때: "이미 열린 칸이에요" 등
    /// 말풍선은 비활성 상태의 템플릿을 복제해서 만든다. 정찰 말풍선은 여러 개가 겹쳐 떠도 각자 사라지지만,
    /// 안내 말풍선은 같은 곳을 연달아 눌러도 쌓이지 않도록 이전 것을 지우고 새로 띄운다.
    /// </remarks>
    public class TileMessagePopup : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("보드를 비추는 카메라. 타일 위치를 화면 좌표로 바꿀 때 쓴다.")]
        [SerializeField] private Camera _boardCamera;
        [Tooltip("복제해서 쓸 말풍선. 비활성 상태로 두고, 아래쪽 가운데(pivot 0.5, 0)를 기준점으로 둔다.")]
        [SerializeField] private RectTransform _template;

        [Header("문구")]
        [SerializeField] private string _alreadyRevealedMessage = "이미 열린 칸이에요";
        [SerializeField] private string _noUsesRemainingMessage = "남은 횟수가 없어요";

        [Header("연출")]
        [Tooltip("타일 중심에서 말풍선 아래 끝까지의 거리(캔버스 단위).")]
        [SerializeField] private float _startOffset = 40f;
        [Tooltip("사라지는 동안 위로 올라가는 거리(캔버스 단위).")]
        [SerializeField] private float _riseDistance = 90f;
        [SerializeField] [Min(0.1f)] private float _duration = 1.2f;
        [Tooltip("이 비율까지는 선명하게 보이고, 그 뒤로 서서히 투명해진다.")]
        [SerializeField] [Range(0f, 1f)] private float _holdRatio = 0.5f;

        private RectTransform _area;
        private Canvas _canvas;
        // 마지막으로 띄운 안내 말풍선. 연달아 누르면 이것을 지우고 새로 띄운다
        private RectTransform _lastNotice;

        private void Awake()
        {
            _area = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (_template != null) _template.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_adapter == null) return;
            _adapter.ScoutRevealed += ShowScoutResult;
            _adapter.TileActionRejected += ShowRejection;
        }

        private void OnDisable()
        {
            if (_adapter == null) return;
            _adapter.ScoutRevealed -= ShowScoutResult;
            _adapter.TileActionRejected -= ShowRejection;
        }

        private void ShowScoutResult(Vector2Int coord, int threatCount)
        {
            Show(coord, threatCount > 0 ? $"주변 위협 {threatCount}개" : "주변에 위협이 없어요");
        }

        private void ShowRejection(Vector2Int coord, TileActionRejectReason reason)
        {
            string message = reason switch
            {
                TileActionRejectReason.AlreadyRevealed => _alreadyRevealedMessage,
                TileActionRejectReason.NoUsesRemaining => _noUsesRemainingMessage,
                _ => null
            };
            if (string.IsNullOrEmpty(message)) return;

            if (_lastNotice != null) Destroy(_lastNotice.gameObject);
            _lastNotice = Show(coord, message);
        }

        /// <summary>
        /// coord 칸 위에 message 말풍선을 띄운다.
        /// </summary>
        /// <returns>띄운 말풍선. 띄울 수 없으면 null.</returns>
        private RectTransform Show(Vector2Int coord, string message)
        {
            if (_template == null || _boardCamera == null) return null;
            if (!_adapter.TryGetTileWorldPosition(coord, out Vector3 worldPosition)) return null;

            // 1. 타일의 월드 좌표 → 화면 좌표 → 이 영역 안의 좌표
            Vector2 screenPoint = _boardCamera.WorldToScreenPoint(worldPosition);
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screenPoint, uiCamera, out Vector2 localPoint)) return null;

            // 2. 템플릿을 복제해 문구를 넣는다
            RectTransform popup = Instantiate(_template, _area);
            popup.gameObject.SetActive(true);
            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 start = localPoint - _area.rect.center + new Vector2(0f, _startOffset);
            popup.anchoredPosition = start;

            TMP_Text text = popup.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = message;

            CanvasGroup group = popup.GetComponent<CanvasGroup>();
            if (group == null) group = popup.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;   // 떠 있는 동안 타일·버튼 입력을 막지 않는다

            StartCoroutine(Animate(popup, group, start));
            return popup;
        }

        private IEnumerator Animate(RectTransform popup, CanvasGroup group, Vector2 start)
        {
            float elapsed = 0f;
            while (elapsed < _duration && popup != null)
            {
                float t = elapsed / _duration;
                // 처음에 빠르게 떠오르고 끝으로 갈수록 느려지게 한다
                float eased = 1f - (1f - t) * (1f - t);
                popup.anchoredPosition = start + new Vector2(0f, _riseDistance * eased);
                group.alpha = t < _holdRatio ? 1f : 1f - (t - _holdRatio) / (1f - _holdRatio);

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (popup != null) Destroy(popup.gameObject);
        }
    }
}
