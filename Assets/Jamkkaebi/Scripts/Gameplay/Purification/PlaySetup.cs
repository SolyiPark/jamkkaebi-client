using System;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>
    /// 미니게임 한 판을 시작하는 데 필요한 값. 정화 시작 응답(서버 PurificationStartResponse)에서 만든다.
    /// 어댑터는 인스펙터 값 대신 이 값으로 판을 연다(4-2).
    /// </summary>
    public sealed class PlaySetup
    {
        /// <summary>정화하는 유물(서버 RelicMaster.id). 초상화 같은 유물별 그림을 고를 때 쓴다.</summary>
        public int RelicId { get; }
        public Phase Phase { get; }
        public MinigameDifficulty Difficulty { get; }
        public SealMedium SealMedium { get; }

        /// <summary>보드 시드. 같은 설정·같은 시드면 같은 보드가 나온다.</summary>
        public long Seed { get; }

        public PlaySetup(int relicId, Phase phase, MinigameDifficulty difficulty, SealMedium sealMedium, long seed)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            if (!Enum.IsDefined(typeof(Phase), phase))
                throw new ArgumentException($"Phase {phase}는 정의되지 않았습니다.", nameof(phase));
            if (!Enum.IsDefined(typeof(MinigameDifficulty), difficulty))
                throw new ArgumentException($"MinigameDifficulty {difficulty}는 정의되지 않았습니다.", nameof(difficulty));
            if (!Enum.IsDefined(typeof(SealMedium), sealMedium))
                throw new ArgumentException($"SealMedium {sealMedium}은 정의되지 않았습니다.", nameof(sealMedium));
            
            RelicId = relicId;
            Phase = phase;
            Difficulty = difficulty;
            SealMedium = sealMedium;
            Seed = seed;
        }
    }
}