using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    /// <summary>
    /// 뿌리 매개체 배경. 화면 모서리와 옆면에서 흰 뿌리가 파고들어 살짝 꿈틀거리고, 숨 쉬듯 은은하게 빛난다.
    /// 정화될수록 뿌리가 화면 밖으로 물러난다.
    /// </summary>
    public class RootBackdrop : SealMediumBackdrop
    {
        [System.Serializable]
        private class RootBranch
        {
            [Tooltip("뿌리 이미지. 피벗을 뿌리가 시작되는 쪽(화면 가장자리)에 두면 그 점을 축으로 흔들린다.")]
            [SerializeField] private RectTransform _target;
            [Tooltip("정화가 끝났을 때 원래 자리에서 물러나 있을 거리(px). 화면 밖을 향하게 준다.")]
            [SerializeField] private Vector2 _retractOffset;
            [Tooltip("꿈틀거리는 각도(도).")]
            [SerializeField, Min(0f)] private float _swayDegrees = 2f;
            [SerializeField, Min(0.1f)] private float _swayPeriod = 4f;

            public RectTransform Target => _target;
            public Vector2 RetractOffset => _retractOffset;
            public float SwayDegrees => _swayDegrees;
            public float SwayPeriod => _swayPeriod;

            // 씬에 놓인 원래 자리. 실행 중에만 쓰므로 직렬화하지 않는다
            public Vector2 BasePosition { get; set; }
            public float BaseAngle { get; set; }
            public Graphic Graphic { get; set; }
            public Color SealedColor { get; set; }
        }

        [SerializeField] private RootBranch[] _branches;

        [Header("빛")]
        [Tooltip("뿌리의 최대 불투명도. 낮을수록 배경이 비쳐 보여 덜 눈에 띈다.")]
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.55f;
        [Tooltip("밝기가 오르내리는 폭. 0이면 빛나지 않고 그대로 보인다.")]
        [SerializeField, Range(0f, 1f)] private float _glowPulse = 0.25f;
        [SerializeField, Min(0.1f)] private float _glowPeriod = 3f;
        [Tooltip("배경이 완전히 밝아졌을 때 뿌리 색. 밝은 바탕에서는 흰 뿌리가 묻히므로 어둡게 바꾼다.")]
        [SerializeField] private Color _awakenedColor = new Color(0.5f, 0.44f, 0.36f, 1f);

        private bool _hasBasePose;
        private float _presence;   // 화면에 남아 있는 정도. 흔들림 크기에도 곱한다

        private void Awake()
        {
            CacheBasePose();
        }

        protected override void Update()
        {
            base.Update();

            for (int i = 0; i < _branches.Length; i++)
            {
                RootBranch branch = _branches[i];
                if (branch.Target == null) continue;

                // 가지마다 위상을 달리해 한꺼번에 같은 방향으로 움직이지 않게 한다
                float phase = i * 1.7f;
                float angle = Mathf.Sin(Time.time * Mathf.PI * 2f / branch.SwayPeriod + phase) * branch.SwayDegrees * _presence;
                branch.Target.localRotation = Quaternion.Euler(0f, 0f, branch.BaseAngle + angle);

                // 텍스처에 빛 번짐이 그려져 있어, 투명도를 천천히 오르내리면 숨 쉬듯 빛나 보인다
                if (branch.Graphic != null)
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / _glowPeriod + phase);
                    Color color = branch.Graphic.color;
                    color.a = _maxAlpha * (1f - _glowPulse * wave);
                    branch.Graphic.color = color;
                }
            }
        }

        protected override void ApplyCorruption(float t)
        {
            CacheBasePose();
            _presence = t;

            foreach (RootBranch branch in _branches)
            {
                if (branch.Target == null) continue;
                branch.Target.anchoredPosition = branch.BasePosition + branch.RetractOffset * (1f - t);

                if (branch.Graphic != null)
                {
                    // 알파는 빛남(Update)이 맡으므로 색만 바꾼다
                    Color color = Color.Lerp(branch.SealedColor, _awakenedColor, Awakening);
                    color.a = branch.Graphic.color.a;
                    branch.Graphic.color = color;
                }
            }
        }

        private void CacheBasePose()
        {
            if (_hasBasePose) return;
            _hasBasePose = true;

            foreach (RootBranch branch in _branches)
            {
                if (branch.Target == null) continue;
                branch.BasePosition = branch.Target.anchoredPosition;
                branch.BaseAngle = branch.Target.localEulerAngles.z;
                branch.Graphic = branch.Target.GetComponent<Graphic>();
                if (branch.Graphic != null) branch.SealedColor = branch.Graphic.color;
            }
        }

        [ContextMenu("Test Corruption to Min")]
        private void TestCorruptionMin() => SetCorruption(0f);

        [ContextMenu("Test Corruption to Max")]
        private void TestCorruptionMax() => SetCorruption(1f);
    }
}
