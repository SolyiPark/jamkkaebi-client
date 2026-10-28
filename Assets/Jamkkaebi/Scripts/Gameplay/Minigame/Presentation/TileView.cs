using System;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    public class TileView : MonoBehaviour, IPointerClickHandler
    {
        //[SerializeField] private Color _coveredColor = Color.gray;
        //[SerializeField] private Color _reinforcedColor = new Color(0.3f, 0.3f, 0.3f); // 진한 회색
        [SerializeField] private Color _emptyColor = Color.white;
        [SerializeField] private Color _targetColor = Color.yellow;
        [SerializeField] private Color _helperColor = Color.green;
        [SerializeField] private Color _threatColor = Color.red;
        [SerializeField] private Color _bonusColor = new Color(0.3f, 0.7f, 1f);    // 임시 색. 정성 보너스 아트가 나오면 교체

        public Vector2Int Coordinate { get; private set; }
        public bool UsesPointerEvents { get; set; }
        public event Action<Vector2Int> Clicked;
        
        private SpriteRenderer _renderer;
        private SealMediumData _sealMediumData;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Vector2Int coordinate, SealMediumData sealMediumData)
        {
            if (sealMediumData == null)
            {
                throw new ArgumentNullException(nameof(sealMediumData));
            }
            
            Coordinate = coordinate;
            _sealMediumData = sealMediumData;
        }

        private void OnMouseDown()
        {
            if (!UsesPointerEvents) Clicked?.Invoke(Coordinate);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (UsesPointerEvents && eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke(Coordinate);
        }

        public void ShowCovered(bool isReinforced)
        {
            _renderer.color = isReinforced ? _sealMediumData.ReinforcedTileColor : _sealMediumData.TileColor;
        }

        public void ShowRevealed(TileContent content)
        {
            _renderer.color = content switch
            {
                TileContent.Empty => _emptyColor,
                TileContent.Target => _targetColor,
                TileContent.Helper => _helperColor,
                TileContent.Threat => _threatColor,
                TileContent.Bonus => _bonusColor,
                _ => throw new ArgumentOutOfRangeException(nameof(content), content, null)
            };
        }
    }
}
