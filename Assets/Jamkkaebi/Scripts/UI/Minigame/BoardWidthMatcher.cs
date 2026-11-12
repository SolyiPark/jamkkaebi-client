using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 지정한 UI들의 좌우 끝을 보드(타일 영역)의 좌우 끝에 맞춘다.
    /// </summary>
    /// <remarks>
    /// 보드 폭은 난이도와 화면 비율에 따라 달라지므로, 손상도 바·형상 패널이 항상 보드와 같은 폭으로 보이도록 매 프레임 맞춘다.
    /// 대상의 높이와 세로 위치는 건드리지 않는다. 늘어나는 이미지는 Sliced로 두어야 둥근 모서리가 유지된다.
    /// </remarks>
    public class BoardWidthMatcher : MonoBehaviour
    {
        [SerializeField] private BoardCameraFitter _fitter;
        [Tooltip("보드 폭에 맞출 UI. 위쪽 기준(pivot y=1)으로 배치되어 있어야 한다.")]
        [SerializeField] private RectTransform[] _targets;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        // 카메라는 BoardCameraFitter가 Update에서 맞추므로, 그 뒤인 LateUpdate에서 화면 위치를 읽는다
        private void LateUpdate()
        {
            if (_fitter == null || _targets == null) return;
            if (!_fitter.TryGetBoardScreenEdges(out float left, out float right)) return;

            // Overlay 캔버스는 화면 좌표를 카메라 없이 변환한다
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            foreach (RectTransform target in _targets)
            {
                if (target == null || !(target.parent is RectTransform parent)) continue;

                // 화면 x 좌표를 부모 RectTransform 안의 좌표로 바꾼다. y는 아무 값이나 써도 x 결과에는 영향이 없다
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(left, 0f), uiCamera, out Vector2 localLeft);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(right, 0f), uiCamera, out Vector2 localRight);

                // 왼쪽 위를 기준점으로 두고, 기준점에서 보드 왼쪽 끝까지의 거리와 폭을 정한다
                target.anchorMin = new Vector2(0f, target.anchorMin.y);
                target.anchorMax = new Vector2(0f, target.anchorMax.y);
                target.pivot = new Vector2(0f, target.pivot.y);
                target.anchoredPosition = new Vector2(localLeft.x - parent.rect.xMin, target.anchoredPosition.y);
                target.sizeDelta = new Vector2(localRight.x - localLeft.x, target.sizeDelta.y);
            }
        }
    }
}
