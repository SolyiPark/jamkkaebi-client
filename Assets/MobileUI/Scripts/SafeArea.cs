using UnityEngine;

namespace Jamkkaebi.Scripts.UI
{
    /// <summary>
    /// 이 RectTransform을 기기의 안전 영역(노치·홈 인디케이터 제외 영역)에 맞춘다.
    /// 화면 회전이나 해상도 변경도 감지해서 다시 맞춘다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rectTransform;

        // 마지막으로 적용한 값. 바뀌지 않았으면 매 프레임 다시 계산하지 않는다.
        private Rect _appliedSafeArea;
        private Vector2 _appliedScreenSize;

        private void OnEnable()
        {
            if (!TryGetComponent(out _rectTransform))
            {
                Debug.LogError("SafeArea requires a RectTransform.", this);
                enabled = false;
                return;
            }

            // 비활성 상태에서 화면이 바뀌었을 수 있으므로 다음 Update에서 반드시 다시 적용한다.
            _appliedSafeArea = default;
            _appliedScreenSize = default;
        }

        private void Update()
        {
            if (!_rectTransform || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (safeArea == _appliedSafeArea && screenSize == _appliedScreenSize)
            {
                return;
            }

            ApplySafeArea(safeArea, screenSize);
        }

        private void ApplySafeArea(Rect safeArea, Vector2 screenSize)
        {
            _appliedSafeArea = safeArea;
            _appliedScreenSize = screenSize;

            // 픽셀 좌표를 0~1 비율로 바꿔 앵커에 넣고, 오프셋은 0으로 맞춰 앵커 영역을 꽉 채운다.
            _rectTransform.anchorMin = safeArea.min / screenSize;
            _rectTransform.anchorMax = safeArea.max / screenSize;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
