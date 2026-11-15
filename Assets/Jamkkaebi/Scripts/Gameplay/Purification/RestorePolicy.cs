using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>난이도별 복구 효과와, 선명도에 따라 고를 수 있는 난이도를 정한다. 기획 확정값(2026-11-15).</summary>
    public sealed class RestorePolicy
    {
        private static readonly Dictionary<MinigameDifficulty, RestoreEffect> _defaultEffects =
            new()
            {
                { MinigameDifficulty.Easy, RestoreEffect.RefreshOnly },
                { MinigameDifficulty.Normal, RestoreEffect.OneStep },
                { MinigameDifficulty.Hard, RestoreEffect.Full }
            };

        private readonly Dictionary<MinigameDifficulty, RestoreEffect> _effects;

        public RestorePolicy(IReadOnlyDictionary<MinigameDifficulty, RestoreEffect> effects)
        {
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (effects.Count <= 0) throw new ArgumentException("난이도별 복구 효과 데이터가 비어 있습니다.", nameof(effects));
            foreach (KeyValuePair<MinigameDifficulty, RestoreEffect> pair in effects)
            {
                if (!Enum.IsDefined(typeof(MinigameDifficulty), pair.Key))
                    throw new ArgumentException($"존재하지 않는 난이도({(int)pair.Key})입니다.", nameof(effects));
                if (!Enum.IsDefined(typeof(RestoreEffect), pair.Value))
                    throw new ArgumentException($"존재하지 않는 복구 효과({(int)pair.Value})입니다.", nameof(effects));
            }

            _effects = effects.ToDictionary(x => x.Key, x => x.Value);
        }

        public static RestorePolicy Default { get; } = new(_defaultEffects);

        /// <exception cref="ArgumentException">등록되지 않은 난이도인 경우.</exception>
        public RestoreEffect EffectOf(MinigameDifficulty difficulty)
        {
            if (!_effects.TryGetValue(difficulty, out var effect))
                throw new ArgumentException("등록되지 않은 난이도입니다.", nameof(difficulty));
            return effect;
        }

        /// <summary>이 선명도에서 관리 플레이로 고를 수 있는 난이도. 쉬운 순.</summary>
        public IReadOnlyList<MinigameDifficulty> AllowedFor(Clarity clarity)
        {
            if (clarity == Clarity.Clear) return _effects.Keys.OrderBy(difficulty => difficulty).ToList();

            return _effects
                .Where(pair => pair.Value != RestoreEffect.RefreshOnly)
                .Select(pair => pair.Key)
                .OrderBy(difficulty => difficulty)
                .ToList();
        }
    }
}