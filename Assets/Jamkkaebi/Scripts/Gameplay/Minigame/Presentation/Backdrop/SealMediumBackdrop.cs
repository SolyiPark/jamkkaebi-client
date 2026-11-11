using Jamkkaebi.Scripts.Gameplay.Data;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    public abstract class SealMediumBackdrop : MonoBehaviour
    {
        [Tooltip("이 배경이 담당하는 봉인 매개체. 판이 시작되면 같은 매개체의 배경만 켜진다.")]
        [SerializeField] private SealMedium _sealMedium;
        [SerializeField, Min(0f)] private float _blendSpeed = 0.5f;
        [SerializeField] private AnimationCurve _visualCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private float _currentCorruption;
        private float _targetCorruption;
        private float _intensity = 1f;
        private float _awakening;

        public SealMedium SealMedium => _sealMedium;
        protected float CurrentCorruption => _currentCorruption;
        /// <summary>봉인이 풀린 정도(0~1). 배경이 밝아지는 만큼 자식이 연출 색을 밝은 배경용으로 바꾸는 데 쓴다.</summary>
        protected float Awakening => _awakening;

        protected virtual void Update()
        {
            // MoveTowards가 목표값을 그대로 돌려주므로 == 비교 가능함
            if (_currentCorruption == _targetCorruption) return;
            
            _currentCorruption = Mathf.MoveTowards(_currentCorruption, _targetCorruption, _blendSpeed * Time.deltaTime);
            ApplyVisual();
        }

        /// <summary>
        /// 연출 전체의 세기(0~1)를 정한다. 페이즈가 오를수록 봉인이 약해지므로 낮은 값을 준다.
        /// </summary>
        /// <remarks>오염도와 따로 곱해지므로, 세기가 0.5면 오염도가 최대여도 절반만큼만 보인다.</remarks>
        public void SetIntensity(float intensity)
        {
            _intensity = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// 봉인이 풀린 정도(0~1)를 정한다. 다음 <see cref="ApplyCorruption"/>부터 반영된다.
        /// </summary>
        public void SetAwakening(float awakening)
        {
            _awakening = Mathf.Clamp01(awakening);
        }

        public void SetCorruption(float t)
        {
            _targetCorruption = Mathf.Clamp01(t);
        }

        public void SetCorruptionImmediate(float t)
        {
            _targetCorruption = Mathf.Clamp01(t);
            _currentCorruption = _targetCorruption;
            ApplyVisual();
        }

        protected abstract void ApplyCorruption(float t);

        // 화면에 보낼 값: 실제 오염도를 커브로 바꾼 뒤 페이즈 세기를 곱한다
        private void ApplyVisual()
        {
            ApplyCorruption(_visualCurve.Evaluate(_currentCorruption) * _intensity);
        }
    }
}
