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
        // 프리팹에 지정된 기본 사각형. 매개체 스프라이트가 없을 때와 열린 타일에 쓴다.
        private Sprite _defaultSprite;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _defaultSprite = _renderer.sprite;
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
            transform.localRotation = Quaternion.identity;
            Sprite sprite = isReinforced ? _sealMediumData.ReinforcedTileSprite : _sealMediumData.TileSprite;
            if (sprite != null)
            {
                // 색이 남아 있으면 그림에 곱해지므로 흰색으로 되돌린다
                _renderer.sprite = sprite;
                _renderer.color = Color.white;
                return;
            }

            // 스프라이트가 등록되지 않은 매개체는 기존처럼 색으로 표시
            _renderer.sprite = _defaultSprite;
            _renderer.color = isReinforced ? _sealMediumData.ReinforcedTileColor : _sealMediumData.TileColor;
        }

        public void ShowRevealed(TileContent content)
        {
            // 내용물은 아직 색으로만 구분하므로 덮개 그림을 걷어낸다
            transform.localRotation = Quaternion.identity;
            _renderer.sprite = _defaultSprite;
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

        /// <summary>
        /// 열린 목표 칸에 폴리오미노 조각 그림을 표시한다.
        /// </summary>
        /// <param name="shape">칸이 속한 폴리오미노 형상.</param>
        /// <param name="cellIndex"><see cref="PolyominoShapes.Definitions"/> 기준의 칸 인덱스.</param>
        /// <param name="rotationCount">형상이 기본 방향에서 반시계로 90°씩 돌아간 횟수.</param>
        /// <remarks>조각 그림이 없으면 목표 색으로 대신 표시한다.</remarks>
        public void ShowPolyominoCell(PolyominoShape shape, int cellIndex, int rotationCount)
        {
            if (!_sealMediumData.TryGetPolyominoCellSprite(shape, cellIndex, out Sprite sprite))
            {
                ShowRevealed(TileContent.Target);
                return;
            }

            _renderer.sprite = sprite;
            _renderer.color = Color.white;
            // 조각은 기본 방향으로 그려져 있으므로 형상과 같은 각도로 돌린다. Unity는 z축 양수가 반시계 방향이다.
            transform.localRotation = Quaternion.Euler(0f, 0f, 90f * rotationCount);
        }
    }
}
