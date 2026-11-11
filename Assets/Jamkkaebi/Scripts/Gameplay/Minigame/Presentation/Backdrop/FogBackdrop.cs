using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    public class FogBackdrop : SealMediumBackdrop
    {
        [System.Serializable]
        private class FogLayer
        {
            [SerializeField] private RawImage _image;
            [SerializeField] private Vector2 _scrollSpeed;
            [SerializeField] private float _maxAlpha;
            [Tooltip("배경이 완전히 밝아졌을 때(깨어남 1) 쓸 색. 어두운 배경용 색은 이미지에 칠해 둔 색이다.")]
            [SerializeField] private Color _awakenedColor = Color.white;
            
            public RawImage Image => _image;
            public Vector2 ScrollSpeed => _scrollSpeed;
            public float MaxAlpha => _maxAlpha;
            public Color AwakenedColor => _awakenedColor;
            public Color SealedColor { get; set; }   // 이미지에 칠해 둔 원래 색. 실행 중에만 기억한다
        }
        
        [SerializeField] private FogLayer[] _layers;
        [SerializeField] private RawImage _edge;
        
        [SerializeField] private float _cleanEdgeScale = 1.4f;
        [Tooltip("배경이 완전히 밝아졌을 때 가장자리 안개 색.")]
        [SerializeField] private Color _edgeAwakenedColor = Color.white;

        private Color _edgeSealedColor;

        private void Awake()
        {
            // 어두운 배경용 색은 Inspector에서 칠해 둔 이미지 색을 그대로 쓴다
            foreach (FogLayer layer in _layers)
            {
                layer.SealedColor = layer.Image.color;
            }
            _edgeSealedColor = _edge.color;
        }

        protected override void Update()
        {
            base.Update();
            
            foreach (FogLayer layer in _layers)
            {
                Rect uv = layer.Image.uvRect;
                uv.x = Mathf.Repeat(uv.x + layer.ScrollSpeed.x * Time.deltaTime, 1f);
                uv.y = Mathf.Repeat(uv.y + layer.ScrollSpeed.y * Time.deltaTime, 1f);
                layer.Image.uvRect = uv;
            }
        }

        protected override void ApplyCorruption(float t)
        {
            foreach (FogLayer layer in _layers)
            {
                Color color = Color.Lerp(layer.SealedColor, layer.AwakenedColor, Awakening);
                color.a = layer.MaxAlpha * t;
                layer.Image.color = color;
            }

            Color edgeColor = Color.Lerp(_edgeSealedColor, _edgeAwakenedColor, Awakening);
            edgeColor.a = _edgeSealedColor.a;
            _edge.color = edgeColor;
            _edge.rectTransform.localScale = Vector3.one * Mathf.Lerp(_cleanEdgeScale, 1f, t);
        }

        [ContextMenu("Test Corruption to Min")]
        private void TestCorruptionMin() => SetCorruption(0f);

        [ContextMenu("Test Corruption to Max")]
        private void TestCorruptionMax() => SetCorruption(1f);
    }
}