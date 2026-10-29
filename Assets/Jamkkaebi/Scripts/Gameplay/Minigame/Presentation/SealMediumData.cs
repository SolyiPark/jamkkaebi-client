using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    [CreateAssetMenu(fileName = "SealMediumData", menuName = "Jamkkaebi/Seal Medium Data")]
    public class SealMediumData : ScriptableObject
    {
        [Header("봉인 컨셉")]
        [SerializeField] private SealMedium _sealMedium;
        
        // 색은 스프라이트가 비어 있을 때 쓰는 대체 표현
        [Header("타일")]
        [SerializeField] private Color _tileColor;
        [SerializeField] private Color _reinforcedTileColor;
        [SerializeField] private Sprite _tileSprite;
        [SerializeField] private Sprite _reinforcedTileSprite;
        
        [Header("배경")]
        [SerializeField] private Color _backgroundColor;

        [Header("격자")]
        [Tooltip("보드 격자 선 색. 알파가 0이면 선이 보이지 않는다.")]
        [SerializeField] private Color _gridLineColor = Color.gray;

        [Header("폴리오미노")]
        [Tooltip("목표 형상으로 쓰는 폴리오미노마다 하나씩 등록한다.")]
        [SerializeField] private PolyominoSpriteSet[] _polyominoSprites;
        
        private Dictionary<PolyominoShape, PolyominoSpriteSet> _polyominoSpritesByShape;
        
        public SealMedium SealMedium => _sealMedium;
        public Color TileColor => _tileColor;
        public Color ReinforcedTileColor => _reinforcedTileColor;
        public Sprite TileSprite => _tileSprite;
        public Sprite ReinforcedTileSprite => _reinforcedTileSprite;
        public Color BackgroundColor => _backgroundColor;
        public Color GridLineColor => _gridLineColor;

        /// <summary>
        /// 형상의 칸 인덱스에 해당하는 조각 스프라이트를 찾는다.
        /// </summary>
        /// <param name="shape">찾을 폴리오미노 형상.</param>
        /// <param name="cellIndex"><see cref="PolyominoShapes.Definitions"/> 기준의 칸 인덱스.</param>
        /// <param name="sprite">찾은 스프라이트. 없으면 null.</param>
        /// <returns>등록된 스프라이트가 있으면 true. 없으면 호출한 쪽이 색으로 대체해 표시한다.</returns>
        public bool TryGetPolyominoCellSprite(PolyominoShape shape, int cellIndex, out Sprite sprite)
        {
            sprite = null;
            if (!TryGetPolyominoSpriteSet(shape, out PolyominoSpriteSet set)) return false;
            if (cellIndex < 0 || cellIndex >= set.Cells.Count) return false;
            
            sprite = set.Cells[cellIndex];
            return sprite != null;
        }

        /// <summary>
        /// 형상 전체 미리보기 스프라이트를 찾는다.
        /// </summary>
        /// <returns>등록된 스프라이트가 있으면 true.</returns>
        public bool TryGetPolyominoPreview(PolyominoShape shape, out Sprite sprite)
        {
            sprite = TryGetPolyominoSpriteSet(shape, out PolyominoSpriteSet set) ? set.Preview : null;
            return sprite != null;
        }

        private bool TryGetPolyominoSpriteSet(PolyominoShape shape, out PolyominoSpriteSet set)
        {
            // 딕셔너리 초기화
            if (_polyominoSpritesByShape == null)
            {
                _polyominoSpritesByShape = BuildPolyominoLookup();
            }
            
            return _polyominoSpritesByShape.TryGetValue(shape, out set);
        }

        private Dictionary<PolyominoShape, PolyominoSpriteSet> BuildPolyominoLookup()
        {
            Dictionary<PolyominoShape, PolyominoSpriteSet> lookup = new Dictionary<PolyominoShape, PolyominoSpriteSet>();
            if (_polyominoSprites == null) return lookup;

            foreach (PolyominoSpriteSet set in _polyominoSprites)
            {
                // 중복은 OnValidate에서 경고하고, 여기서는 먼저 등록된 것을 쓴다
                if (set != null && !lookup.ContainsKey(set.Shape))
                {
                    lookup.Add(set.Shape, set);
                }
            }
            
            return lookup;
        }

        private void OnValidate()
        {
            // Inspector에서 값이 바뀌면 다음 조회 때 다시 만든다
            _polyominoSpritesByShape = null;
            
            if (_polyominoSprites == null) return;
            
            HashSet<PolyominoShape> seen = new HashSet<PolyominoShape>();
            for (int i = 0; i < _polyominoSprites.Length; i++)
            {
                PolyominoSpriteSet set = _polyominoSprites[i];
                if (set == null) continue;
                
                if (!seen.Add(set.Shape))
                {
                    Debug.LogWarning($"[SealMediumData] '{name}'의 폴리오미노 목록에 {set.Shape}가 중복 등록되어 있습니다. (Element {i})", this);
                }

                int expected = PolyominoShapes.Definitions[set.Shape].Count;
                if (set.Cells.Count != expected)
                {
                    Debug.LogWarning($"[SealMediumData] '{name}'의 {set.Shape} 조각은 {expected}개여야 하지만 {set.Cells.Count}개입니다. (Element {i})", this);
                }
            }
        }
    }
}
