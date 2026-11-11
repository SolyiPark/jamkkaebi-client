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
            
            public RawImage Image => _image;
            public Vector2 ScrollSpeed => _scrollSpeed;
            public float MaxAlpha => _maxAlpha;
        }
        
        [SerializeField] private FogLayer[] _layers;
        [SerializeField] private RawImage _edge;
        
        [SerializeField] private float _cleanEdgeScale = 1.4f;

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
                Color color = layer.Image.color;
                color.a = layer.MaxAlpha * t;
                layer.Image.color = color;
            }

            _edge.rectTransform.localScale = Vector3.one * Mathf.Lerp(_cleanEdgeScale, 1f, t);
        }

        [ContextMenu("Test Corruption to Min")]
        private void TestCorruptionMin() => SetCorruption(0f);

        [ContextMenu("Test Corruption to Max")]
        private void TestCorruptionMax() => SetCorruption(1f);
    }
}