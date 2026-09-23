using UnityEngine;

namespace MobilePrototype.Exhibition
{
    public sealed class ExhibitionIntro : MonoBehaviour
    {
        [SerializeField] private bool _playOnSessionStart = true;
        [SerializeField] private float _duration = 1.65f;
        private static bool _sessionShown;
        private bool _started;
        public float Elapsed { get; private set; }
        public bool IsComplete { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _sessionShown = false;
        public void Begin()
        {
            if (_started) return;
            _started = true;
            if (_sessionShown || !_playOnSessionStart) Complete();
            _sessionShown = true;
        }
        public void Complete()
        {
            _started = true;
            _sessionShown = true;
            Elapsed = _duration + 10;
            IsComplete = true;
        }
        private void Update()
        {
            if (!_started || IsComplete) return;
            Elapsed += Time.unscaledDeltaTime;
            if (Elapsed >= _duration) Complete();
        }
    }
}
