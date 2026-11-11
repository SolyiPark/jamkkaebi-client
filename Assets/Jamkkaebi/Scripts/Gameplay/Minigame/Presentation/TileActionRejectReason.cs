namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 누른 칸에서 도구를 쓰지 못한 이유. 화면에 안내 문구를 띄우는 데 쓴다.
    /// </summary>
    public enum TileActionRejectReason
    {
        /// <summary>도구가 닿는 칸이 모두 이미 열려 있다.</summary>
        AlreadyRevealed,
        /// <summary>선택한 도구의 남은 횟수가 없다.</summary>
        NoUsesRemaining
    }
}
