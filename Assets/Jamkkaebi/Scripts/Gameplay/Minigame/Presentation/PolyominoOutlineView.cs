using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 폴리오미노 하나의 칸이 모두 열리면 그 모양의 바깥 테두리를 금색으로 그린다.
    /// </summary>
    /// <remarks>
    /// 모양끼리 붙어 있으면 어디까지가 한 모양인지 헷갈리므로, 완성된 모양의 경계를 따로 표시한다.
    /// 테두리는 모양에 속한 칸의 네 변 중 이웃 칸이 같은 모양이 아닌 변만 골라 그린다(안쪽 변은 그리지 않는다).
    /// "열렸다"는 세션 상태가 아니라 화면에서 타일이 깨진 시점(<see cref="MinigameSessionAdapter.TileBroken"/>)으로 판단한다.
    /// 그래야 도움 타일 연쇄로 늦게 깨지는 칸까지 다 보인 뒤에 테두리가 나타난다.
    /// </remarks>
    public class PolyominoOutlineView : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("테두리를 놓을 부모. 타일과 같은 보드 컨테이너를 쓴다.")]
        [SerializeField] private Transform _parent;
        [Tooltip("흰색 사각형 스프라이트. 색을 곱해 쓴다.")]
        [SerializeField] private Sprite _sprite;

        [Header("모양")]
        [SerializeField] private Color _color = new Color(1f, 0.78f, 0.2f, 1f);
        [Tooltip("테두리 두께(타일 한 칸 = 1). 격자 선(약 0.047)보다 굵게 해야 눈에 띈다.")]
        [SerializeField] [Range(0.02f, 0.3f)] private float _thickness = 0.09f;
        [Tooltip("격자 선(10)보다 위, 정찰 숫자·깨진 조각(20)보다 아래.")]
        [SerializeField] private int _sortingOrder = 12;

        [Header("나타나는 연출")]
        [SerializeField] [Min(0.01f)] private float _appearDuration = 0.35f;
        [Tooltip("나타날 때 잠깐 더 굵어졌다가 원래 두께로 돌아오는 정도(1 = 변화 없음).")]
        [SerializeField] [Min(1f)] private float _appearThicknessBoost = 2f;

        /// <summary>테두리 한 변. 나타나는 연출을 위해 원래 크기를 기억한다.</summary>
        private class Edge
        {
            public SpriteRenderer Renderer;
            public Vector3 BaseScale;
            public bool IsHorizontal;
            public float Age;
        }

        private readonly HashSet<Vector2Int> _brokenCells = new HashSet<Vector2Int>();
        // 이미 테두리를 그린 모양. 모양은 구조체라 첫 칸 좌표로 구분한다
        private readonly HashSet<Vector2Int> _outlinedGroups = new HashSet<Vector2Int>();
        private readonly List<Edge> _edges = new List<Edge>();
        private MinigameSession _shownSession;

        private void OnEnable()
        {
            if (_adapter != null) _adapter.TileBroken += OnTileBroken;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.TileBroken -= OnTileBroken;
        }

        private void OnTileBroken(TileBreakInfo info)
        {
            if (!info.IsFullBreak || info.Content != TileContent.Target || info.View == null) return;
            ResetIfNewSession();

            Vector2Int coord = info.View.Coordinate;
            _brokenCells.Add(coord);
            if (!_adapter.Session.Grid.TryGetPolyominoCell(coord, out PolyominoGroup group, out _)) return;
            if (_outlinedGroups.Contains(group.Coordinates[0])) return;

            foreach (Vector2Int cell in group.Coordinates)
            {
                if (!_brokenCells.Contains(cell)) return;   // 아직 안 깨진 칸이 있다
            }

            _outlinedGroups.Add(group.Coordinates[0]);
            DrawOutline(group);
        }

        private void Update()
        {
            ResetIfNewSession();

            float deltaTime = Time.deltaTime;
            foreach (Edge edge in _edges)
            {
                if (edge.Age >= _appearDuration) continue;
                edge.Age += deltaTime;
                float t = Mathf.Clamp01(edge.Age / _appearDuration);
                // 투명에서 또렷하게, 굵게 번쩍였다가 원래 두께로 돌아온다
                float boost = Mathf.Lerp(_appearThicknessBoost, 1f, t * t);
                edge.Renderer.color = new Color(_color.r, _color.g, _color.b, _color.a * t);
                edge.Renderer.transform.localScale = edge.IsHorizontal
                    ? new Vector3(edge.BaseScale.x, edge.BaseScale.y * boost, 1f)
                    : new Vector3(edge.BaseScale.x * boost, edge.BaseScale.y, 1f);
            }
        }

        /// <summary>
        /// 모양의 바깥 변만 골라 막대 스프라이트로 그린다.
        /// </summary>
        private void DrawOutline(PolyominoGroup group)
        {
            if (_sprite == null || _parent == null) return;

            var cells = new HashSet<Vector2Int>(group.Coordinates);
            float unit = _adapter.TileSize;
            float half = unit * 0.5f;
            float thickness = unit * _thickness;
            // 변 길이에 두께를 더해, 모서리에서 가로·세로 변이 빈틈 없이 겹치게 한다
            float length = unit + thickness;

            foreach (Vector2Int cell in group.Coordinates)
            {
                if (!_adapter.TileViews.TryGetValue(cell, out TileView view) || view == null) continue;
                Vector3 center = view.transform.localPosition;

                // 그리드 좌표는 y가 클수록 위쪽이다
                if (!cells.Contains(cell + Vector2Int.up)) AddEdge(center + new Vector3(0f, half), length, thickness, true);
                if (!cells.Contains(cell + Vector2Int.down)) AddEdge(center + new Vector3(0f, -half), length, thickness, true);
                if (!cells.Contains(cell + Vector2Int.left)) AddEdge(center + new Vector3(-half, 0f), thickness, length, false);
                if (!cells.Contains(cell + Vector2Int.right)) AddEdge(center + new Vector3(half, 0f), thickness, length, false);
            }
        }

        private void AddEdge(Vector3 position, float width, float height, bool isHorizontal)
        {
            var go = new GameObject("PolyominoOutlineEdge");
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            go.transform.localPosition = position;

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _sprite;
            spriteRenderer.sortingOrder = _sortingOrder;
            spriteRenderer.color = new Color(_color.r, _color.g, _color.b, 0f);

            Vector2 spriteSize = _sprite.bounds.size;
            var baseScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
            go.transform.localScale = baseScale;
            _edges.Add(new Edge { Renderer = spriteRenderer, BaseScale = baseScale, IsHorizontal = isHorizontal });
        }

        /// <summary>새 판이 시작되면 이전 판의 테두리와 기록을 지운다.</summary>
        private void ResetIfNewSession()
        {
            if (_adapter == null || _adapter.Session == _shownSession) return;
            _shownSession = _adapter.Session;
            _brokenCells.Clear();
            _outlinedGroups.Clear();
            foreach (Edge edge in _edges)
            {
                if (edge.Renderer != null) Destroy(edge.Renderer.gameObject);
            }
            _edges.Clear();
        }
    }
}
