using System;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 홈 배치를 서버와 맞추는 때를 정한다. 일정 간격마다 한 번씩만 판단하고, 그 사이에는 기다린다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>첫 판단은 바로 한다. 서버 배치를 아직 불러오지 못했으면 불러오기부터 한다.</item>
    /// <item>불러오기 전에는 보내지 않는다. 빈 홈을 보내면 서버에 저장된 배치가 통째로 내려가기 때문이다.</item>
    /// <item>인터넷 연결이 없으면 그 차례를 건너뛴다.</item>
    /// <item>마지막으로 맞춘 배치와 같으면 보내지 않는다.</item>
    /// <item>실패해도 따로 다시 시도하지 않는다. 다음 차례에 그때의 배치로 판단한다.</item>
    /// </list>
    /// </remarks>
    public sealed class ExhibitionSyncSchedule
    {
        /// <summary>기본 간격. 3분.</summary>
        public const float DefaultIntervalSeconds = 180f;

        public enum Step
        {
            /// <summary>아직 차례가 아니다.</summary>
            Wait,
            /// <summary>차례지만 할 일이 없거나 연결이 없어 건너뛴다.</summary>
            Skip,
            /// <summary>서버 배치를 불러온다.</summary>
            Load,
            /// <summary>지금 배치를 보낸다.</summary>
            Send
        }

        private readonly float _interval;
        private float _nextAt;

        /// <summary>서버 배치를 불러와 홈에 반영했는지.</summary>
        public bool Loaded { get; private set; }

        /// <summary>서버와 마지막으로 맞춘 배치의 요약. 맞춘 적이 없으면 null.</summary>
        public string SyncedSignature { get; private set; }

        /// <param name="startTime">첫 판단을 할 시각. 보통 지금.</param>
        /// <exception cref="ArgumentOutOfRangeException">간격이 0 이하인 경우.</exception>
        public ExhibitionSyncSchedule(float startTime, float intervalSeconds = DefaultIntervalSeconds)
        {
            if (!(intervalSeconds > 0)) throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "간격은 0보다 커야 합니다.");
            _interval = intervalSeconds; _nextAt = startTime;
        }

        /// <summary>판단할 차례가 됐는지. 배치 요약을 만들기 전에 싸게 확인하는 데 쓴다.</summary>
        public bool IsDue(float now) => now >= _nextAt;

        /// <summary>지금 할 일을 정한다. 차례가 됐으면 무엇을 고르든 다음 차례를 한 간격 뒤로 미룬다.</summary>
        /// <param name="reachable">인터넷 연결이 확인되는지.</param>
        /// <param name="signature">지금 배치의 요약.</param>
        public Step Tick(float now, bool reachable, string signature)
        {
            if (now < _nextAt) return Step.Wait;
            _nextAt = now + _interval;
            if (!reachable) return Step.Skip;
            if (!Loaded) return Step.Load;
            return signature == SyncedSignature ? Step.Skip : Step.Send;
        }

        /// <summary>서버 배치를 홈에 반영했다. 그때의 배치는 서버와 같으므로 다시 보내지 않는다.</summary>
        public void MarkLoaded(string signature) { Loaded = true; SyncedSignature = signature; }

        /// <summary>이 배치를 서버가 저장했다.</summary>
        public void MarkSent(string signature) => SyncedSignature = signature;
    }
}
