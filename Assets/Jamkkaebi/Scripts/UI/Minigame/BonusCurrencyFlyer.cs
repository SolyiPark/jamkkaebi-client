using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 보너스(정성) 타일이 열리면 재화 아이콘 하나가 튀어 올랐다가 재화 표시 쪽으로 날아가게 한다.
    /// </summary>
    /// <remarks>
    /// 화면 전체를 덮는 HUD 루트에 붙인다. 아이콘은 이 오브젝트의 자식으로 만들고, 다 쓰면 꺼 두었다가 다시 쓴다.
    /// 날아갈 곳(<see cref="_target"/>)이 없으면 제자리에서 떠올랐다가 사라진다.
    /// 아이콘 그림이 없으면 코드로 만든 금색 원을 쓴다. 재화 아이콘이 나오면 Icon Sprite에 넣기만 하면 된다.
    /// </remarks>
    public class BonusCurrencyFlyer : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("보드를 비추는 카메라. 타일 위치를 화면 좌표로 바꿀 때 쓴다.")]
        [SerializeField] private Camera _boardCamera;

        [Header("아이콘")]
        [Tooltip("재화 아이콘. 비우면 금색 원을 쓴다.")]
        [SerializeField] private Sprite _iconSprite;
        [Tooltip("아이콘 그림에 곱할 색. 금색 원을 쓸 때는 이 색이 원 색이 된다.")]
        [SerializeField] private Color _iconColor = new Color(1f, 0.82f, 0.32f);
        [SerializeField] [Min(8f)] private float _iconSize = 72f;

        [Header("도착지 (선택)")]
        [Tooltip("아이콘이 날아갈 재화 표시. 비우면 제자리에서 떠올랐다가 사라진다.")]
        [SerializeField] private RectTransform _target;
        [Tooltip("도착 지점을 재화 표시의 기준점(pivot)에서 얼마나 옮길지(캔버스 단위). 표시 안 아이콘 위치에 맞춘다.")]
        [SerializeField] private Vector2 _targetOffset;
        [Tooltip("이번 판에서 모은 정성 수를 보여 줄 글자. 아이콘이 도착할 때 하나씩 오른다.")]
        [SerializeField] private TMP_Text _counterText;

        [Header("움직임 (캔버스 단위)")]
        [SerializeField] private FloatRange _burstSpeed = new FloatRange(350f, 650f);
        [SerializeField] private float _gravity = 2200f;
        [Tooltip("튀어 오른 뒤 날아가기 시작할 때까지의 시간(초).")]
        [SerializeField] [Min(0.05f)] private float _burstDuration = 0.3f;
        [SerializeField] [Min(0.05f)] private float _flyDuration = 0.45f;
        [Tooltip("도착지가 없을 때 떠오르는 거리와 시간.")]
        [SerializeField] private float _floatDistance = 140f;
        [SerializeField] [Min(0.05f)] private float _floatDuration = 0.6f;

        /// <summary>날아가는 아이콘 하나의 상태.</summary>
        private class Icon
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public Vector2 FlyStart;
            public float Age;
            public float FlyDelay;
        }

        private readonly List<Icon> _icons = new List<Icon>();
        private RectTransform _area;
        private Canvas _canvas;
        private Sprite _fallbackSprite;
        private Texture2D _fallbackTexture;
        private MinigameSession _countedSession;
        private int _count;
        private float _targetPunchTime;

        private void Awake()
        {
            _area = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            if (_iconSprite == null) CreateFallbackSprite();
        }

        private void OnEnable()
        {
            if (_adapter != null) _adapter.TileBroken += OnTileBroken;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.TileBroken -= OnTileBroken;
            foreach (Icon icon in _icons) icon.Rect.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_fallbackSprite != null) Destroy(_fallbackSprite);
            if (_fallbackTexture != null) Destroy(_fallbackTexture);
        }

        private void OnTileBroken(TileBreakInfo info)
        {
            // 플레이어가 직접 연 보너스만 연출한다(정답 공개로 열린 칸은 받은 정성이 아니다)
            if (!info.IsFullBreak || info.IsEndReveal || info.Content != TileContent.Bonus) return;
            if (_boardCamera == null || info.View == null) return;

            // 타일의 월드 좌표 → 화면 좌표 → 이 영역 안의 좌표
            Vector2 screenPoint = _boardCamera.WorldToScreenPoint(info.View.transform.position);
            if (!TryScreenToArea(screenPoint, out Vector2 start)) return;

            // 보너스 타일 하나 = 정성 1. 받은 개수와 화면에 보이는 하트 수가 같도록 하트도 하나만 띄운다
            // 거의 위쪽으로, 좌우로 조금만 기울여 튀어 오른다
            float angle = Random.Range(70f, 110f) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Icon icon = GetIcon();
            icon.Position = start;
            icon.Velocity = direction * _burstSpeed.RandomValue();
            icon.Age = 0f;
            icon.FlyDelay = _burstDuration;
            icon.Rect.anchoredPosition = start;
            icon.Rect.localScale = Vector3.one;
            icon.Image.color = _iconColor;
            icon.Rect.SetAsLastSibling();
            icon.Rect.gameObject.SetActive(true);
        }

        private void Update()
        {
            ResetCountOnNewSession();

            float deltaTime = Time.deltaTime;
            bool hasTarget = _target != null && _target.gameObject.activeInHierarchy;
            Vector2 targetPosition = Vector2.zero;
            if (hasTarget)
            {
                Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(GetUiCamera(), _target.position);
                hasTarget = TryScreenToArea(targetScreen, out targetPosition);
                targetPosition += _targetOffset;
            }

            foreach (Icon icon in _icons)
            {
                if (!icon.Rect.gameObject.activeSelf) continue;
                icon.Age += deltaTime;

                if (icon.Age < icon.FlyDelay)
                {
                    // 1단계: 튀어 오르며 중력으로 느려진다
                    icon.Velocity += Vector2.down * (_gravity * deltaTime);
                    icon.Position += icon.Velocity * deltaTime;
                    icon.Rect.anchoredPosition = icon.Position;
                    icon.FlyStart = icon.Position;
                    continue;
                }

                float t = (icon.Age - icon.FlyDelay) / (hasTarget ? _flyDuration : _floatDuration);
                if (t >= 1f)
                {
                    icon.Rect.gameObject.SetActive(false);
                    if (hasTarget) OnIconArrived(icon);
                    continue;
                }

                if (hasTarget)
                {
                    // 2단계: 처음엔 느리게, 끝으로 갈수록 빨라지며 빨려 들어간다(t³)
                    float eased = t * t * t;
                    icon.Rect.anchoredPosition = Vector2.LerpUnclamped(icon.FlyStart, targetPosition, eased);
                    icon.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, eased);
                }
                else
                {
                    // 도착지가 없으면 떠오르며 사라진다
                    icon.Rect.anchoredPosition = icon.FlyStart + Vector2.up * (_floatDistance * (1f - (1f - t) * (1f - t)));
                    Color color = _iconColor;
                    color.a *= 1f - t;
                    icon.Image.color = color;
                }
            }

            UpdateTargetPunch(deltaTime);
        }

        private void OnIconArrived(Icon icon)
        {
            _targetPunchTime = 0.15f;
            _count++;
            if (_counterText != null) _counterText.text = _count.ToString();
        }

        /// <summary>도착할 때마다 재화 표시가 톡 커졌다가 돌아온다.</summary>
        private void UpdateTargetPunch(float deltaTime)
        {
            if (_target == null || _targetPunchTime <= 0f) return;
            _targetPunchTime = Mathf.Max(0f, _targetPunchTime - deltaTime);
            float t = _targetPunchTime / 0.15f;   // 1에서 0으로
            _target.localScale = Vector3.one * (1f + 0.2f * t);
        }

        private void ResetCountOnNewSession()
        {
            if (_adapter == null || _adapter.Session == _countedSession) return;
            _countedSession = _adapter.Session;
            _count = 0;
            if (_counterText != null) _counterText.text = "0";
        }

        private bool TryScreenToArea(Vector2 screenPoint, out Vector2 anchoredPosition)
        {
            anchoredPosition = Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screenPoint, GetUiCamera(), out Vector2 local)) return false;
            // 아이콘은 영역 가운데를 기준점(anchor 0.5, 0.5)으로 두므로 가운데 기준 좌표로 바꾼다
            anchoredPosition = local - _area.rect.center;
            return true;
        }

        // Overlay 캔버스는 화면 좌표를 카메라 없이 변환한다
        private Camera GetUiCamera() =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private Icon GetIcon()
        {
            foreach (Icon icon in _icons)
            {
                if (!icon.Rect.gameObject.activeSelf) return icon;
            }

            var go = new GameObject("BonusIcon", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(_area, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_iconSize, _iconSize);

            var image = go.AddComponent<Image>();
            image.sprite = _iconSprite != null ? _iconSprite : _fallbackSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var created = new Icon { Rect = rect, Image = image };
            _icons.Add(created);
            return created;
        }

        /// <summary>재화 아이콘이 없을 때 쓸 흰색 원. 색은 Image 색으로 입힌다.</summary>
        private void CreateFallbackSprite()
        {
            const int size = 64;
            _fallbackTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BonusIconFallback" };
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // 원 테두리 1텍셀을 부드럽게 깎아 계단 현상을 줄인다
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    // 안쪽 테두리를 살짝 어둡게 해 동전처럼 보이게 한다
                    byte shade = distance > radius - 6f ? (byte)200 : (byte)255;
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
                }
            }
            _fallbackTexture.SetPixels32(pixels);
            _fallbackTexture.Apply();
            _fallbackSprite = Sprite.Create(_fallbackTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
