using System;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    public class TileView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Color _coveredColor = Color.gray;
        [SerializeField] private Color _reinforcedColor = new Color(0.3f, 0.3f, 0.3f); // 진한 회색
        [SerializeField] private Color _emptyColor = Color.white;
        [SerializeField] private Color _targetColor = Color.yellow;
        [SerializeField] private Color _helperColor = Color.green;
        [SerializeField] private Color _threatColor = Color.red;

        public Vector2Int Coordinate { get; private set; }
        public bool UsesPointerEvents { get; set; }
        public event Action<Vector2Int> Clicked;
        
        private SpriteRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Vector2Int coordinate)
        {
            Coordinate = coordinate;
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
            _renderer.color = isReinforced ? _reinforcedColor : _coveredColor;
        }

        public void ShowRevealed(TileContent content)
        {
            _renderer.color = content switch
            {
                TileContent.Empty => _emptyColor,
                TileContent.Target => _targetColor,
                TileContent.Helper => _helperColor,
                TileContent.Threat => _threatColor,
                _ => throw new ArgumentOutOfRangeException(nameof(content), content, null)
            };
        }
    }
}
