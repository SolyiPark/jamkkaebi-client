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
        /// <summary>
        /// 새 실행 세션에서 최초 등장 재생 여부를 초기화하며 도메인 재로드가 꺼진 플레이 모드도 지원합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => _sessionShown = false;
        /// <summary>
        /// 홈 첫 표시에서 등장 연출을 시작합니다. 이미 시작했으면 유지하고, 세션에서 재생했거나 설정이 꺼져 있으면 완료 상태로 만듭니다.
        /// </summary>
        public void Begin()
        {
            if (_started) return;
            _started = true;
            if (_sessionShown || !_playOnSessionStart) Complete();
            _sessionShown = true;
        }
        /// <summary>
        /// 등장 시간을 모든 레이어가 표시되는 완료 상태로 이동하고 세션 재생 완료를 기록합니다. 시차 연출은 중지하지 않습니다.
        /// </summary>
        public void Complete()
        {
            _started = true;
            _sessionShown = true;
            Elapsed = _duration + 10;
            IsComplete = true;
        }
        /// <summary>
        /// 등장 연출이 진행 중일 때 시간 배율과 무관한 경과 시간을 누적하고 설정한 길이에 도달하면 완료합니다.
        /// </summary>
        private void Update()
        {
            if (!_started || IsComplete) return;
            Elapsed += Time.unscaledDeltaTime;
            if (Elapsed >= _duration) Complete();
        }
    }
}
