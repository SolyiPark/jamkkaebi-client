using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>
    /// 선명도 계산. 마지막 정화 시각에서 매번 계산한다.
    /// 서버 ClarityPolicy와 같은 규칙이다.
    /// </summary>
    /// <remarks>
    /// 상태를 직접 저장하지 않고 시각으로부터 계산한다.
    /// 오염 관리를 마치지 않은 유물에는 선명도가 없으므로 호출하는 쪽에서 먼저 완료 여부를 확인한다.
    /// </remarks>
    public sealed class ClarityPolicy
    {
        public static readonly TimeSpan DefaultFadedAfter = TimeSpan.FromDays(7);
        public static readonly TimeSpan DefaultResealedAfter = TimeSpan.FromDays(14);
        
        /// <summary>서버 기본값(7일, 14일)을 쓰는 정책.</summary>
        public static ClarityPolicy Default { get; } = new ClarityPolicy(DefaultFadedAfter, DefaultResealedAfter);

        /// <summary>마지막 정화 후 흐려짐이 되기까지의 시간</summary>
        public TimeSpan FadedAfter { get; }
        /// <summary>마지막 정화 후 재봉인이 되기까지의 시간</summary>
        public TimeSpan ResealedAfter { get; }

        
        /// <exception cref="ArgumentOutOfRangeException"> 흐려짐 시간이 0 이하이거나 재봉인 시간이 흐려짐 시간 이하인 경우.</exception>
        public ClarityPolicy(TimeSpan fadedAfter, TimeSpan resealedAfter)
        {
            if (fadedAfter <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(fadedAfter), "0보다 커야 합니다.");
            if (resealedAfter <= fadedAfter)
                throw new ArgumentOutOfRangeException(nameof(resealedAfter), "흐려짐 시간보다 커야 합니다.");
            
            FadedAfter = fadedAfter;
            ResealedAfter = resealedAfter;
        }

        /// <summary>현재 선명도. 기기 시각이 마지막 정화보다 앞서 있으면 선명으로 본다.</summary>
        public Clarity ClarityOf(DateTime lastPurifiedAt, DateTime now)
        {
            TimeSpan elapsed = now - lastPurifiedAt;
            if (elapsed >= ResealedAfter) return Clarity.Resealed;
            if (elapsed >= FadedAfter) return Clarity.Faded;
            return Clarity.Clear;
        }

        /// <summary>이대로 두면 재봉인되는 시각. 이미 재봉인됐으면 null.</summary>
        public DateTime? ResealAt(DateTime lastPurifiedAt, DateTime now)
        {
            return ClarityOf(lastPurifiedAt, now) == Clarity.Resealed ? (DateTime?)null : lastPurifiedAt + ResealedAfter;
        }

        /// <summary>
        /// 선명으로 돌아가기까지 남은 페이즈. 이번 복구에서 이미 클리어한 페이즈는 뺀다.
        /// 비어 있으면 복구할 것이 없다는 뜻이며, 그때 무엇을 플레이할지는 정화 계획이 정한다.
        /// </summary>
        /// <param name="clarity">현재 선명도.</param>
        /// <param name="lastClearedPhase">이번 복구 순서에서 마지막으로 클리어한 페이즈. 복구 중이 아니면 null</param>
        public IReadOnlyList<Phase> RemainingPhases(Clarity clarity, Phase? lastClearedPhase)
        {
            return PhaseExtensions.From(clarity.RestoreFrom())
                .Where(phase => phase.IsAfter(lastClearedPhase))
                .ToList();
        }
    }
}