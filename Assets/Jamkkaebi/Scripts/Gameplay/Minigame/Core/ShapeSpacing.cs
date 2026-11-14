namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    /// <summary>
    /// 목표 형상끼리 얼마나 떨어져 있어야 하는지 정하는 규칙
    /// </summary>
    public enum ShapeSpacing
    {
        /// <summary>
        /// 겹치지만 않으면 맞닿아도 된다.
        /// </summary>
        None,
        /// <summary>
        /// 상하좌우로 맞닿지 않는다. 대각선 접촉은 가능
        /// </summary>
        FourWay,
        /// <summary>
        /// 대각선까지 포함해 8방향 모두 맞닿지 않는다.
        /// </summary>
        EightWay
    }
}