using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype
{
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }
        public static Image Image(string name, Transform parent, Color color, Vector2 min, Vector2 max,
            Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            var image = Rect(name, parent, min, max, offsetMin, offsetMax).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }
        public static Text Label(string name, Transform parent, Font font, string text, int size, Color color,
            Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Rect(name, parent, min, max, offsetMin, offsetMax).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
    }
}
