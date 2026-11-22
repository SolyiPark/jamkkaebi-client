using System;

namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>미니게임 한 판의 결과 보고. 서버 PurificationResultRequest와 같은 내용이다.</summary>
    public sealed class PlayReport
    {
        /// <summary>손상도 최댓값(%). 서버는 0~100 정수로 받는다.</summary>
        public const int MaxDamage = 100;

        public PlayResult Result { get; }

        /// <summary>실패한 이유. 성공이면 null.</summary>
        public FailReason? FailReason { get; }

        /// <summary>파괴 도구(안전형·공격형)를 쓴 횟수.</summary>
        public int TapsUsed { get; }

        public int ScoutsUsed { get; }

        /// <summary>손상도(%). 0이 멀쩡함, 100이 다 상함.</summary>
        public int Damage { get; }

        /// <summary>캐낸 정성 보너스 타일 수.</summary>
        public int BonusJeongseong { get; }

        public PlayReport(PlayResult result, FailReason? failReason, int tapsUsed, int scoutsUsed,
            int damage, int bonusJeongseong)
        {
            if (!Enum.IsDefined(typeof(PlayResult), result))
                throw new ArgumentException($"PlayResult {result}는 정의되지 않았습니다.", nameof(result));
            if ((result == PlayResult.Failure) ^ (failReason.HasValue))
                throw new ArgumentException("실패한 플레이에만 실패 사유가 있어야 합니다.", nameof(failReason));
            if (failReason.HasValue && !Enum.IsDefined(typeof(FailReason), failReason.Value))
                throw new ArgumentException($"FailReason {failReason}은 정의되지 않았습니다.", nameof(failReason));
            if (tapsUsed < 0) throw new ArgumentOutOfRangeException(nameof(tapsUsed), "도구 사용 횟수는 0회 이상이어야 합니다.");
            if (scoutsUsed < 0) throw new ArgumentOutOfRangeException(nameof(scoutsUsed), "정찰 사용 횟수는 0회 이상이어야 합니다.");
            if (damage < 0 || damage > MaxDamage) throw new ArgumentOutOfRangeException(nameof(damage), $"손상도는 0~{MaxDamage} 범위여야 합니다.");
            if (bonusJeongseong < 0) throw new ArgumentOutOfRangeException(nameof(bonusJeongseong), "획득한 보너스 정성 개수는 0개 이상이어야 합니다.");
            
            Result = result;
            FailReason = failReason;
            TapsUsed = tapsUsed;
            ScoutsUsed = scoutsUsed;
            Damage = damage;
            BonusJeongseong = bonusJeongseong;
        }

        /// <summary>
        /// 같은 판을 같은 내용으로 다시 보고했는지. 정산한 판에 다시 보고가 오면
        /// 같으면 처음 정산을 돌려주고, 다르면 충돌로 거부한다(서버 PlaySession.matches).
        /// </summary>
        public bool SameAs(PlayReport other)
        {
            if (other == null) return false;

            return (other.Result == Result && other.FailReason == FailReason && other.TapsUsed == TapsUsed &&
                    other.ScoutsUsed == ScoutsUsed && other.Damage == Damage &&
                    other.BonusJeongseong == BonusJeongseong);
        }
    }
}