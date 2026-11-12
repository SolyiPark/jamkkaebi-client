using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 보드 위에 같은 두께의 격자 선을 그린다.
    /// </summary>
    /// <remarks>
    /// 타일 이미지에 테두리를 넣으면 덮인 타일끼리는 두 겹, 열린 칸 옆은 한 겹이 되어 선 두께가 달라진다.
    /// 그래서 타일에는 테두리를 넣지 않고, 칸 경계마다 선을 하나씩 따로 그린다.
    /// </remarks>
    public class BoardGridLineView : MonoBehaviour
    {
        [Tooltip("선으로 늘려 쓸 흰색 사각형 스프라이트. 색은 매개체 데이터의 격자 색으로 칠한다.")]
        [SerializeField] private Sprite _lineSprite;
        [Tooltip("칸 한 변 대비 선 두께. 0.047은 256px 타일 기준 12px로, 예전 타일 테두리 두 겹과 같다.")]
        [SerializeField] [Range(0f, 0.2f)] private float _lineWidthRatio = 0.047f;
        [Tooltip("타일(0)보다 위에 그리기 위한 정렬 순서.")]
        [SerializeField] private int _sortingOrder = 10;

        private readonly List<GameObject> _lines = new List<GameObject>();

        /// <summary>
        /// 이전 선을 지우고 새 보드 크기에 맞춰 바깥 테두리와 안쪽 격자 선을 만든다.
        /// </summary>
        /// <param name="parent">타일과 같은 부모. 선도 같은 레이어와 좌표계를 쓴다.</param>
        /// <param name="width">가로 칸 수.</param>
        /// <param name="height">세로 칸 수.</param>
        /// <param name="tileSize">칸 간격(로컬 단위).</param>
        /// <param name="color">선 색.</param>
        public void Build(Transform parent, int width, int height, float tileSize, Color color)
        {
            Clear();
            if (_lineSprite == null)
            {
                Debug.LogWarning($"{nameof(BoardGridLineView)}: _lineSprite가 연결되지 않아 격자 선을 그리지 않습니다.", this);
                return;
            }

            float thickness = tileSize * _lineWidthRatio;
            float boardWidth = width * tileSize;
            float boardHeight = height * tileSize;
            // 타일 중심이 보드 가운데 기준으로 놓이므로, 칸 경계는 (i - width / 2) * tileSize에 있다
            float left = -boardWidth * 0.5f;
            float bottom = -boardHeight * 0.5f;

            // 모서리가 비지 않도록 선 길이에 두께만큼 더한다
            for (int i = 0; i <= width; i++)
            {
                CreateLine(parent, $"GridLine_V{i}", new Vector2(left + i * tileSize, 0f),
                    new Vector2(thickness, boardHeight + thickness), color);
            }
            for (int j = 0; j <= height; j++)
            {
                CreateLine(parent, $"GridLine_H{j}", new Vector2(0f, bottom + j * tileSize),
                    new Vector2(boardWidth + thickness, thickness), color);
            }
        }

        /// <summary>
        /// 만든 격자 선을 모두 지운다.
        /// </summary>
        public void Clear()
        {
            foreach (GameObject line in _lines)
            {
                if (line != null) Destroy(line);
            }
            _lines.Clear();
        }

        private void CreateLine(Transform parent, string lineName, Vector2 localPosition, Vector2 size, Color color)
        {
            GameObject line = new GameObject(lineName);
            line.layer = parent.gameObject.layer;
            line.transform.SetParent(parent, false);
            line.transform.localPosition = localPosition;

            // 스프라이트 원래 크기로 나눠서, 어떤 크기의 사각형 스프라이트를 넣어도 원하는 크기가 되게 한다
            Vector2 spriteSize = _lineSprite.bounds.size;
            line.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);

            SpriteRenderer lineRenderer = line.AddComponent<SpriteRenderer>();
            lineRenderer.sprite = _lineSprite;
            lineRenderer.color = color;
            lineRenderer.sortingOrder = _sortingOrder;

            _lines.Add(line);
        }
    }
}
