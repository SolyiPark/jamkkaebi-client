using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// UI 그림의 투명도와 크기를 주기적으로 오르내리게 해 은은하게 빛나거나 반짝이게 한다.
    /// </summary>
    /// <remarks>
    /// 켜질 때 <see cref="_fadeInDuration"/> 동안 투명도를 0부터 서서히 올리고, 그 뒤로 사인파로 숨 쉬듯 반복한다.
    /// 색(RGB)은 Image의 Color를 그대로 쓰고 알파만 바꾼다. 주기·크기 범위를 바꿔 은은한 빛부터 빠른 반짝임까지 만든다.
    /// 게임이 멈춰도(timeScale 0) 움직이도록 unscaledTime을 쓴다.
    /// </remarks>
    [RequireComponent(typeof(Graphic))]
    public class PulseEffect : MonoBehaviour
    {
        [Tooltip("가장 흐릴 때와 가장 진할 때의 알파.")]
        [SerializeField] private Vector2 _alphaRange = new Vector2(0.35f, 0.6f);
        [Tooltip("가장 작을 때와 가장 클 때의 크기 배율.")]
        [SerializeField] private Vector2 _scaleRange = new Vector2(0.97f, 1.03f);
        [Tooltip("한 번 오르내리는 데 걸리는 시간(초).")]
        [SerializeField] [Min(0.1f)] private float _period = 2.5f;
        [Tooltip("박자를 얼마나 늦출지(0~1 = 한 주기 안에서의 위치). 여러 개를 엇갈리게 할 때 쓴다.")]
        [SerializeField] [Range(0f, 1f)] private float _phaseOffset;
        [Tooltip("켜질 때 투명도를 0에서 올리는 시간(초). 0이면 바로 보인다.")]
        [SerializeField] [Min(0f)] private float _fadeInDuration = 0.4f;
        [Tooltip("켜질 때 이 시간(초)만큼 기다렸다가 나타난다. 반짝임을 하나씩 차례로 띄울 때 쓴다.")]
        [SerializeField] [Min(0f)] private float _startDelay;

        private Graphic _graphic;
        private Vector3 _baseScale;
        private float _enabledTime;

        private void Awake()
        {
            _graphic = GetComponent<Graphic>();
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            _enabledTime = Time.unscaledTime;
            Apply();   // 첫 프레임부터 투명한 상태로 시작해, 켜지는 순간 번쩍이지 않게 한다
        }

        private void Update() => Apply();

        private void OnDisable()
        {
            transform.localScale = _baseScale;
        }

        private void Apply()
        {
            float sinceEnabled = Time.unscaledTime - _enabledTime - _startDelay;
            // 0~1로 오르내리는 값. 사인(-1~1)을 0~1로 옮긴다
            float wave = Mathf.Sin((Time.unscaledTime / _period + _phaseOffset) * Mathf.PI * 2f) * 0.5f + 0.5f;
            float fade = _fadeInDuration > 0f ? Mathf.Clamp01(sinceEnabled / _fadeInDuration) : (sinceEnabled >= 0f ? 1f : 0f);

            Color color = _graphic.color;
            color.a = Mathf.Lerp(_alphaRange.x, _alphaRange.y, wave) * fade;
            _graphic.color = color;

            float scale = Mathf.Lerp(_scaleRange.x, _scaleRange.y, wave);
            transform.localScale = _baseScale * scale;
        }
    }
}
