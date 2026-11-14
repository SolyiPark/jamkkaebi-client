using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>이 유물로 다음에 할 한 판. 서버 PlayPlan에 해당한다.</summary>
    public sealed class PlayPlan
    {
        public PlayPlan(EntryRoute route, IReadOnlyList<Phase> remainingPhases,
            IReadOnlyList<MinigameDifficulty> allowedDifficulties)
        {
            if (remainingPhases == null) throw new ArgumentNullException(nameof(remainingPhases));
            if (remainingPhases.Count < 1)
                throw new ArgumentException("남은 페이즈는 하나 이상 존재해야 합니다.", nameof(remainingPhases));
            if (allowedDifficulties == null) throw new ArgumentNullException(nameof(allowedDifficulties));
            if (allowedDifficulties.Count < 1)
                throw new ArgumentException("가능한 난이도는 하나 이상 존재해야 합니다.", nameof(allowedDifficulties));

            Route = route;
            RemainingPhases = remainingPhases.ToList().AsReadOnly();
            AllowedDifficulties = allowedDifficulties.ToList().AsReadOnly();
            Phase = remainingPhases.First();
        }

        public EntryRoute Route { get; }

        /// <summary>이번에 할 페이즈.</summary>
        public Phase Phase { get; }

        /// <summary>이번 것을 포함해 남은 순서.</summary>
        public IReadOnlyList<Phase> RemainingPhases { get; }

        /// <summary>이번 판에 고를 수 있는 난이도. 하나뿐이면 고정이다.</summary>
        public IReadOnlyList<MinigameDifficulty> AllowedDifficulties { get; }

        public bool DifficultySelectable => AllowedDifficulties.Count > 1;

        /// <summary>이 난이도로 시작해도 되는지. 미니게임 시작 전에 확인한다.</summary>
        public bool Allows(MinigameDifficulty difficulty)
        {
            return AllowedDifficulties.Contains(difficulty);
        }
    }
}