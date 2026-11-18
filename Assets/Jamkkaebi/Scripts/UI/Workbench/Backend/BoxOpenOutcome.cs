using System;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>상자 하나를 연 결과. 서버 BoxOpenResponse.</summary>
    public sealed class BoxOpenOutcome
    {
        public AcquireResult Result { get; }
        public int RelicId { get; }

        /// <summary>중복일 때 받은 정성. 새 유물이면 0.</summary>
        public int JeongseongGained { get; }

        /// <summary>중복일 때 받은 시대 결정. 새 유물이면 0.</summary>
        public int CrystalGained { get; }

        /// <summary>구매로 쓴 정성. 무료·선물이면 0.</summary>
        public int JeongseongSpent { get; }

        public BoxOpenOutcome(AcquireResult result, int relicId,
            int jeongseongGained, int crystalGained, int jeongseongSpent)
        {
            if (!Enum.IsDefined(typeof(AcquireResult), result))
                throw new ArgumentException($"유물 획득 결과 {result}는 정의되지 않았습니다.", nameof(result));
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            if (jeongseongGained < 0) throw new ArgumentOutOfRangeException(nameof(jeongseongGained));
            if (crystalGained < 0) throw new ArgumentOutOfRangeException(nameof(crystalGained));
            if (jeongseongSpent < 0) throw new ArgumentOutOfRangeException(nameof(jeongseongSpent));

            if (result == AcquireResult.New)
            {
                if (jeongseongGained != 0)
                    throw new ArgumentException("신규 유물 획득 시에는 중복 환전 정성이 존재할 수 없습니다.", nameof(jeongseongGained));
                if (crystalGained != 0)
                    throw new ArgumentException("신규 유물 획득 시에는 중복 환전 결정이 존재할 수 없습니다.", nameof(crystalGained));
            }

            Result = result;
            RelicId = relicId;
            JeongseongGained = jeongseongGained;
            CrystalGained = crystalGained;
            JeongseongSpent = jeongseongSpent;
        }
    }
}