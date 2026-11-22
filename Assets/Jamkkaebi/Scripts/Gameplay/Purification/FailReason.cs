namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>판이 실패한 이유. 서버 FailReason과 순서·의미가 같다.</summary>
    public enum FailReason
    {
        DamageFull = 0,    // 손상 게이지 100%
        TapsExhausted = 1, // 도구(탭) 예산 소진
        GaveUp = 2         // 중도 포기
    }
}