using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 보드 전체가 화면 안에 들어오도록 직교 카메라의 크기와 위치를 맞춘다.
    /// </summary>
    /// <remarks>
    /// 난이도마다 보드 칸 수가 다르고 기기마다 화면 비율이 다르므로 카메라 크기를 고정하면 보드가 잘린다.
    /// 화면 위·아래에는 HUD와 도구 버튼 자리를 비워 두고, 남은 영역 가운데에 보드를 놓는다.
    /// UI는 픽셀 단위로 위·아래에 붙어 있어 화면 높이가 달라지면 차지하는 비율도 달라진다.
    /// 그래서 경계 RectTransform이 연결되어 있으면 매번 실제 화면 위치를 읽어 비율을 구한다.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public class BoardCameraFitter : MonoBehaviour
    {
        [Header("보드 영역 경계 (연결하면 아래 비율 대신 사용)")]
        [Tooltip("이 영역의 아래 끝까지를 위쪽 UI가 차지한다.")]
        [SerializeField] private RectTransform _topBoundary;
        [Tooltip("이 영역의 위 끝까지를 아래쪽 UI가 차지한다.")]
        [SerializeField] private RectTransform _bottomBoundary;

        [Header("경계가 없을 때 쓰는 고정 비율")]
        [Tooltip("화면 위쪽에서 HUD용으로 비워 둘 높이 비율.")]
        [SerializeField] [Range(0f, 0.5f)] private float _topReservedRatio = 0.18f;
        [Tooltip("화면 아래쪽에서 도구 버튼용으로 비워 둘 높이 비율.")]
        [SerializeField] [Range(0f, 0.5f)] private float _bottomReservedRatio = 0.18f;
        [Tooltip("보드 좌우·위아래에 둘 여백. 칸 한 변 대비 비율.")]
        [SerializeField] [Min(0f)] private float _marginInTiles = 0.3f;

        private Camera _camera;
        private Transform _board;
        private Vector2 _boardSize;
        private float _boardTileWidth;   // 여백을 뺀 보드 폭(보드 Transform 기준 단위)
        private float _lastAspect;
        private float _lastTopRatio;
        private float _lastBottomRatio;
        private bool _hasBoard;
        private static readonly Vector3[] Corners = new Vector3[4];

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (!_camera.orthographic)
            {
                Debug.LogError($"{nameof(BoardCameraFitter)}: 직교 카메라에서만 동작합니다.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 새 보드에 맞춰 카메라를 조정한다. 이후 화면 비율이 바뀌면 자동으로 다시 맞춘다.
        /// </summary>
        /// <param name="board">보드 중심이 되는 Transform. 타일이 이 위치를 기준으로 가운데 정렬되어 있어야 한다.</param>
        /// <param name="width">가로 칸 수.</param>
        /// <param name="height">세로 칸 수.</param>
        /// <param name="tileSize">칸 간격(월드 단위).</param>
        public void Fit(Transform board, int width, int height, float tileSize)
        {
            _board = board;
            float margin = _marginInTiles * tileSize * 2f;
            _boardSize = new Vector2(width * tileSize + margin, height * tileSize + margin);
            _boardTileWidth = width * tileSize;
            _hasBoard = true;
            Apply();
        }

        // 배경(BackgroundView)이 LateUpdate에서 카메라를 따라가므로, 그보다 먼저인 Update에서 맞춘다
        private void Update()
        {
            if (!_hasBoard) return;

            // 화면 비율뿐 아니라 UI 위치(SafeArea, Canvas Scaler 적용 등)가 바뀌어도 다시 맞춘다
            GetReservedRatios(out float top, out float bottom);
            if (!Mathf.Approximately(_lastAspect, _camera.aspect)
                || Mathf.Abs(_lastTopRatio - top) > 0.001f
                || Mathf.Abs(_lastBottomRatio - bottom) > 0.001f)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (!enabled || _camera == null || _board == null) return;

            // 1. 보드가 들어갈 수 있는 화면 높이 비율
            GetReservedRatios(out float topRatio, out float bottomRatio);
            float usableRatio = Mathf.Max(0.1f, 1f - topRatio - bottomRatio);

            // 2. 세로·가로 각각 보드가 들어가는 데 필요한 카메라 크기를 구하고 큰 쪽을 쓴다
            //    orthographicSize는 화면 높이의 절반이고, 화면 폭은 높이 × aspect다
            float sizeForHeight = _boardSize.y / (2f * usableRatio);
            float sizeForWidth = _boardSize.x / (2f * _camera.aspect);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            _camera.orthographicSize = size;

            // 3. 보드 중심이 비워 둔 영역을 뺀 나머지 영역의 가운데에 오도록 카메라를 옮긴다
            //    usableCenter는 화면 아래에서부터의 비율(0~1)이고, 화면 가운데는 0.5다
            float usableCenter = bottomRatio + usableRatio * 0.5f;
            Vector3 position = _camera.transform.position;
            position.x = _board.position.x;
            position.y = _board.position.y - (usableCenter - 0.5f) * 2f * size;
            _camera.transform.position = position;

            _lastAspect = _camera.aspect;
            _lastTopRatio = topRatio;
            _lastBottomRatio = bottomRatio;
        }

        /// <summary>
        /// 보드(타일 영역)의 왼쪽·오른쪽 끝이 화면에서 몇 픽셀에 있는지 구한다. 화면 왼쪽이 0이다.
        /// </summary>
        /// <remarks>HUD 패널 등을 보드 폭에 맞출 때 쓴다.</remarks>
        /// <returns>맞출 보드가 있으면 true.</returns>
        public bool TryGetBoardScreenEdges(out float left, out float right)
        {
            left = right = 0f;
            if (!_hasBoard || _board == null || _camera == null) return false;

            Vector3 halfWidth = _board.right * (_boardTileWidth * 0.5f * _board.lossyScale.x);
            left = _camera.WorldToScreenPoint(_board.position - halfWidth).x;
            right = _camera.WorldToScreenPoint(_board.position + halfWidth).x;
            return true;
        }

        /// <summary>
        /// 화면 위·아래에서 UI가 차지하는 높이 비율을 구한다. 경계가 연결되어 있으면 실제 화면 위치를 쓴다.
        /// </summary>
        private void GetReservedRatios(out float top, out float bottom)
        {
            Rect pixels = _camera.pixelRect;
            top = _topBoundary != null
                ? Mathf.Clamp01((pixels.yMax - GetScreenEdgeY(_topBoundary, lowest: true)) / pixels.height)
                : _topReservedRatio;
            bottom = _bottomBoundary != null
                ? Mathf.Clamp01((GetScreenEdgeY(_bottomBoundary, lowest: false) - pixels.yMin) / pixels.height)
                : _bottomReservedRatio;
        }

        /// <summary>
        /// RectTransform 네 꼭짓점 중 화면에서 가장 낮은(또는 높은) y 좌표를 픽셀로 구한다. 화면 아래가 0이다.
        /// </summary>
        private static float GetScreenEdgeY(RectTransform rect, bool lowest)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            // Overlay 캔버스는 월드 좌표가 곧 화면 픽셀이라 카메라 없이 변환한다
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            rect.GetWorldCorners(Corners);
            float edge = lowest ? float.MaxValue : float.MinValue;
            foreach (Vector3 corner in Corners)
            {
                float y = RectTransformUtility.WorldToScreenPoint(uiCamera, corner).y;
                edge = lowest ? Mathf.Min(edge, y) : Mathf.Max(edge, y);
            }
            return edge;
        }
    }
}
