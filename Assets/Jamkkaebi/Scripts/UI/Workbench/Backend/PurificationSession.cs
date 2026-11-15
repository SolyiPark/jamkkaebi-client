using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>시작한 정화 한 판. 서버 PurificationStartResponse 중 필요한 부분.</summary>
    public sealed class PurificationSession
    {
        /// <summary>결과 보고에 쓰는 1회용 토큰.</summary>
        public string SessionToken { get; }

        public int RelicId { get; }
        public EntryRoute Route { get; }
        public Phase Phase { get; }
        public MinigameDifficulty Difficulty { get; }

        public PurificationSession(string sessionToken, int relicId, EntryRoute route,
            Phase phase, MinigameDifficulty difficulty)
        {
            if (sessionToken == null) throw new ArgumentNullException(nameof(sessionToken));
            if (sessionToken.Length == 0) throw new ArgumentException("sessionToken이 비어있습니다.", nameof(sessionToken));
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            if (!Enum.IsDefined(typeof(EntryRoute), route))
                throw new ArgumentException($"정화 진입 경로 {route}는 정의되지 않았습니다.", nameof(route));
            if (!Enum.IsDefined(typeof(Phase), phase))
                throw new ArgumentException($"페이즈 {phase}는 정의되지 않았습니다.", nameof(phase));
            if (!Enum.IsDefined(typeof(MinigameDifficulty), difficulty))
                throw new ArgumentException($"미니게임 난이도 {difficulty}는 정의되지 않았습니다.", nameof(difficulty));

            SessionToken = sessionToken;
            RelicId = relicId;
            Route = route;
            Phase = phase;
            Difficulty = difficulty;
        }
    }
}