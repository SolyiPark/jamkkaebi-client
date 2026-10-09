using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype.Exhibition
{
    public sealed class HousingRadialMenu : MonoBehaviour
    {
        private Camera _camera;
        private RectTransform _canvas;
        private RectTransform _menu;
        private readonly Image[] _buttons = new Image[5];
        private Text _feedback;
        private Vector2 _press;
        public bool IsVisible => _menu && _menu.gameObject.activeSelf;
        public bool IsFloorMenu { get; private set; }
        public int VisibleActionCount => IsVisible ? (IsFloorMenu ? 2 : 3) : 0;
        private static readonly Vector2[] Offsets =
            { new Vector2(0, 105), new Vector2(-95, -55), new Vector2(95, -55),
                new Vector2(-95, 0), new Vector2(95, 0) };

        public void Configure(RectTransform canvas, Camera camera, Font font)
        {
            _canvas = canvas; _camera = camera;
            _menu = UIFactory.Rect("Hold Menu", canvas, Vector2.one * .5f, Vector2.one * .5f);
            _menu.sizeDelta = Vector2.zero;
            var labels = new[] { "이동", "회수", "회전", "배치", "취소" };
            for (int i = 0; i < labels.Length; i++)
            {
                var image = UIFactory.Image(labels[i], _menu, new Color(.25f, .35f, .26f),
                    Vector2.one * .5f, Vector2.one * .5f);
                var rect = image.rectTransform;
                rect.sizeDelta = new Vector2(86, 58); rect.anchoredPosition = Offsets[i];
                // These are swipe targets, not independent pointer receivers.
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
                UIFactory.Label("Label", rect, font, labels[i], 26, Color.white, Vector2.zero, Vector2.one);
                _buttons[i] = image;
            }
            _feedback = UIFactory.Label("Feedback", _menu, font, "", 20, new Color(.2f, .24f, .2f),
                Vector2.one * .5f, Vector2.one * .5f);
            _feedback.rectTransform.sizeDelta = new Vector2(340, 52);
            _feedback.rectTransform.anchoredPosition = new Vector2(0, -118);
            Hide();
        }

        private Vector2 LocalPoint(Vector2 texturePoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, texturePoint, _camera, out var point);
            return point;
        }

        public void Show(Vector2 texturePoint, bool floorMenu = false)
        {
            IsFloorMenu = floorMenu;
            _press = LocalPoint(texturePoint);
            var bounds = _canvas.rect;
            // Keep all three targets visible near platform edges.
            _menu.anchoredPosition = new Vector2(Mathf.Clamp(_press.x, bounds.xMin + 175, bounds.xMax - 175),
                Mathf.Clamp(_press.y, bounds.yMin + 150, bounds.yMax - 145));
            for (int i = 0; i < _buttons.Length; i++)
            {
                _buttons[i].gameObject.SetActive(floorMenu ? i >= 3 : i < 3);
                _buttons[i].color = new Color(.25f, .35f, .26f);
            }
            _menu.SetAsLastSibling();
            _menu.gameObject.SetActive(true);
        }

        public bool TryChoose(Vector2 texturePoint, out HousingAction action)
        {
            action = default;
            var delta = LocalPoint(texturePoint) - _press;
            if (!IsVisible || delta.magnitude < 65) return false;
            float bestDot = .92f;
            int best = -1;
            for (int i = 0; i < Offsets.Length; i++)
            {
                if (!_buttons[i].gameObject.activeSelf) continue;
                var direction = _menu.anchoredPosition + Offsets[i] - _press;
                float dot = Vector2.Dot(delta.normalized, direction.normalized);
                if (dot <= bestDot) continue;
                bestDot = dot; best = i;
            }
            if (best < 0) return false;
            action = (HousingAction)best;
            _buttons[best].color = new Color(.75f, .49f, .19f);
            return true;
        }

        public Vector3 ActionWorldPosition(HousingAction action) => _buttons[(int)action].transform.position;
        public void SetMessage(string message) => _feedback.text = message;
        public void Hide() { if (_menu) _menu.gameObject.SetActive(false); }
    }
}
