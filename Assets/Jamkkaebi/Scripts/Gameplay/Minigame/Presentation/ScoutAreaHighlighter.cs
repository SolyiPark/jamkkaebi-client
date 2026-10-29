using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 정찰하면 정찰 범위(3×3)를 잠깐 밝게 덮었다가 사라지게 한다.
    /// </summary>
    /// <remarks>
    /// 범위는 세션의 정찰 규칙(<see cref="Core.MinigameSession.GetScoutArea"/>)과 같은 칸을 쓴다.
    /// 덮개는 타일 위, 격자 선 아래에 그려서 선이 가려지지 않게 한다.
    /// </remarks>
    public class ScoutAreaHighlighter : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("덮개를 놓을 부모. 타일과 같은 보드 컨테이너를 쓴다.")]
        [SerializeField] private Transform _parent;
        [Tooltip("칸을 덮을 흰색 사각형 스프라이트.")]
        [SerializeField] private Sprite _sprite;

        [Header("연출")]
        [SerializeField] private Color _color = new Color(1f, 0.96f, 0.75f, 0.55f);
        [Tooltip("타일(0)보다 위, 격자 선(10)보다 아래.")]
        [SerializeField] private int _sortingOrder = 5;
        [SerializeField] [Min(0f)] private float _fadeInDuration = 0.1f;
        [SerializeField] [Min(0f)] private float _holdDuration = 0.35f;
        [SerializeField] [Min(0.01f)] private float _fadeOutDuration = 0.6f;

        private void OnEnable()
        {
            if (_adapter != null) _adapter.ScoutRevealed += Highlight;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.ScoutRevealed -= Highlight;
        }

        private void Highlight(Vector2Int origin, int threatCount)
        {
            if (_sprite == null || _parent == null || _adapter.Session == null) return;

            var renderers = new List<SpriteRenderer>();
            Vector2 spriteSize = _sprite.bounds.size;

            foreach (Vector2Int coord in _adapter.Session.GetScoutArea(origin))
            {
                if (!_adapter.TryGetTileWorldPosition(coord, out Vector3 position)) continue;

                var cover = new GameObject($"ScoutHighlight_{coord.x}_{coord.y}");
                cover.layer = _parent.gameObject.layer;
                cover.transform.SetParent(_parent, false);
                cover.transform.position = position;
                // 칸 간격과 같은 크기로 덮어, 이웃한 덮개끼리 틈 없이 3×3 한 덩어리로 보이게 한다
                cover.transform.localScale = new Vector3(_adapter.TileSize / spriteSize.x, _adapter.TileSize / spriteSize.y, 1f);

                var spriteRenderer = cover.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = _sprite;
                spriteRenderer.sortingOrder = _sortingOrder;
                spriteRenderer.color = WithAlpha(0f);
                renderers.Add(spriteRenderer);
            }

            if (renderers.Count > 0) StartCoroutine(Animate(renderers));
        }

        private IEnumerator Animate(List<SpriteRenderer> renderers)
        {
            float total = _fadeInDuration + _holdDuration + _fadeOutDuration;
            float elapsed = 0f;

            while (elapsed < total)
            {
                float alpha;
                if (elapsed < _fadeInDuration) alpha = elapsed / _fadeInDuration;
                else if (elapsed < _fadeInDuration + _holdDuration) alpha = 1f;
                else alpha = 1f - (elapsed - _fadeInDuration - _holdDuration) / _fadeOutDuration;

                foreach (SpriteRenderer spriteRenderer in renderers)
                {
                    // 새 판을 시작하면 보드가 지워지므로 이미 사라진 덮개는 건너뛴다
                    if (spriteRenderer != null) spriteRenderer.color = WithAlpha(alpha);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                if (spriteRenderer != null) Destroy(spriteRenderer.gameObject);
            }
        }

        // 설정한 색의 알파를 최대값으로 보고, 연출 진행도(0~1)를 곱한다
        private Color WithAlpha(float progress) => new Color(_color.r, _color.g, _color.b, _color.a * Mathf.Clamp01(progress));
    }
}
