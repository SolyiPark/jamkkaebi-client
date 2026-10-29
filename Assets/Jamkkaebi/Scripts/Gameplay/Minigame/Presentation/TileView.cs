using System;
using System.Collections;
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
        /// <summary>지금 표시 중인 그림. 깨짐 연출이 덮개 그림을 조각낼 때 쓴다.</summary>
        public Sprite CurrentSprite => _renderer.sprite;
        /// <summary>지금 그림에 곱해지는 색.</summary>
        public Color CurrentColor => _renderer.color;
        /// <summary>보드에 배치될 때의 크기. 튕김 연출은 이 크기로 돌아온다.</summary>
        public Vector3 BaseScale => _baseScale;
        
        private SpriteRenderer _renderer;
        private SealMediumData _sealMediumData;
        // 프리팹에 지정된 기본 사각형. 매개체 스프라이트가 없을 때와 열린 타일에 쓴다.
        private Sprite _defaultSprite;
        private Vector3 _baseScale = Vector3.one;
        private Coroutine _punchRoutine;

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
            // 어댑터가 Initialize 전에 칸 크기를 정해 두므로 지금 크기를 기준 크기로 기억한다
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            // 튕기는 도중 꺼지면 코루틴이 멈춰 크기가 어중간하게 남으므로 기준 크기로 되돌린다
            _punchRoutine = null;
            transform.localScale = _baseScale;
        }

        /// <summary>
        /// 타일을 잠깐 작게 했다가 원래 크기로 튕기듯 되돌린다.
        /// </summary>
        /// <param name="startRatio">시작 크기 비율(1보다 작을수록 크게 튕긴다).</param>
        /// <param name="duration">원래 크기로 돌아오는 데 걸리는 시간(초).</param>
        public void PlayPunch(float startRatio, float duration)
        {
            if (!isActiveAndEnabled) return;
            if (_punchRoutine != null) StopCoroutine(_punchRoutine);
            _punchRoutine = StartCoroutine(PunchRoutine(startRatio, duration));
        }

        private IEnumerator PunchRoutine(float startRatio, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = elapsed / duration;
                // 1 - (1 - t)^2: 처음에 빨리 커지고 끝에서 천천히 멈춘다
                float eased = 1f - (1f - t) * (1f - t);
                transform.localScale = _baseScale * Mathf.Lerp(startRatio, 1f, eased);
                yield return null;
            }
            transform.localScale = _baseScale;
            _punchRoutine = null;
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
