using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    /// <summary>
    /// 녹 매개체 배경. 화면 가장자리에 녹이 단계별로 번져 있고, 녹 조각이 가끔 떨어진다.
    /// 정화될수록 짙은 녹부터 차례로 걷힌다.
    /// </summary>
    public class RustBackdrop : SealMediumBackdrop
    {
        [System.Serializable]
        private class RustStage
        {
            [SerializeField] private RawImage _image;
            [Tooltip("이 단계가 보이기 시작하는 오염도. 오염도가 이 값 아래로 내려가면 완전히 사라진다.")]
            [SerializeField, Range(0f, 1f)] private float _appearAt;
            [SerializeField, Range(0f, 1f)] private float _maxAlpha = 1f;

            public RawImage Image => _image;
            public float AppearAt => _appearAt;
            public float MaxAlpha => _maxAlpha;
        }

        [Tooltip("옅은 녹부터 짙은 녹 순서로. 뒤 단계일수록 AppearAt을 높게 준다.")]
        [SerializeField] private RustStage[] _stages;
        [SerializeField] private UiDriftParticles _flakes;

        protected override void ApplyCorruption(float t)
        {
            foreach (RustStage stage in _stages)
            {
                if (stage.Image == null) continue;

                // AppearAt~1 구간을 0~1로 바꾼다. 짙은 녹(AppearAt이 높음)은 정화 초반에 먼저 사라진다
                float visibility = Mathf.InverseLerp(stage.AppearAt, 1f, t);
                Color color = stage.Image.color;
                color.a = stage.MaxAlpha * visibility;
                stage.Image.color = color;
            }

            if (_flakes != null) _flakes.Intensity = t;
        }

        [ContextMenu("Test Corruption to Min")]
        private void TestCorruptionMin() => SetCorruption(0f);

        [ContextMenu("Test Corruption to Max")]
        private void TestCorruptionMax() => SetCorruption(1f);
    }
}
