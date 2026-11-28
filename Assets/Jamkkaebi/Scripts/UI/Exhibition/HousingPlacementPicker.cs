using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 놓을 대상을 고르는 화면 아래쪽 목록. 선택지의 키와 이름만 다루므로 도깨비와 건물이 함께 쓴다.
    /// 고른 뒤의 동작(빈칸 터치 대기 등)은 하우징이 맡는다.
    /// </summary>
    public sealed class HousingPlacementPicker : MonoBehaviour
    {
        private const int Columns = 2;
        private const int Rows = 4;
        private const int PageSize = Columns * Rows;
        private static readonly Color PanelColor = new Color(.13f, .16f, .14f, .94f);
        private static readonly Color ButtonColor = new Color(.25f, .35f, .26f);
        private static readonly Color SubButtonColor = new Color(.32f, .32f, .32f);

        private RectTransform _canvas;
        private Font _font;
        private RectTransform _panel;
        private readonly List<HousingPlacementOption> _options = new List<HousingPlacementOption>();
        private string _message;
        private Text _messageLabel;
        private int _page;

        public bool IsVisible => _panel && _panel.gameObject.activeSelf;

        /// <summary>지금 화면에 그려진 선택지 수(현재 쪽).</summary>
        public int VisibleOptionCount { get; private set; }

        /// <summary>선택지를 눌렀을 때. 인자는 그 선택지의 Key.</summary>
        public event Action<string> Chosen;

        /// <summary>취소를 눌렀을 때.</summary>
        public event Action Cancelled;

        public void Configure(RectTransform canvas, Font font)
        { _canvas = canvas; _font = font; }

        /// <summary>목록을 띄운다. 이미 떠 있으면 내용을 바꾼다.</summary>
        /// <param name="message">선택지 위에 보일 안내. 불러오는 중·실패·없음 같은 상태를 알린다. 없으면 null.</param>
        /// <exception cref="ArgumentNullException">options가 null인 경우.</exception>
        public void Show(IEnumerable<HousingPlacementOption> options, string message = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var next = new List<HousingPlacementOption>(options);
            // 선택지가 그대로면 버튼을 다시 만들지 않는다. 누르고 있는 버튼이 사라지면 그 누름이 끊겨 선택이 무시된다.
            // 보유 목록을 다시 읽을 때(불러오는 중 → 완료)는 대개 안내만 바뀐다.
            bool unchanged = IsVisible && SameOptions(next);
            _options.Clear(); _options.AddRange(next);
            _message = message;
            if (unchanged)
            {
                if (_messageLabel) _messageLabel.text = _message ?? "";
                return;
            }
            _page = Mathf.Clamp(_page, 0, LastPage);
            Rebuild();
        }

        public void Hide()
        {
            _page = 0; VisibleOptionCount = 0;
            if (_panel) _panel.gameObject.SetActive(false);
        }

        private bool SameOptions(List<HousingPlacementOption> next)
        {
            if (next.Count != _options.Count) return false;
            for (int i = 0; i < next.Count; i++)
                if (next[i].Key != _options[i].Key || next[i].Label != _options[i].Label) return false;
            return true;
        }

        private int LastPage => Mathf.Max(0, (_options.Count - 1) / PageSize);

        private void Rebuild()
        {
            if (!_canvas) return;
            if (_panel)
            {
                // 에디터 테스트에서는 Destroy를 쓸 수 없다.
                if (Application.isPlaying) Destroy(_panel.gameObject);
                else DestroyImmediate(_panel.gameObject);
            }
            var background = UIFactory.Image("Placement Picker", _canvas, PanelColor, Vector2.zero, new Vector2(1, .42f));
            // 목록 위를 누른 것이 뒤의 전시관 바닥으로 넘어가지 않게 막는다.
            // 이 캔버스는 하우징 루트의 자식이라, 받는 곳이 없으면 누름이 루트의 바닥 입력까지 올라간다. 여기서 받아 끊는다.
            background.raycastTarget = true;
            background.gameObject.AddComponent<EventTrigger>();
            _panel = background.rectTransform;

            UIFactory.Label("Title", _panel, _font, "무엇을 놓을까요?", 26, Color.white,
                new Vector2(0, .86f), Vector2.one);
            // 안내가 없어도 만들어 둔다. 버튼을 다시 만들지 않고 글만 바꾸기 위해서다.
            _messageLabel = UIFactory.Label("Message", _panel, _font, _message ?? "", 20, new Color(.85f, .85f, .8f),
                    new Vector2(.05f, .74f), new Vector2(.95f, .86f));

            VisibleOptionCount = 0;
            int first = _page * PageSize;
            for (int i = 0; i < PageSize && first + i < _options.Count; i++)
            {
                var option = _options[first + i];
                int column = i % Columns, row = i / Columns;
                float top = .74f - row * .14f;
                var min = new Vector2(.05f + column * .46f, top - .12f);
                var max = new Vector2(.49f + column * .46f, top);
                AddButton(option.Label, min, max, ButtonColor, () => Chosen?.Invoke(option.Key));
                VisibleOptionCount++;
            }

            AddButton("취소", new Vector2(.05f, .03f), new Vector2(.35f, .15f), SubButtonColor, () => Cancelled?.Invoke());
            if (LastPage > 0)
            {
                AddButton(_page < LastPage ? "다음" : "처음", new Vector2(.65f, .03f), new Vector2(.95f, .15f), SubButtonColor,
                    () => { _page = _page < LastPage ? _page + 1 : 0; Rebuild(); });
                UIFactory.Label("Page", _panel, _font, $"{_page + 1}/{LastPage + 1}", 20, Color.white,
                    new Vector2(.4f, .03f), new Vector2(.6f, .15f));
            }

            _panel.SetAsLastSibling();
            // 캔버스의 지금 레이어를 따른다. 셸이 탭 레이어를 이 컴포넌트 설정보다 늦게 정할 수 있어 값을 미리 기억해 두지 않는다.
            MirrorSceneRoot.SetLayer(_panel, _canvas.gameObject.layer);
        }

        private void AddButton(string label, Vector2 min, Vector2 max, Color color, Action clicked)
        {
            var image = UIFactory.Image(label, _panel, color, min, max);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            UIFactory.Label("Label", image.rectTransform, _font, label, 22, Color.white, Vector2.zero, Vector2.one);
        }
    }
}
