using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    public class SealMediumBackdropBinder : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("매개체마다 하나씩. 판이 시작되면 현재 매개체의 배경만 켜고 나머지는 끈다.")]
        [SerializeField] private SealMediumBackdrop[] _backdrops;

        private readonly HashSet<Vector2Int> _brokenTargetCells = new HashSet<Vector2Int>();
        private int _totalTargetCells;
        private SealMediumBackdrop _current;

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
            
            _current = ActivateBackdrop(_adapter.CurrentSkin.SealMedium);
            if (_current != null) _current.SetCorruptionImmediate(1f);
        }

        private void OnTileBroken(TileBreakInfo info)
        {
            if (!info.IsFullBreak) return;
            if (info.Content != TileContent.Target) return;
            if (info.IsEndReveal) return;
            if (info.View == null) return;
            if (_totalTargetCells == 0) return;
            if (_current == null) return;
            
            _brokenTargetCells.Add(info.View.Coordinate);
            
            float progress = _brokenTargetCells.Count / (float)_totalTargetCells;
            _current.SetCorruption(1f - progress);
        }

        /// <summary>
        /// 매개체가 같은 배경 하나만 켜고 나머지는 끈다. 꺼진 배경은 Update가 돌지 않아 비용이 들지 않는다.
        /// </summary>
        /// <returns>켜진 배경. 등록된 것이 없으면 null.</returns>
        private SealMediumBackdrop ActivateBackdrop(SealMedium sealMedium)
        {
            SealMediumBackdrop found = null;
            foreach (SealMediumBackdrop backdrop in _backdrops)
            {
                if (backdrop == null) continue;

                bool isMatch = found == null && backdrop.SealMedium == sealMedium;
                backdrop.gameObject.SetActive(isMatch);
                if (isMatch) found = backdrop;
            }

            if (found == null)
            {
                Debug.LogWarning($"[{nameof(SealMediumBackdropBinder)}] 봉인 매개체 {sealMedium}의 배경 연출이 등록되어 있지 않습니다.", this);
            }
            return found;
        }
    }
}