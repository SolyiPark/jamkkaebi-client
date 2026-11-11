using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    public abstract class SealMediumBackdrop : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _blendSpeed = 0.5f;
        [SerializeField] private AnimationCurve _visualCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private float _currentCorruption;
        private float _targetCorruption;

        protected float CurrentCorruption => _currentCorruption;

        protected virtual void Update()
        {
            // MoveTowards가 목표값을 그대로 돌려주므로 == 비교 가능함
            if (_currentCorruption == _targetCorruption) return;
            
            _currentCorruption = Mathf.MoveTowards(_currentCorruption, _targetCorruption, _blendSpeed * Time.deltaTime);
            ApplyCorruption(_visualCurve.Evaluate(_currentCorruption));
        }

        public void SetCorruption(float t)
        {
            _targetCorruption = Mathf.Clamp01(t);
        }

        public void SetCorruptionImmediate(float t)
        {
            _targetCorruption = Mathf.Clamp01(t);
            _currentCorruption = _targetCorruption;
            ApplyCorruption(_visualCurve.Evaluate(_currentCorruption));
        }

        protected abstract void ApplyCorruption(float t);
    }
}
