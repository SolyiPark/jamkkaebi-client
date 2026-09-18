using System;
using System.Collections;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    public class MinigameSessionAdapter : MonoBehaviour
    {
        [SerializeField] private RelicData _relicData;
        [SerializeField] [Range(0, 2)] private int _phaseIndex;
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] [Range(0.5f, 1f)] private float _tileFillRatio = 0.9f;
        
        private MinigameSession _session;
        private Dictionary<Vector2Int, TileView> _tileViews;
        private Coroutine _tickCoroutine;
        private ToolMode _selectedTool = ToolMode.SafeDestroy;
        private bool _hasHandledEnd;

        public event Action<ToolMode> ToolSelectionChanged;

        private void Start()
        {
            if (_relicData == null)
            {
                Debug.LogError($"{nameof(MinigameSessionAdapter)}: _relicData가 연결되지 않았습니다.", this);
                return;
            }
            
            BeginSession();
        }

        public void BeginSession()
        {
            // 1. 이전 세션 정리
            if (_session != null)
            {
                _session.TileRevealAttempted -= OnTileRevealAttempted;
            }

            if (_tickCoroutine != null)
            {
                StopCoroutine(_tickCoroutine);
                _tickCoroutine = null;
            }

            if (_tileViews != null)
            {
                foreach (TileView view in _tileViews.Values)
                {
                    Destroy(view.gameObject);
                }
            }
            
            _hasHandledEnd = false;
            
            // 2. 새 세션 생성
            MinigamePhaseConfig config = _relicData.GetPhaseConfig(_phaseIndex);
            _session = new MinigameSession(config);
            _tileViews = new Dictionary<Vector2Int, TileView>();

            foreach (Vector2Int coord in _session.Grid.AllCoordinates())
            {
                Tile tile = _session.Grid.GetTile(coord.x, coord.y);
                
                GameObject instance = Instantiate(_tilePrefab, _gridContainer);
                instance.transform.localPosition = GridToWorldPosition(coord);
                instance.transform.localScale = new Vector3(_tileSize * _tileFillRatio, _tileSize * _tileFillRatio, 1f);
                
                TileView view = instance.GetComponent<TileView>();
                view.Initialize(coord);
                view.ShowCovered(tile.IsReinforced);
                view.Clicked += HandleTileClicked;
                
                _tileViews[coord] = view;
            }
            
            _session.TileRevealAttempted += OnTileRevealAttempted;
            _tickCoroutine = StartCoroutine(TickRoutine());
        }

        private void Update()
        {
            // TODO: 정식 도구 선택 UI 완성되면 해당 Update는 필요 없음
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(ToolMode.SafeDestroy);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(ToolMode.AttackDestroy);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(ToolMode.Scout);
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.TileRevealAttempted -= OnTileRevealAttempted;
            }
        }

        private Vector2 GridToWorldPosition(Vector2Int coord)
        {
            float offsetX = (_session.Grid.Width - 1) * _tileSize * 0.5f;
            float offsetY = (_session.Grid.Height - 1) * _tileSize * 0.5f;
            return new Vector2(coord.x * _tileSize - offsetX, coord.y * _tileSize - offsetY);
        }

        private IEnumerator TickRoutine()
        {
            while (_session.State == SessionState.InProgress)
            {
                yield return new WaitForSeconds(1f);
                _session.AdvanceTime(1f);
                // TODO: 타이머 텍스트 갱신
            }
            
            HandleSessionEndIfNeeded();
        }

        public void SelectTool(ToolMode mode)
        {
            _selectedTool = mode;
            Debug.Log($"도구 변경: {mode}");
            ToolSelectionChanged?.Invoke(mode);
        }

        private void HandleTileClicked(Vector2Int coord)
        {
            switch (_selectedTool)
            {
                case ToolMode.SafeDestroy:
                {
                    DestroyResult result = _session.UseDestructiveTool(coord, DestructiveToolType.Safe);
                    Debug.Log($"({coord.x}, {coord.y}) 안전 파괴 결과: {result}");
                    HandleSessionEndIfNeeded();
                    break;
                }
                case ToolMode.AttackDestroy:
                {
                    DestroyResult result = _session.UseDestructiveTool(coord, DestructiveToolType.Attack);
                    Debug.Log($"({coord.x}, {coord.y}) 공격 파괴 결과: {result}");
                    HandleSessionEndIfNeeded();
                    break;
                }
                case ToolMode.Scout:
                {
                    ScoutResult result = _session.UseScoutTool(coord, out int threatCount);
                    Debug.Log($"({coord.x}, {coord.y}) 정찰 결과: {result}, 위협 타일 {threatCount}개");
                    // TODO: threatCount 결과를 타일 위에 텍스트로 표시
                    break;
                }
            }
        }

        private void OnTileRevealAttempted(TileRevealResult result)
        {
            Debug.Log($"({result.Coordinate.x}, {result.Coordinate.y}) {result.Content} / {result.Outcome}");
            
            TileView view = _tileViews[result.Coordinate];

            switch (result.Outcome)
            {
                case RevealOutcome.AlreadyRevealed:
                    break;
                case RevealOutcome.ReinforcementConsumed:
                    view.ShowCovered(false);
                    break;
                case RevealOutcome.Revealed:
                    view.ShowRevealed(result.Content);
                    break;
            }
        }

        private void HandleSessionEndIfNeeded()
        {
            if (_hasHandledEnd || _session.State == SessionState.InProgress) return;
            _hasHandledEnd = true;
            Debug.Log($"세션 종료: {_session.State} (남은 시간: {_session.RemainingSeconds:F1}s, 손상도: {_session.DamageGauge:P0})");
            // TODO: 세션 종료 결과 화면 처리
        }
    }
}