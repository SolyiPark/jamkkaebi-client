using System.Collections;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 화면 가장자리를 잠깐 빛나게 한다. 손상도가 오르면 붉게, 도움 타일 연쇄가 시작되면 노랗게 빛난다.
    /// </summary>
    /// <remarks>
    /// 캔버스 루트에 붙이면 화면 전체를 덮는 이미지를 스스로 만든다. SafeArea 밖(노치 영역)까지 덮기 위해서다.
    /// 가장자리 그림은 코드로 만든 텍스처를 9-slice(Sliced)로 늘려 화면 비율이 달라도 띠 두께가 같게 유지한다.
    /// 첫 번째 형제로 두어 보드(월드)보다는 위, HUD보다는 아래에 그려진다.
    /// </remarks>
    public class ScreenEdgeFlash : MonoBehaviour
    {
        // 텍스처 가장자리 띠의 폭(텍셀). 가운데 2텍셀은 완전히 투명하다
        private const int BorderTexels = 64;

        [SerializeField] private MinigameSessionAdapter _adapter;

        [Header("색")]
        [Tooltip("손상도가 올랐을 때. 무섭지 않게 순수한 빨강보다 따뜻한 산호색을 쓴다.")]
        [SerializeField] private Color _damageColor = new Color(1f, 0.38f, 0.3f, 0.55f);
        [SerializeField] private Color _helperColor = new Color(1f, 0.84f, 0.3f, 0.45f);

        [Header("모양·시간")]
        [Tooltip("가장자리에서 안쪽으로 빛이 번지는 두께(캔버스 단위).")]
        [SerializeField] [Min(10f)] private float _edgeThickness = 260f;
        [SerializeField] [Min(0.01f)] private float _riseDuration = 0.08f;
        [SerializeField] [Min(0.01f)] private float _fadeDuration = 0.45f;

        private Image _image;
        private Texture2D _texture;
        private Sprite _sprite;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            _texture = CreateEdgeTexture();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(BorderTexels, BorderTexels, BorderTexels, BorderTexels));

            var go = new GameObject("ScreenEdgeFlash", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            _image = go.AddComponent<Image>();
            _image.sprite = _sprite;
            _image.type = Image.Type.Sliced;
            // 가운데는 완전히 투명하므로 아예 그리지 않는다. 화면 대부분의 픽셀 계산(오버드로)을 줄인다
            _image.fillCenter = false;
            _image.raycastTarget = false;   // 터치를 막지 않는다
            // 텍스처 띠 폭(64텍셀)이 화면에서 _edgeThickness가 되도록 배율을 정한다
            _image.pixelsPerUnitMultiplier = BorderTexels / _edgeThickness;
            _image.color = Color.clear;
        }

        private void OnEnable()
        {
            if (_adapter == null) return;
            _adapter.DamageTaken += OnDamageTaken;
            _adapter.HelperChainStarted += OnHelperChainStarted;
        }

        private void OnDisable()
        {
            if (_adapter != null)
            {
                _adapter.DamageTaken -= OnDamageTaken;
                _adapter.HelperChainStarted -= OnHelperChainStarted;
            }
            _flashRoutine = null;
            if (_image != null) _image.color = Color.clear;
        }

        private void OnDestroy()
        {
            // 코드로 만든 텍스처와 스프라이트는 직접 지워야 메모리에서 빠진다
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }

        private void OnDamageTaken(float damage) => Flash(_damageColor);

        private void OnHelperChainStarted(Vector2Int origin, bool isHorizontal) => Flash(_helperColor);

        private void Flash(Color color)
        {
            if (_image == null) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        private IEnumerator FlashRoutine(Color color)
        {
            // 이미 빛나는 중이면 지금 밝기에서 이어서 올린다
            float startAlpha = _image.color.a;
            var transparent = new Color(color.r, color.g, color.b, 0f);

            for (float elapsed = 0f; elapsed < _riseDuration; elapsed += Time.unscaledDeltaTime)
            {
                float alpha = Mathf.Lerp(startAlpha, color.a, elapsed / _riseDuration);
                _image.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < _fadeDuration; elapsed += Time.unscaledDeltaTime)
            {
                _image.color = Color.Lerp(color, transparent, elapsed / _fadeDuration);
                yield return null;
            }

            _image.color = Color.clear;
            _flashRoutine = null;
        }

        /// <summary>
        /// 가장자리는 불투명한 흰색이고 안쪽으로 갈수록 투명해지는 텍스처를 만든다. 색은 Image 색을 곱해 정한다.
        /// </summary>
        private static Texture2D CreateEdgeTexture()
        {
            int size = BorderTexels * 2 + 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "ScreenEdgeFlash"
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 가장 가까운 가장자리까지의 거리(0 = 가장자리, 1 = 띠 안쪽 끝)
                    int edgeDistance = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float t = Mathf.Clamp01(edgeDistance / (float)BorderTexels);
                    // (1 - t)²: 가장자리에서 진하고 안쪽으로 부드럽게 사라진다
                    float alpha = (1f - t) * (1f - t);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
