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
            
            MinigamePhaseConfig config = _relicData.GetPhaseConfig(_phaseIndex);
            _session = new MinigameSession(config);
            _tileViews = new Dictionary<Vector2Int, TileView>();

            foreach (Vector2Int coord in _session.Grid.AllCoordinates())
            {
                Tile tile = _session.Grid.GetTile(coord.x, coord.y);
                
                GameObject instance = Instantiate(_tilePrefab, _gridContainer);
                instance.transform.localPosition = GridToWorldPosition(coord);
                
                TileView view = instance.GetComponent<TileView>();
                view.ShowCovered(tile.IsReinforced);
                
                _tileViews[coord] = view;
            }
            
            _session.TileRevealAttempted += OnTileRevealAttempted;
            _tickCoroutine = StartCoroutine(TickRoutine());
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
            return new Vector2(coord.x * _tileSize, coord.y * _tileSize);
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
            ToolSelectionChanged?.Invoke(mode);
        }

        private void HandleTileClicked(Vector2Int coord)
        {
            switch (_selectedTool)
            {
                case ToolMode.SafeDestroy:
                    _session.UseDestructiveTool(coord, DestructiveToolType.Safe);
                    HandleSessionEndIfNeeded();
                    break;
                case ToolMode.AttackDestroy:
                    _session.UseDestructiveTool(coord, DestructiveToolType.Attack);
                    HandleSessionEndIfNeeded();
                    break;
                case ToolMode.Scout:
                    _session.UseScoutTool(coord, out int threatCount);
                    // TODO: threatCount 결과를 텍스트로 표시
                    break;
            }
        }

        private void OnTileRevealAttempted(TileRevealResult result)
        {
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
            // TODO: 세션 종료 결과 화면 처리
        }
    }
}