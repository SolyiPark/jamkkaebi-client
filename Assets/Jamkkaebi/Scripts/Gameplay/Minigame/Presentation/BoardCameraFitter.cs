using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 보드 전체가 화면 안에 들어오도록 직교 카메라의 크기와 위치를 맞춘다.
    /// </summary>
    /// <remarks>
    /// 난이도마다 보드 칸 수가 다르고 기기마다 화면 비율이 다르므로 카메라 크기를 고정하면 보드가 잘린다.
    /// 화면 위·아래에는 HUD와 도구 버튼 자리를 비워 두고, 남은 영역 가운데에 보드를 놓는다.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public class BoardCameraFitter : MonoBehaviour
    {
        [Tooltip("화면 위쪽에서 HUD용으로 비워 둘 높이 비율.")]
        [SerializeField] [Range(0f, 0.5f)] private float _topReservedRatio = 0.18f;
        [Tooltip("화면 아래쪽에서 도구 버튼용으로 비워 둘 높이 비율.")]
        [SerializeField] [Range(0f, 0.5f)] private float _bottomReservedRatio = 0.18f;
        [Tooltip("보드 좌우·위아래에 둘 여백. 칸 한 변 대비 비율.")]
        [SerializeField] [Min(0f)] private float _marginInTiles = 0.3f;

        private Camera _camera;
        private Transform _board;
        private Vector2 _boardSize;
        private float _lastAspect;
        private bool _hasBoard;

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
            _hasBoard = true;
            Apply();
        }

        // 배경(BackgroundView)이 LateUpdate에서 카메라를 따라가므로, 그보다 먼저인 Update에서 맞춘다
        private void Update()
        {
            if (_hasBoard && !Mathf.Approximately(_lastAspect, _camera.aspect))
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (!enabled || _camera == null || _board == null) return;

            // 1. 보드가 들어갈 수 있는 화면 높이 비율
            float usableRatio = Mathf.Max(0.1f, 1f - _topReservedRatio - _bottomReservedRatio);

            // 2. 세로·가로 각각 보드가 들어가는 데 필요한 카메라 크기를 구하고 큰 쪽을 쓴다
            //    orthographicSize는 화면 높이의 절반이고, 화면 폭은 높이 × aspect다
            float sizeForHeight = _boardSize.y / (2f * usableRatio);
            float sizeForWidth = _boardSize.x / (2f * _camera.aspect);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            _camera.orthographicSize = size;

            // 3. 보드 중심이 비워 둔 영역을 뺀 나머지 영역의 가운데에 오도록 카메라를 옮긴다
            //    usableCenter는 화면 아래에서부터의 비율(0~1)이고, 화면 가운데는 0.5다
            float usableCenter = _bottomReservedRatio + usableRatio * 0.5f;
            Vector3 position = _camera.transform.position;
            position.x = _board.position.x;
            position.y = _board.position.y - (usableCenter - 0.5f) * 2f * size;
            _camera.transform.position = position;

            _lastAspect = _camera.aspect;
        }
    }
}
