using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// UI 그림을 제자리에서 위아래로 둥둥 띄우고 살짝 흔든다.
    /// </summary>
    /// <remarks>
    /// 처음 위치(anchoredPosition)와 각도를 기준으로 사인파만큼 더한다. 꺼질 때 처음 상태로 되돌린다.
    /// 여러 개를 같이 쓸 때는 <see cref="_phaseOffset"/>을 다르게 주면 박자가 엇갈려 자연스럽다.
    /// 게임이 멈춰도(timeScale 0) 움직이도록 unscaledTime을 쓴다.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class FloatingBob : MonoBehaviour
    {
        [Tooltip("위아래로 움직이는 거리(px). 처음 위치에서 위로도 아래로도 이만큼 간다.")]
        [SerializeField] [Min(0f)] private float _amplitude = 12f;
        [Tooltip("한 번 오르내리는 데 걸리는 시간(초).")]
        [SerializeField] [Min(0.1f)] private float _period = 2.4f;
        [Tooltip("좌우로 기우는 최대 각도(도).")]
        [SerializeField] [Min(0f)] private float _swayDegrees = 6f;
        [Tooltip("박자를 얼마나 늦출지(0~1 = 한 주기 안에서의 위치).")]
        [SerializeField] [Range(0f, 1f)] private float _phaseOffset;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private float _baseAngle;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _baseAngle = _rect.localEulerAngles.z;
        }

        private void Update()
        {
            // 0~1을 한 바퀴(2π)로 바꿔 사인에 넣는다
            float angle = (Time.unscaledTime / _period + _phaseOffset) * Mathf.PI * 2f;
            _rect.anchoredPosition = _basePosition + new Vector2(0f, Mathf.Sin(angle) * _amplitude);
            // 기울기는 높이보다 1/4 주기 늦게(cos) 움직여, 올라가며 기울고 꼭대기에서 돌아온다
            _rect.localEulerAngles = new Vector3(0f, 0f, _baseAngle + Mathf.Cos(angle) * _swayDegrees);
        }

        private void OnDisable()
        {
            if (_rect == null) return;
            _rect.anchoredPosition = _basePosition;
            _rect.localEulerAngles = new Vector3(0f, 0f, _baseAngle);
        }
    }
}
