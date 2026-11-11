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

        public SealMedium SealMedium => _sealMedium;
        protected float CurrentCorruption => _currentCorruption;

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
