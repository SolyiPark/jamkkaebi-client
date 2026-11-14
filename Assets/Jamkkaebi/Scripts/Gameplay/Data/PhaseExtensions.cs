using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>페이즈 순서 계산.</summary>
    public static class PhaseExtensions
    {
        /// <summary>1부터 시작하는 페이즈 번호. 미니게임 PhaseData의 번호와 같다.</summary>
        public static int Order(this Phase phase) => (int)phase + 1;

        public static bool IsLast(this Phase phase) => phase == Phase.Guard;

        /// <summary>다음 페이즈. 마지막이면 null.</summary>
        public static Phase? Next(this Phase phase) => phase.IsLast() ? (Phase?)null : phase + 1;

        /// <summary>other보다 뒤에 오는지. other가 null이면 true. 복구 순서에서 이미 클리어한 페이즈를 뺄 때 쓴다.</summary>
        public static bool IsAfter(this Phase phase, Phase? other) => other == null || phase > other.Value;

        /// <summary>from부터 마지막 페이즈까지의 순서.</summary>
        public static IReadOnlyList<Phase> From(Phase from)
        {
            var phases = new List<Phase>();
            for (Phase phase = from; phase <= Phase.Guard; phase++)
                phases.Add(phase);
            return phases;
        }
    }
}