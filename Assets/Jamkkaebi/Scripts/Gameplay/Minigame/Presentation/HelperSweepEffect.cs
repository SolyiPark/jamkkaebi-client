using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 도움 타일이 한 줄을 열면, 도움 타일에서 줄 양 끝으로 레이저처럼 뻗는 빛을 쏜다.
    /// </summary>
    /// <remarks>
    /// 레이저 한 줄기는 두 겹이다. 넓고 부드러운 금빛 테두리(glow) 위에 가늘고 밝은 흰 심(core)을 겹쳐,
    /// 밝은 매개체 타일 위에서도 또렷하게 보이게 한다. 끝에는 흰 빛 덩어리(tip)가 달려 줄을 따라 달린다.
    /// 레이저 모양은 시작이 둥글고 부드럽게 나타나고, 끝으로 갈수록 가늘어지며 사라진다.
    /// 이 시작·끝 부분은 9-slice(Sliced)로 그려, 레이저 길이가 달라져도 늘어나지 않고 같은 크기를 유지한다.
    /// 레이저가 지나간 자리에는 작은 금빛 입자(sparkle)가 남아 반짝이며 떠다니다 사라진다.
    /// 뻗는 속도는 어댑터의 연쇄 간격(<see cref="MinigameSessionAdapter.ChainStepDelay"/>)과 맞춰,
    /// 빛 끝이 닿는 칸이 그 순간 깨지는 것처럼 보이게 한다.
    /// 그림은 코드로 만든 텍스처를 쓰므로 따로 그릴 필요가 없다.
    /// </remarks>
    public class HelperSweepEffect : MonoBehaviour
    {
        // 레이저 텍스처(텍셀): [둥근 시작 | 늘어나는 가운데 | 가늘어지는 끝] 순서로 이어 붙인다
        private const int BeamCapTexels = 16;
        private const int BeamMiddleTexels = 4;
        private const int BeamTailTexels = 64;
        private const int BeamTextureWidth = 32;
        private const int FlareTextureSize = 64;

        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("빛을 놓을 부모. 타일과 같은 보드 컨테이너를 쓴다.")]
        [SerializeField] private Transform _parent;

        [Header("색")]
        [SerializeField] private Color _glowColor = new Color(1f, 0.72f, 0.12f, 0.95f);
        [SerializeField] private Color _coreColor = Color.white;
        [SerializeField] private Color _flareColor = new Color(1f, 0.97f, 0.85f, 1f);

        [Header("크기 (타일 한 칸 = 1)")]
        [SerializeField] [Min(0.05f)] private float _glowWidth = 0.9f;
        [SerializeField] [Min(0.01f)] private float _coreWidth = 0.22f;
        [Tooltip("끝으로 갈수록 가늘어지는 구간의 길이. 1이면 줄의 마지막 칸 안에서 가늘어져 보드 가장자리에서 끝난다.")]
        [SerializeField] [Min(0.1f)] private float _tailLength = 1f;
        [Tooltip("레이저 끝을 따라 달리는 빛 덩어리 크기.")]
        [SerializeField] [Min(0.1f)] private float _tipFlareSize = 1f;
        [Tooltip("도움 타일 자리에서 번쩍이는 빛 크기.")]
        [SerializeField] [Min(0.1f)] private float _originFlareSize = 2f;

        [Header("시간 (초)")]
        [Tooltip("연쇄 간격이 0일 때(한 번에 깨질 때) 레이저가 끝까지 뻗는 시간.")]
        [SerializeField] [Min(0.02f)] private float _minExtendDuration = 0.12f;
        [Tooltip("끝까지 뻗은 뒤 그대로 머무는 시간.")]
        [SerializeField] [Min(0f)] private float _holdDuration = 0.1f;
        [Tooltip("가늘어지며 사라지는 시간.")]
        [SerializeField] [Min(0.02f)] private float _fadeDuration = 0.25f;
        [SerializeField] [Min(0.02f)] private float _originFlareDuration = 0.4f;

        [Header("금빛 입자")]
        [Tooltip("금빛 테두리와 구분되도록 테두리보다 밝고 연한 금색을 쓴다.")]
        [SerializeField] private Color _sparkleColor = new Color(1f, 0.93f, 0.6f, 1f);
        [Tooltip("레이저가 한 칸 지날 때마다 남기는 입자 수.")]
        [SerializeField] [Min(0f)] private float _sparklesPerTile = 4f;
        [Tooltip("도움 타일 자리에서 퍼지는 입자 수.")]
        [SerializeField] [Min(0)] private int _originSparkleCount = 8;
        [Tooltip("입자 크기 범위(타일 한 칸 = 1).")]
        [SerializeField] private FloatRange _sparkleSize = new FloatRange(0.28f, 0.5f);
        [Tooltip("입자가 보이는 시간 범위(초).")]
        [SerializeField] private FloatRange _sparkleLifetime = new FloatRange(0.6f, 1.2f);
        [Tooltip("레이저 중심선에서 옆으로 떨어진 거리 범위(타일 한 칸 = 1). 금빛 테두리 안에 묻히지 않도록 테두리 가장자리 바깥쪽에 뿌린다.")]
        [SerializeField] private FloatRange _sparkleSpread = new FloatRange(0.3f, 0.7f);
        [Tooltip("입자가 떠다니는 최대 속도(칸/초).")]
        [SerializeField] [Min(0f)] private float _sparkleDrift = 0.35f;
        [Tooltip("1초에 몇 번 깜빡이듯 밝아졌다 어두워지는지.")]
        [SerializeField] [Min(0f)] private float _sparkleTwinkleSpeed = 3f;

        [Header("그리는 순서")]
        [Tooltip("타일(0)·정찰 범위(5)·격자 선(10)보다 위. 깨진 조각(20)보다 아래.")]
        [SerializeField] private int _sortingOrder = 16;

        /// <summary>레이저 한 줄기. 루트를 도움 타일에 두고 진행 방향으로 돌린 뒤 자식들의 길이를 늘인다.</summary>
        private class Beam
        {
            public Transform Root;
            public SpriteRenderer Glow;
            public SpriteRenderer Core;
            public SpriteRenderer Tip;
            public float Length;           // 다 뻗었을 때의 길이(로컬 단위)
            public float ExtendDuration;
            public float Age;
            public float SparkleLength;    // 입자를 이미 뿌린 길이. 레이저 끝이 이보다 앞서면 그 사이에 입자를 뿌린다
        }

        /// <summary>떠다니며 반짝이다 사라지는 금빛 입자 하나.</summary>
        private class Sparkle
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float Size;
            public float Lifetime;
            public float Age;
            public float TwinklePhase;     // 입자마다 깜빡이는 박자를 다르게 한다
        }

        /// <summary>제자리에서 커지며 사라지는 빛 덩어리.</summary>
        private class Flare
        {
            public SpriteRenderer Renderer;
            public float Age;
        }

        private readonly List<Beam> _beams = new List<Beam>();
        private readonly List<Flare> _flares = new List<Flare>();
        private readonly List<Sparkle> _sparkles = new List<Sparkle>();
        private Texture2D _beamTexture;
        private Texture2D _flareTexture;
        private Sprite _beamSprite;
        private Sprite _flareSprite;
        private Texture2D _sparkleTexture;
        private Sprite _sparkleSprite;
        // 보드 영역(보드 컨테이너 로컬 좌표). 입자가 떠다니다 보드 밖으로 나가지 않게 막는다
        private Rect _boardBounds;

        private void Awake()
        {
            CreateSprites();
        }

        private void OnEnable()
        {
            if (_adapter != null) _adapter.HelperChainStarted += Fire;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.HelperChainStarted -= Fire;
            foreach (Beam beam in _beams) beam.Root.gameObject.SetActive(false);
            foreach (Flare flare in _flares) flare.Renderer.gameObject.SetActive(false);
            foreach (Sparkle sparkle in _sparkles) sparkle.Renderer.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // 코드로 만든 텍스처와 스프라이트는 직접 지워야 메모리에서 빠진다
            if (_beamSprite != null) Destroy(_beamSprite);
            if (_flareSprite != null) Destroy(_flareSprite);
            if (_beamTexture != null) Destroy(_beamTexture);
            if (_flareTexture != null) Destroy(_flareTexture);
            if (_sparkleSprite != null) Destroy(_sparkleSprite);
            if (_sparkleTexture != null) Destroy(_sparkleTexture);
        }

        private void Fire(Vector2Int origin, bool isHorizontal)
        {
            if (_parent == null || _adapter.Session == null) return;
            if (!_adapter.TileViews.TryGetValue(origin, out TileView originView) || originView == null) return;

            float unit = _adapter.TileSize;
            Vector3 start = originView.transform.localPosition;
            int width = _adapter.Session.Grid.Width;
            int height = _adapter.Session.Grid.Height;
            // 타일 좌표는 보드 가운데가 원점이다(MinigameSessionAdapter.GridToWorldPosition)
            _boardBounds = new Rect(-width * unit * 0.5f, -height * unit * 0.5f, width * unit, height * unit);

            LaunchFlare(start, unit);
            for (int i = 0; i < _originSparkleCount; i++)
            {
                // 도움 타일 칸 안 아무 곳에서 사방으로 퍼진다
                Vector3 offset = Random.insideUnitCircle * (0.5f * unit);
                SpawnSparkle(start + offset, Random.insideUnitCircle.normalized, unit);
            }

            // 양쪽 끝까지의 칸 수. 끝 칸에 닿으면 그 칸을 반 칸 더 지나 가장자리까지 뻗는다
            if (isHorizontal)
            {
                LaunchBeam(start, Vector2.right, width - 1 - origin.x, unit);
                LaunchBeam(start, Vector2.left, origin.x, unit);
            }
            else
            {
                LaunchBeam(start, Vector2.up, height - 1 - origin.y, unit);
                LaunchBeam(start, Vector2.down, origin.y, unit);
            }
        }

        private void LaunchBeam(Vector3 start, Vector2 direction, int tileCount, float unit)
        {
            if (tileCount <= 0) return;

            Beam beam = GetBeam();
            beam.Root.localPosition = start;
            beam.Root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            // 끝 칸의 바깥 가장자리(보드 가장자리)까지만 뻗는다. 가늘어지는 구간은 그 안쪽에 들어간다
            beam.Length = (tileCount + 0.5f) * unit;
            beam.ExtendDuration = Mathf.Max(tileCount * _adapter.ChainStepDelay, _minExtendDuration);
            beam.Age = 0f;
            beam.SparkleLength = 0.5f * unit;   // 레이저는 반 칸 길이에서 출발한다
            UpdateBeam(beam, unit);
            beam.Root.gameObject.SetActive(true);
        }

        private void LaunchFlare(Vector3 position, float unit)
        {
            Flare flare = GetFlare();
            flare.Renderer.transform.localPosition = position;
            flare.Age = 0f;
            UpdateFlare(flare, unit);
            flare.Renderer.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_adapter == null) return;
            float unit = _adapter.TileSize;
            float deltaTime = Time.deltaTime;

            foreach (Beam beam in _beams)
            {
                if (!beam.Root.gameObject.activeSelf) continue;
                beam.Age += deltaTime;
                if (beam.Age >= beam.ExtendDuration + _holdDuration + _fadeDuration)
                {
                    beam.Root.gameObject.SetActive(false);
                    continue;
                }
                UpdateBeam(beam, unit);
                SpawnSparklesAlongBeam(beam, unit);
            }

            foreach (Flare flare in _flares)
            {
                if (!flare.Renderer.gameObject.activeSelf) continue;
                flare.Age += deltaTime;
                if (flare.Age >= _originFlareDuration)
                {
                    flare.Renderer.gameObject.SetActive(false);
                    continue;
                }
                UpdateFlare(flare, unit);
            }

            foreach (Sparkle sparkle in _sparkles)
            {
                if (!sparkle.Renderer.gameObject.activeSelf) continue;
                sparkle.Age += deltaTime;
                if (sparkle.Age >= sparkle.Lifetime)
                {
                    sparkle.Renderer.gameObject.SetActive(false);
                    continue;
                }
                UpdateSparkle(sparkle, deltaTime);
            }
        }

        /// <summary>
        /// 레이저 끝이 지난 구간에 일정 간격으로 입자를 뿌린다. 프레임 길이와 상관없이 칸당 개수가 같다.
        /// </summary>
        private void SpawnSparklesAlongBeam(Beam beam, float unit)
        {
            if (_sparklesPerTile <= 0f) return;
            float spacing = unit / _sparklesPerTile;
            float tipLength = Mathf.Lerp(0.5f * unit, beam.Length, Mathf.Clamp01(beam.Age / beam.ExtendDuration));

            while (beam.SparkleLength + spacing <= tipLength)
            {
                beam.SparkleLength += spacing;
                // 레이저 방향 좌표(x)와 옆으로 퍼진 좌표(y)를 루트의 회전으로 보드 좌표로 바꾼다
                // 위·아래(가로 레이저 기준) 중 한쪽을 골라 테두리 가장자리 근처에 놓는다
                float sideSign = Random.value < 0.5f ? -1f : 1f;
                float side = sideSign * _sparkleSpread.RandomValue() * unit;
                Vector3 position = beam.Root.localPosition + beam.Root.localRotation * new Vector3(beam.SparkleLength, side, 0f);
                // 레이저에서 바깥쪽으로 살짝 밀려나며 위로 떠오른다
                Vector3 sideways = beam.Root.localRotation * new Vector3(0f, Mathf.Sign(side), 0f);
                SpawnSparkle(position, (Vector2)(sideways * 0.6f) + Vector2.up * 0.4f, unit);
            }
        }

        private void SpawnSparkle(Vector3 position, Vector2 direction, float unit)
        {
            Sparkle sparkle = GetSparkle();
            sparkle.Renderer.transform.localPosition = position;
            sparkle.Velocity = (Vector3)(direction * (Random.Range(0.3f, 1f) * _sparkleDrift * unit));
            sparkle.Size = _sparkleSize.RandomValue() * unit;
            sparkle.Lifetime = _sparkleLifetime.RandomValue();
            sparkle.Age = 0f;
            sparkle.TwinklePhase = Random.value * Mathf.PI * 2f;
            UpdateSparkle(sparkle, 0f);
            sparkle.Renderer.gameObject.SetActive(true);
        }

        /// <summary>
        /// 빠르게 나타났다가 반짝이며 천천히 사라진다. 떠다니는 속도는 점점 줄어든다.
        /// </summary>
        private void UpdateSparkle(Sparkle sparkle, float deltaTime)
        {
            float t = sparkle.Age / sparkle.Lifetime;
            sparkle.Velocity *= 1f - Mathf.Min(1f, 2f * deltaTime);   // 공기 저항처럼 서서히 느려진다
            Vector3 position = sparkle.Renderer.transform.localPosition + sparkle.Velocity * deltaTime;
            // 입자 반지름만큼 안쪽으로 가둬, 반짝임이 보드 가장자리를 넘지 않게 한다
            float radius = sparkle.Size * 0.5f;
            position.x = Mathf.Clamp(position.x, _boardBounds.xMin + radius, _boardBounds.xMax - radius);
            position.y = Mathf.Clamp(position.y, _boardBounds.yMin + radius, _boardBounds.yMax - radius);
            sparkle.Renderer.transform.localPosition = position;

            // 처음 15%는 나타나고, 그 뒤로는 (1-t)²로 은은하게 사라진다
            float envelope = t < 0.15f ? t / 0.15f : Mathf.Pow(1f - (t - 0.15f) / 0.85f, 2f);
            float twinkle = 0.65f + 0.35f * Mathf.Sin(sparkle.Age * _sparkleTwinkleSpeed * Mathf.PI * 2f + sparkle.TwinklePhase);
            sparkle.Renderer.color = WithAlpha(_sparkleColor, _sparkleColor.a * envelope * twinkle);
            sparkle.Renderer.transform.localScale = Vector3.one * (sparkle.Size * Mathf.Lerp(1f, 0.6f, t));
        }

        /// <summary>
        /// 나이에 따라 레이저 길이·두께·투명도를 정한다. 뻗기 → 머물기 → 가늘어지며 사라지기 순서다.
        /// </summary>
        private void UpdateBeam(Beam beam, float unit)
        {
            float extend = Mathf.Clamp01(beam.Age / beam.ExtendDuration);
            float fade = Mathf.Clamp01((beam.Age - beam.ExtendDuration - _holdDuration) / _fadeDuration);

            // 반 칸 길이에서 출발해 도움 타일 가장자리에서 뻗어 나가는 것처럼 보이게 한다
            float length = Mathf.Lerp(0.5f * unit, beam.Length, extend);
            float thickness = Mathf.Lerp(1f, 0.2f, fade);   // 사라질 때 가늘어진다
            float alpha = 1f - fade * fade;

            // Sliced로 그리므로 크기는 scale이 아니라 size로 정한다(단위: 타일 한 칸, scale에 unit을 곱해 둔다).
            // 둥근 시작 부분은 도움 타일 중심 뒤쪽에 놓아, 중심에서 이미 굵은 레이저가 나오는 것처럼 보이게 한다
            // 스프라이트를 만든 뒤 Inspector에서 Tail Length를 바꿔도 어긋나지 않도록 실제 스프라이트 값으로 계산한다
            float capLength = _beamSprite.border.x / _beamSprite.pixelsPerUnit;
            float sizeX = length / unit + capLength;
            SetBeamLayer(beam.Glow, sizeX, _glowWidth * thickness, capLength, unit);
            SetBeamLayer(beam.Core, sizeX, _coreWidth * thickness, capLength, unit);
            beam.Glow.color = WithAlpha(_glowColor, _glowColor.a * alpha);
            beam.Core.color = WithAlpha(_coreColor, _coreColor.a * alpha);

            // 끝 빛 덩어리는 뻗는 동안만 보이고, 다 뻗으면 빠르게 사라진다
            float tipAlpha = extend < 1f ? 1f : Mathf.Clamp01(1f - (beam.Age - beam.ExtendDuration) / 0.1f);
            // 빛 덩어리가 보드 밖으로 삐져나가지 않도록 반지름만큼 안쪽에 둔다
            float tipRadius = 0.5f * _tipFlareSize * unit;
            beam.Tip.transform.localPosition = new Vector3(Mathf.Max(0f, length - tipRadius), 0f, 0f);
            beam.Tip.transform.localScale = Vector3.one * (_tipFlareSize * unit);
            beam.Tip.color = WithAlpha(_flareColor, _flareColor.a * tipAlpha);
        }

        private static void SetBeamLayer(SpriteRenderer layer, float sizeX, float width, float capLength, float unit)
        {
            layer.transform.localPosition = new Vector3(-capLength * unit, 0f, 0f);
            layer.transform.localScale = new Vector3(unit, unit, 1f);
            layer.size = new Vector2(sizeX, width);
        }

        // 가늘어지는 구간(BeamTailTexels)이 _tailLength 칸이 되도록 텍셀 밀도를 정한다
        private float GetBeamPixelsPerUnit() => BeamTailTexels / _tailLength;

        private void UpdateFlare(Flare flare, float unit)
        {
            float t = flare.Age / _originFlareDuration;
            // 빠르게 커지고(1 - (1-t)²) 점점 투명해진다
            float grow = 1f - (1f - t) * (1f - t);
            flare.Renderer.transform.localScale = Vector3.one * (_originFlareSize * unit * Mathf.Lerp(0.5f, 1f, grow));
            flare.Renderer.color = WithAlpha(_flareColor, _flareColor.a * (1f - t));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private Beam GetBeam()
        {
            foreach (Beam beam in _beams)
            {
                if (!beam.Root.gameObject.activeSelf) return beam;
            }

            var root = new GameObject("HelperLaser");
            root.layer = _parent.gameObject.layer;
            root.transform.SetParent(_parent, false);

            var created = new Beam
            {
                Root = root.transform,
                Glow = CreateBeamRenderer("Glow", root.transform, _sortingOrder),
                Core = CreateBeamRenderer("Core", root.transform, _sortingOrder + 1),
                Tip = CreateRenderer("Tip", root.transform, _flareSprite, _sortingOrder + 2),
            };
            _beams.Add(created);
            return created;
        }

        private Flare GetFlare()
        {
            foreach (Flare flare in _flares)
            {
                if (!flare.Renderer.gameObject.activeSelf) return flare;
            }

            var created = new Flare { Renderer = CreateRenderer("HelperFlare", _parent, _flareSprite, _sortingOrder + 2) };
            _flares.Add(created);
            return created;
        }

        private Sparkle GetSparkle()
        {
            foreach (Sparkle sparkle in _sparkles)
            {
                if (!sparkle.Renderer.gameObject.activeSelf) return sparkle;
            }

            var created = new Sparkle { Renderer = CreateRenderer("HelperSparkle", _parent, _sparkleSprite, _sortingOrder + 3) };
            _sparkles.Add(created);
            return created;
        }

        private SpriteRenderer CreateBeamRenderer(string name, Transform parent, int sortingOrder)
        {
            SpriteRenderer layer = CreateRenderer(name, parent, _beamSprite, sortingOrder);
            // 시작·끝 부분은 원래 크기로 두고 가운데만 늘린다
            layer.drawMode = SpriteDrawMode.Sliced;
            return layer;
        }

        private static SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        /// <summary>
        /// 레이저 단면과 빛 덩어리 텍스처를 만든다. 둘 다 흰색이고, 색은 SpriteRenderer 색을 곱해 정한다.
        /// </summary>
        private void CreateSprites()
        {
            // 레이저: 단면은 가운데가 진하고 양옆으로 부드럽게 사라진다.
            // 길이 방향으로는 둥글게 시작해서, 가운데는 일정하고, 끝으로 갈수록 가늘고 흐려진다.
            // 기준점이 왼쪽 가운데라 x로 늘리면 오른쪽으로 뻗는다
            int beamLength = BeamCapTexels + BeamMiddleTexels + BeamTailTexels;
            _beamTexture = CreateTexture(beamLength, BeamTextureWidth, "HelperLaserBeam", (x, y) =>
            {
                float along = x + 0.5f;
                float widthFactor;   // 이 위치의 굵기 비율(0~1)
                float fade;          // 이 위치의 밝기 비율(0~1)
                if (along < BeamCapTexels)
                {
                    // 둥근 시작: 반원 모양으로 굵어지며 나타난다
                    float back = (BeamCapTexels - along) / BeamCapTexels;   // 왼쪽 끝 1 → 가운데 쪽 0
                    widthFactor = Mathf.Sqrt(1f - back * back);
                    fade = 1f - back;
                }
                else if (along < BeamCapTexels + BeamMiddleTexels)
                {
                    widthFactor = 1f;
                    fade = 1f;
                }
                else
                {
                    // 가늘어지는 끝: SmoothStep으로 처음엔 천천히, 끝에서 부드럽게 0이 된다
                    float tail = (along - BeamCapTexels - BeamMiddleTexels) / BeamTailTexels;   // 0 → 1
                    widthFactor = 1f - Mathf.SmoothStep(0f, 1f, tail);
                    fade = 1f - tail * tail;
                }

                float across = (y + 0.5f) / BeamTextureWidth * 2f - 1f;   // -1 ~ 1
                float scaledAcross = across / Mathf.Max(widthFactor, 0.001f);
                return fade * Mathf.Exp(-scaledAcross * scaledAcross * 4f);
            });
            // border(왼, 아래, 오른, 위): 시작과 끝 부분을 늘어나지 않는 테두리로 지정한다
            _beamSprite = Sprite.Create(_beamTexture, new Rect(0, 0, beamLength, BeamTextureWidth),
                new Vector2(0f, 0.5f), GetBeamPixelsPerUnit(), 0, SpriteMeshType.FullRect,
                new Vector4(BeamCapTexels, 0f, BeamTailTexels, 0f));

            // 빛 덩어리: 가운데에서 바깥으로 부드럽게 사라지는 원
            _flareTexture = CreateTexture(FlareTextureSize, FlareTextureSize, "HelperLaserFlare", (x, y) =>
            {
                float dx = (x + 0.5f) / FlareTextureSize * 2f - 1f;
                float dy = (y + 0.5f) / FlareTextureSize * 2f - 1f;
                float distanceSquared = dx * dx + dy * dy;
                return distanceSquared >= 1f ? 0f : Mathf.Exp(-distanceSquared * 5f) * (1f - distanceSquared);
            });
            _flareSprite = Sprite.Create(_flareTexture, new Rect(0, 0, FlareTextureSize, FlareTextureSize),
                new Vector2(0.5f, 0.5f), FlareTextureSize, 0, SpriteMeshType.FullRect);

            // 반짝임: 밝은 점 + 가늘게 뻗은 십자 빛살. 둥근 빛 덩어리보다 '반짝' 하는 느낌이 또렷하다
            _sparkleTexture = CreateTexture(FlareTextureSize, FlareTextureSize, "HelperSparkle", (x, y) =>
            {
                float dx = (x + 0.5f) / FlareTextureSize * 2f - 1f;
                float dy = (y + 0.5f) / FlareTextureSize * 2f - 1f;
                float core = Mathf.Exp(-(dx * dx + dy * dy) * 30f);
                // 빛살: 한 축으로는 아주 가늘고, 다른 축으로는 끝으로 갈수록 흐려진다
                float horizontal = Mathf.Exp(-dy * dy * 900f) * (1f - Mathf.Abs(dx));
                float vertical = Mathf.Exp(-dx * dx * 900f) * (1f - Mathf.Abs(dy));
                float glow = Mathf.Exp(-(dx * dx + dy * dy) * 6f) * 0.35f;
                return Mathf.Max(core, Mathf.Max(horizontal, vertical) * 0.9f, glow);
            });
            _sparkleSprite = Sprite.Create(_sparkleTexture, new Rect(0, 0, FlareTextureSize, FlareTextureSize),
                new Vector2(0.5f, 0.5f), FlareTextureSize, 0, SpriteMeshType.FullRect);
        }

        private static Texture2D CreateTexture(int width, int height, string name, System.Func<int, int, float> alphaAt)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAt(x, y)) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
