using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    public class SealMediumBackdropBinder : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [SerializeField] private SealMediumBackdrop _backdrop;

        private readonly HashSet<Vector2Int> _brokenTargetCells = new HashSet<Vector2Int>();
        private int _totalTargetCells;

        private void OnEnable()
        {
            if (_adapter != null)
            {
                _adapter.SessionBegan += OnSessionBegan;
                _adapter.TileBroken += OnTileBroken;
            }
        }

        private void OnDisable()
        {
            if (_adapter != null)
            {
                _adapter.SessionBegan -= OnSessionBegan;
                _adapter.TileBroken -= OnTileBroken;
            }
        }

        private void OnSessionBegan()
        {
            _brokenTargetCells.Clear();
            _totalTargetCells = 0;

            foreach (PolyominoGroup group in _adapter.Session.Grid.PolyominoGroups)
            {
                _totalTargetCells += group.Coordinates.Count;
            }
            
            _backdrop.SetCorruptionImmediate(1f);
        }

        private void OnTileBroken(TileBreakInfo info)
        {
            if (!info.IsFullBreak) return;
            if (info.Content != TileContent.Target) return;
            if (info.IsEndReveal) return;
            if (info.View == null) return;
            if (_totalTargetCells == 0) return;
            
            _brokenTargetCells.Add(info.View.Coordinate);
            
            float progress = _brokenTargetCells.Count / (float)_totalTargetCells;
            _backdrop.SetCorruption(1f - progress);
        }
    }
}