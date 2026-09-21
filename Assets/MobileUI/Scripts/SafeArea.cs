using UnityEngine;

namespace MobilePrototype
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private Rect previous;
        private Vector2 size;
        private RectTransform _rectTransform;

        private void OnEnable()
        {
            if (!TryGetComponent(out _rectTransform))
            {
                Debug.LogError("SafeArea requires a RectTransform.", this);
                enabled = false;
                return;
            }
            previous = default;
            size = default;
        }

        private void Update()
        {
            if (!_rectTransform || Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            Vector2 currentSize = new Vector2(Screen.width, Screen.height);
            if (area == previous && currentSize == size) return;
            previous = area;
            size = currentSize;
            _rectTransform.anchorMin = area.min / size;
            _rectTransform.anchorMax = area.max / size;
            _rectTransform.offsetMin = _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
