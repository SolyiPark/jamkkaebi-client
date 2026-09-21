using UnityEngine;

namespace MobilePrototype
{
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        private Rect previous;
        private Vector2 size;
        private void Update()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            Vector2 currentSize = new Vector2(Screen.width, Screen.height);
            if (area == previous && currentSize == size) return;
            previous = area;
            size = currentSize;
            var rect = (RectTransform)transform;
            rect.anchorMin = area.min / size;
            rect.anchorMax = area.max / size;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
