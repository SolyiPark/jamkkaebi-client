namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 결과 창에 보여 줄 한 판의 결과.
    /// </summary>
    /// <remarks>
    /// 창이 세션을 직접 읽지 않고 이 값만 받으므로, 창은 미니게임 규칙을 몰라도 된다.
    /// readonly struct라 만든 뒤에는 값이 바뀌지 않는다.
    /// </remarks>
    public readonly struct ResultPopupData
    {
        public bool Succeeded { get; }
        /// <summary>손상도. 0이 멀쩡함, 1이 다 상함.</summary>
        public float Damage { get; }
        /// <summary>찾은 정성 타일 수.</summary>
        public int BonusCount { get; }
        /// <summary>이어서 할 다음 페이즈가 있는지. 마지막 페이즈면 false.</summary>
        public bool HasNextPhase { get; }

        public ResultPopupData(bool succeeded, float damage, int bonusCount, bool hasNextPhase)
        {
            Succeeded = succeeded;
            Damage = damage;
            BonusCount = bonusCount;
            HasNextPhase = hasNextPhase;
        }
    }
}
