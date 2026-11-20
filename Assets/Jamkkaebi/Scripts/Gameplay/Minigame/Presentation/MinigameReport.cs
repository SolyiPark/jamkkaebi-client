using System;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Purification;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>미니게임 한 판을 서버 결과 보고(PlayReport)로 바꾼다. 순수 계산이라 EditMode 테스트 대상이다.</summary>
    public static class MinigameReport
    {
        /// <param name="session">끝난 세션. 포기할 때는 진행 중이어도 된다.</param>
        /// <param name="config">그 세션을 만든 보드 설정. 사용 횟수를 "예산 - 남은 횟수"로 구한다.</param>
        /// <param name="gaveUp">중도 포기했는지.</param>
        public static PlayReport From(MinigameSession session, MinigameBoardConfig config, bool gaveUp)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            SessionState state = session.State;
            if (state == SessionState.InProgress && !gaveUp)
                throw new ArgumentException("진행 중 상태에서 끝난 세션은 포기한 세션이어야 합니다.", nameof(gaveUp));

            PlayResult result;
            FailReason? reason;
            if (state == SessionState.Succeeded)
            {
                result = PlayResult.Success;
                reason = null;
            }
            else if (state == SessionState.Failed)
            {
                result = PlayResult.Failure;
                reason = session.DamageGauge >= 1.0f
                    ? FailReason.DamageFull
                    : FailReason.TapsExhausted;
            }
            else
            {
                result = PlayResult.Failure;
                reason = FailReason.GaveUp;
            }
           
            int tapsUsed = config.DestructiveToolLimit - session.RemainingDestructiveToolUses;
            int scoutsUsed = config.ScoutToolLimit - session.RemainingScoutToolUses;
            int bonusJeongseong = session.RevealedBonusTileCount;


            int damage = Mathf.FloorToInt((float)(session.DamageGauge * PlayReport.MaxDamage + 0.0001));
            
            return new PlayReport(result, reason, tapsUsed, scoutsUsed, damage, bonusJeongseong);
        }
    }
}