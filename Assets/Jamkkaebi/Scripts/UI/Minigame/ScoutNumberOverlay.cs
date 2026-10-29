using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using TMPro;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 정찰한 칸 위에 주변 위협 수를 숫자로 남겨 둔다.
    /// </summary>
    /// <remarks>
    /// 숫자는 판이 끝날 때까지 남는다. 그 칸이 열려 폴리오미노 조각 등 그림이 나오면 그림을 가리지 않도록 지운다.
    /// 보드는 월드 공간에, 숫자는 화면 위 캔버스에 있으므로 매 프레임 타일 위치를 따라간다.
    /// </remarks>
    public class ScoutNumberOverlay : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("보드를 비추는 카메라. 타일 위치를 화면 좌표로 바꿀 때 쓴다.")]
        [SerializeField] private Camera _boardCamera;
        [Tooltip("복제해서 쓸 숫자 글자. 비활성 상태로 둔다.")]
        [SerializeField] private TMP_Text _template;

        [Header("모양")]
        [Tooltip("칸 크기 대비 글자 크기.")]
        [SerializeField] [Range(0.2f, 1f)] private float _sizeRatio = 0.55f;
        [Tooltip("어떤 타일 색 위에서도 읽히도록 두르는 외곽선 두께.")]
        [SerializeField] [Range(0f, 0.5f)] private float _outlineWidth = 0.2f;
        [SerializeField] private Color _outlineColor = Color.white;

        private readonly Dictionary<Vector2Int, TMP_Text> _labels = new Dictionary<Vector2Int, TMP_Text>();
        private RectTransform _area;
        private Canvas _canvas;
        private MinigameSession _shownSession;

        private void Awake()
        {
            _area = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (_template != null) _template.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_adapter != null) _adapter.ScoutRevealed += Add;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.ScoutRevealed -= Add;
        }

        private void Add(Vector2Int coord, int threatCount)
        {
            if (_template == null) return;

            if (!_labels.TryGetValue(coord, out TMP_Text label))
            {
                label = Instantiate(_template, _area);
                label.gameObject.SetActive(true);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                // 외곽선을 넣으면 이 글자 전용 머티리얼이 만들어진다. 템플릿과 공유 머티리얼은 바뀌지 않는다
                label.outlineWidth = _outlineWidth;
                label.outlineColor = _outlineColor;
                _labels.Add(coord, label);
            }

            // 같은 칸을 다시 정찰해도 결과는 같으므로 숫자만 덮어쓴다
            label.text = threatCount.ToString();
        }

        private void LateUpdate()
        {
            MinigameSession session = _adapter != null ? _adapter.Session : null;

            // 새 판이 시작되면 이전 판의 숫자를 모두 지운다
            if (session != _shownSession)
            {
                _shownSession = session;
                Clear();
            }
            if (session == null || _labels.Count == 0 || _boardCamera == null) return;

            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            List<Vector2Int> removed = null;

            foreach (KeyValuePair<Vector2Int, TMP_Text> pair in _labels)
            {
                Vector2Int coord = pair.Key;
                TMP_Text label = pair.Value;

                Tile tile = session.Grid.GetTile(coord.x, coord.y);
                if (tile.IsRevealed && tile.Content != TileContent.Empty)
                {
                    (removed ??= new List<Vector2Int>()).Add(coord);
                    continue;
                }
                if (!_adapter.TryGetTileWorldPosition(coord, out Vector3 world)) continue;

                // 타일 중심과 한 칸 오른쪽 점을 화면에 찍어 칸 크기(픽셀)를 구하고, 캔버스 단위로 바꾼다
                Vector2 center = _boardCamera.WorldToScreenPoint(world);
                Vector2 side = _boardCamera.WorldToScreenPoint(world + Vector3.right * _adapter.TileSize);
                float cellInCanvas = Mathf.Abs(side.x - center.x) / scaleFactor;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, center, uiCamera, out Vector2 local))
                {
                    label.rectTransform.anchoredPosition = local - _area.rect.center;
                }
                label.rectTransform.sizeDelta = new Vector2(cellInCanvas, cellInCanvas);
                label.fontSize = cellInCanvas * _sizeRatio;
            }

            if (removed == null) return;
            foreach (Vector2Int coord in removed)
            {
                Destroy(_labels[coord].gameObject);
                _labels.Remove(coord);
            }
        }

        private void Clear()
        {
            foreach (TMP_Text label in _labels.Values)
            {
                if (label != null) Destroy(label.gameObject);
            }
            _labels.Clear();
        }
    }
}
