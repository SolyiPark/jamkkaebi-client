using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 보드 영역을 먼저 확보하고, 남은 높이에 HUD가 들어가도록 캔버스 배율을 정한다.
    /// </summary>
    /// <remarks>
    /// 시안(1080×2400)에서는 HUD가 원래 크기 그대로다. 그보다 짧은 화면에서는 보드 대신 HUD를 줄이고,
    /// HUD가 최소 배율에 닿으면 그때부터 보드가 줄어든다. 보드 자체는 BoardCameraFitter가 HUD 경계 사이에 맞춘다.
    /// 배율은 화면 크기에만 따르고 난이도와는 무관하므로, 판이 바뀌어도 HUD 크기는 그대로다.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public class HudCanvasScaler : MonoBehaviour
    {
        [Tooltip("시안의 가로 폭(기준 단위).")]
        [SerializeField] private float _referenceWidth = 1080f;
        [Tooltip("확보할 보드 영역의 높이(기준 단위). 시안에서 형상 패널 아래부터 도구 버튼 위까지.")]
        [SerializeField] private float _boardAreaHeight = 1140f;
        [Tooltip("HUD가 차지하는 높이(기준 단위). 보드 위 영역 + 보드 아래 영역.")]
        [SerializeField] private float _hudHeight = 1260f;
        [Tooltip("가로 기준 배율 대비 HUD를 줄일 수 있는 최소 비율. 도구 버튼을 누를 수 있는 크기를 지킨다.")]
        [SerializeField] [Range(0.3f, 1f)] private float _minHudScale = 0.7f;

        private CanvasScaler _scaler;

        private void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        // CanvasScaler가 Update에서 배율을 캔버스에 적용하므로 같은 프레임에 반영되도록 Update에서 정한다
        private void Update() => Apply();

        private void Apply()
        {
            if (_scaler == null || _referenceWidth <= 0f || _hudHeight <= 0f) return;

            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) return;

            // 1. 시안처럼 가로를 꽉 채울 때의 배율 (화면 픽셀 / 기준 단위)
            float widthScale = safe.width / _referenceWidth;
            // 2. 그 배율로 보드 영역을 확보했을 때 남는 높이에 HUD가 들어가는 배율
            float fitScale = (safe.height - _boardAreaHeight * widthScale) / _hudHeight;
            // 3. 시안보다 크게 키우지 않고, 최소 배율 아래로는 줄이지 않는다
            float scale = Mathf.Clamp(fitScale, widthScale * _minHudScale, widthScale);

            if (_scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            }
            // 값이 같을 때 다시 넣으면 에디터에서 씬이 계속 변경된 것으로 표시되므로 바뀔 때만 넣는다
            if (!Mathf.Approximately(_scaler.scaleFactor, scale))
            {
                _scaler.scaleFactor = scale;
            }
        }
    }
}
