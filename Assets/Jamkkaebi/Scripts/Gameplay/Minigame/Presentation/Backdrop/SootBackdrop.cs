using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    /// <summary>
    /// 그을음 매개체 배경. 화면 가장자리가 검게 그을려 있고, 재가 천천히 피어오르며 가끔 불씨가 섞인다.
    /// 정화될수록 그을음이 화면 밖으로 밀려나고 재가 줄어든다.
    /// </summary>
    public class SootBackdrop : SealMediumBackdrop
    {
        [SerializeField] private RawImage _edge;
        [SerializeField, Range(0f, 1f)] private float _edgeMaxAlpha = 0.85f;
        [Tooltip("정화가 끝났을 때 가장자리 크기. 1보다 클수록 그을음이 화면 밖으로 밀려난다.")]
        [SerializeField, Min(1f)] private float _cleanEdgeScale = 1.3f;
        [SerializeField] private UiDriftParticles _ash;

        protected override void ApplyCorruption(float t)
        {
            Color color = _edge.color;
            color.a = _edgeMaxAlpha * t;
            _edge.color = color;
            _edge.rectTransform.localScale = Vector3.one * Mathf.Lerp(_cleanEdgeScale, 1f, t);

            if (_ash != null) _ash.Intensity = t;
        }

        [ContextMenu("Test Corruption to Min")]
        private void TestCorruptionMin() => SetCorruption(0f);

        [ContextMenu("Test Corruption to Max")]
        private void TestCorruptionMax() => SetCorruption(1f);
    }
}
