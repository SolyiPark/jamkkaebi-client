using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>
    /// 선명도 계산. 선명도 기준 시각에서 매번 계산한다.
    /// </summary>
    /// <remarks>
    /// 상태를 직접 저장하지 않고 시각으로부터 계산한다. 기준 시각은 마지막 정화 시각과 따로 둔다.
    /// 복구 한 단계를 깨면 선명도가 한 칸 오르도록 기준 시각을 당겨 저장한다(<see cref="AnchorFor"/>).
    /// 오염 관리를 마치지 않은 유물에는 선명도가 없으므로 호출하는 쪽에서 먼저 완료 여부를 확인한다.
    /// 서버에 같은 규칙으로 바꿔 달라고 요청해 두었다(작업대 서버 수정 요청 2차, 2026-11-22).
    /// </remarks>
    public sealed class ClarityPolicy
    {
        public static readonly TimeSpan DefaultFadedAfter = TimeSpan.FromDays(7);
        public static readonly TimeSpan DefaultResealedAfter = TimeSpan.FromDays(14);

        /// <summary>서버 기본값(7일, 14일)을 쓰는 정책.</summary>
        public static ClarityPolicy Default { get; } = new ClarityPolicy(DefaultFadedAfter, DefaultResealedAfter);

        /// <summary>기준 시각 후 흐려짐이 되기까지의 시간</summary>
        public TimeSpan FadedAfter { get; }
        /// <summary>기준 시각 후 재봉인이 되기까지의 시간</summary>
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

        /// <summary>현재 선명도. 기기 시각이 기준 시각보다 앞서 있으면 선명으로 본다.</summary>
        public Clarity ClarityOf(DateTime clarityAnchorAt, DateTime now)
        {
            TimeSpan elapsed = now - clarityAnchorAt;
            if (elapsed >= ResealedAfter) return Clarity.Resealed;
            if (elapsed >= FadedAfter) return Clarity.Faded;
            return Clarity.Clear;
        }

        /// <summary>이대로 두면 선명도가 한 칸 떨어지는 시각(선명 → 흐려짐, 흐려짐 → 재봉인). 이미 재봉인됐으면 null.</summary>
        public DateTime? NextChangeAt(DateTime clarityAnchorAt, DateTime now)
        {
            return ClarityOf(clarityAnchorAt, now) switch
            {
                Clarity.Clear => clarityAnchorAt + FadedAfter,
                Clarity.Faded => clarityAnchorAt + ResealedAfter,
                _ => (DateTime?)null
            };
        }

        /// <summary>
        /// 지금 이 선명도가 막 시작된 것으로 보는 기준 시각. 선명은 지금, 흐려짐은 흐려짐이 막 시작된 시점(지금 - 흐려짐 시간).
        /// 흐려짐으로 오르면 재봉인까지 흐려짐 구간을 온전히 받는다.
        /// </summary>
        /// <exception cref="ArgumentException">재봉인이나 정의되지 않은 선명도인 경우(복구로 재봉인이 되지는 않는다).</exception>
        public DateTime AnchorFor(Clarity clarity, DateTime now)
        {
            return clarity switch
            {
                Clarity.Clear => now,
                Clarity.Faded => now - FadedAfter,
                _ => throw new ArgumentException("복구로 오를 수 있는 선명도는 흐려짐과 선명뿐입니다.", nameof(clarity))
            };
        }

        /// <summary>
        /// 선명으로 돌아가기까지 남은 페이즈(보통 난이도로 한 판씩 깰 때). 재봉인은 봉인 해제·기억 회복, 흐려짐은 기억 회복.
        /// 선명하면 복구할 것이 없으므로 관리 판(오염 관리) 하나만 돌려준다.
        /// </summary>
        public IReadOnlyList<Phase> RemainingPhases(Clarity clarity)
        {
            if (clarity == Clarity.Clear) return new[] { Phase.Guard };
            return PhaseExtensions.From(clarity.RestoreFrom())
                .Where(phase => !phase.IsLast())
                .ToList();
        }
    }
}
