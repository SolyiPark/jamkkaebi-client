using System;
using System.Collections.Generic;
using System.Linq;

namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>돌봄(관리 플레이 성공) 횟수로 친밀도 단계와 게이지를 계산한다. 기준값은 기획 확정 전 임시값.</summary>
    public sealed class IntimacyPolicy
    {
        private static readonly int[] DefaultLevelUpCareCounts = { 3, 7, 12, 20 };
        public static IntimacyPolicy Default { get; } = new IntimacyPolicy(DefaultLevelUpCareCounts);

        private readonly int[] _levelUpCareCounts;

        public IReadOnlyList<int> LevelUpCareCounts { get; }
        public int MaxLevel => _levelUpCareCounts.Length + 1;

        public IntimacyPolicy(IReadOnlyList<int> levelUpCareCounts)
        {
            if (levelUpCareCounts == null) throw new ArgumentNullException(nameof(levelUpCareCounts));
            if (levelUpCareCounts.Count == 0) throw new ArgumentException("레벨업 기준값이 하나 이상 있어야 합니다.", nameof(levelUpCareCounts));
            if (levelUpCareCounts[0] <= 0) throw new ArgumentOutOfRangeException(nameof(levelUpCareCounts), "레벨업 기준은 0회보다 커야합니다.");
            for (int i = 0; i < levelUpCareCounts.Count - 1; i++)
            {
                if (levelUpCareCounts[i] >= levelUpCareCounts[i + 1]) throw new ArgumentException("이후 레벨의 기준값이 이전 레벨의 기준값보다 높아야 합니다.", nameof(levelUpCareCounts));
            }
           
            _levelUpCareCounts = levelUpCareCounts.ToArray();
            LevelUpCareCounts = Array.AsReadOnly(_levelUpCareCounts);
        }

        public IntimacyGauge GaugeOf(int careCount)
        {
            if (careCount < 0) throw new ArgumentOutOfRangeException(nameof(careCount), "돌봄 횟수는 0회 이상이어야 합니다.");
            
            int level = MaxLevel;
            int current = careCount - _levelUpCareCounts[_levelUpCareCounts.Length - 1];
            int max = 0;
            
            for (int i = 0; i < _levelUpCareCounts.Length; i++)
            {
                if (careCount < _levelUpCareCounts[i])
                {
                    int previous = i == 0 ? 0 : _levelUpCareCounts[i - 1];
                    level = i + 1;
                    current = careCount - previous;
                    max = _levelUpCareCounts[i] - previous;
                    break;
                }
            }
            
            return new IntimacyGauge(level, current, max);
        }
    }
}