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
        [SerializeField] private Color _bonusColor = new Color(0.3f, 0.7f, 1f);

        [Header("내용물 아이콘")]
        [Tooltip("열린 칸 위에 올리는 그림. 비우면 위의 색으로만 구분한다.")]
        [SerializeField] private Sprite _helperIcon;
        [SerializeField] private Sprite _threatIcon;
        [SerializeField] private Sprite _bonusIcon;
        [Tooltip("타일 한 칸 대비 아이콘 크기. 아이콘의 긴 변을 이 비율에 맞춘다.")]
        // using System이 있어 Range만 쓰면 System.Range(C# 범위 타입)와 헷갈리므로 UnityEngine을 붙인다
        [SerializeField] [UnityEngine.Range(0.2f, 1f)] private float _iconSizeRatio = 0.72f;
        [Tooltip("아이콘을 타일 위에 그리는 순서. 타일(0)보다 위, 정찰 범위(5)·격자 선(10)보다 아래.")]
        [SerializeField] private int _iconSortingOrder = 1;

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
        // 내용물 아이콘을 그리는 자식 렌더러. 처음 필요할 때 만든다
        private SpriteRenderer _iconRenderer;

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
            HideIcon();
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
            transform.localRotation = Quaternion.identity;
            _renderer.sprite = _defaultSprite;

            // 아이콘이 있으면 빈 칸 바탕 위에 아이콘을 올리고, 없으면 내용물 색으로 칸을 칠한다
            Sprite icon = content switch
            {
                TileContent.Helper => _helperIcon,
                TileContent.Threat => _threatIcon,
                TileContent.Bonus => _bonusIcon,
                _ => null
            };
            if (icon != null)
            {
                _renderer.color = _emptyColor;
                ShowIcon(icon);
                return;
            }

            HideIcon();
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

            HideIcon();
            _renderer.sprite = sprite;
            _renderer.color = Color.white;
            // 조각은 기본 방향으로 그려져 있으므로 형상과 같은 각도로 돌린다. Unity는 z축 양수가 반시계 방향이다.
            transform.localRotation = Quaternion.Euler(0f, 0f, 90f * rotationCount);
        }

        /// <summary>
        /// 칸 가운데에 아이콘을 띄운다. 아이콘 그림 크기와 상관없이 칸 크기의 일정 비율로 맞춘다.
        /// </summary>
        private void ShowIcon(Sprite icon)
        {
            if (_iconRenderer == null)
            {
                var go = new GameObject("ContentIcon");
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                _iconRenderer = go.AddComponent<SpriteRenderer>();
                _iconRenderer.sharedMaterial = _renderer.sharedMaterial;   // 타일과 같은 머티리얼로 그려 묶어 그리기 쉽게 한다
                _iconRenderer.sortingOrder = _iconSortingOrder;
            }

            _iconRenderer.sprite = icon;
            // 이 오브젝트의 크기는 기본 사각형(_defaultSprite) 한 장이 칸 하나가 되도록 맞춰져 있다.
            // 그래서 아이콘의 긴 변이 기본 사각형 크기 × 비율이 되도록 자식 크기를 정한다
            Vector2 cellSize = _defaultSprite != null ? (Vector2)_defaultSprite.bounds.size : Vector2.one;
            Vector2 iconSize = icon.bounds.size;
            float scale = Mathf.Min(cellSize.x, cellSize.y) * _iconSizeRatio / Mathf.Max(iconSize.x, iconSize.y);
            _iconRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            _iconRenderer.gameObject.SetActive(true);
        }

        private void HideIcon()
        {
            if (_iconRenderer != null) _iconRenderer.gameObject.SetActive(false);
        }
    }
}
