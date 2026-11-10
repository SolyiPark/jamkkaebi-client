using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// 여러 장의 안내 그림을 넘겨 보는 도움말 창.
    /// </summary>
    /// <remarks>
    /// 화면 전체를 덮는 오브젝트에 붙인다. 평소에는 꺼 두고 <see cref="Open"/>으로 연다.
    /// 창 틀은 1080×2400 기준으로 만들어졌으므로, 화면이 그보다 낮으면 틀 전체를 같은 비율로 줄여 모두 보이게 한다.
    /// 어떤 화면의 도움말인지는 모른다. 열고 닫힐 때 이벤트만 알리므로, 게임 입력을 막는 일은 쓰는 쪽이 맡는다.
    /// </remarks>
    public class InfoPanel : MonoBehaviour
    {
        [Tooltip("1080×2400 기준으로 배치한 창 틀. 화면 높이에 맞춰 이 오브젝트의 크기를 줄인다.")]
        [SerializeField] private RectTransform _frame;
        [Tooltip("틀에서 실제로 그림이 있는 세로 길이(px). 화면이 이보다 낮으면 줄인다.")]
        [SerializeField] [Min(1f)] private float _visibleHeight = 2240f;
        [SerializeField] [Min(1f)] private float _designWidth = 1080f;

        [Header("페이지")]
        [SerializeField] private Image _pageImage;
        [Tooltip("넘겨 볼 안내 그림. 순서대로 1쪽, 2쪽…")]
        [SerializeField] private Sprite[] _pages;
        [SerializeField] private TMP_Text _pageText;

        [Header("버튼")]
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;

        private int _pageIndex;

        /// <summary>창이 열렸을 때. 뒤에 있는 게임의 입력을 막는 데 쓴다.</summary>
        public event Action Opened;
        /// <summary>창이 닫혔을 때.</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (_previousButton != null) _previousButton.onClick.AddListener(ShowPreviousPage);
            if (_nextButton != null) _nextButton.onClick.AddListener(ShowNextPage);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (_previousButton != null) _previousButton.onClick.RemoveListener(ShowPreviousPage);
            if (_nextButton != null) _nextButton.onClick.RemoveListener(ShowNextPage);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        }

        private void OnEnable()
        {
            FitFrame();
        }

        // 화면 크기나 방향이 바뀌면 Unity가 호출한다
        private void OnRectTransformDimensionsChange()
        {
            FitFrame();
        }

        private void Update()
        {
            // 안드로이드 뒤로 가기 버튼은 Escape 키로 들어온다
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>첫 페이지부터 보여 주며 창을 연다.</summary>
        public void Open()
        {
            if (IsOpen) return;
            gameObject.SetActive(true);
            ShowPage(0);
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void ShowPreviousPage() => ShowPage(_pageIndex - 1);

        private void ShowNextPage() => ShowPage(_pageIndex + 1);

        private void ShowPage(int index)
        {
            int count = _pages != null ? _pages.Length : 0;
            if (count == 0)
            {
                Debug.LogWarning($"{nameof(InfoPanel)}: 보여 줄 페이지가 없습니다.", this);
                return;
            }

            _pageIndex = Mathf.Clamp(index, 0, count - 1);
            if (_pageImage != null) _pageImage.sprite = _pages[_pageIndex];
            if (_pageText != null) _pageText.text = $"{_pageIndex + 1} / {count}";
            // 첫 쪽과 마지막 쪽에서는 더 갈 곳이 없으므로 화살표를 흐리게 하고 누를 수 없게 한다
            if (_previousButton != null) _previousButton.interactable = _pageIndex > 0;
            if (_nextButton != null) _nextButton.interactable = _pageIndex < count - 1;
        }

        /// <summary>
        /// 창 틀이 화면 안에 다 들어오도록 줄인다. 화면이 충분히 크면 원래 크기(1배) 그대로 둔다.
        /// </summary>
        private void FitFrame()
        {
            if (_frame == null) return;
            Rect area = ((RectTransform)transform).rect;
            if (area.width <= 0f || area.height <= 0f) return;

            float scale = Mathf.Min(1f, area.width / _designWidth, area.height / _visibleHeight);
            _frame.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
