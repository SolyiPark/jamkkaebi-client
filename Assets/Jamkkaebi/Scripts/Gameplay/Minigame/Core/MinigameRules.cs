using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    /// <summary>
    /// 난이도와 무관하게 클라이언트가 고정해서 쓰는 미니게임 규칙.
    /// </summary>
    public static class MinigameRules
    {
        /// <summary>
        /// 목표 폴리오미노를 고를 때 쓰는 모양 풀. 서로 다른 모양만 들어 있다.
        /// </summary>
        /// <remarks>
        /// 1칸짜리 <see cref="PolyominoShape.Dot"/>은 목표 형상으로 쓰지 않으므로 뺐다.
        /// 풀의 순서가 바뀌면 같은 시드라도 고르는 세트가 달라진다.
        /// </remarks>
        public static readonly IReadOnlyList<PolyominoShape> ShapePool = new[]
        {
            PolyominoShape.Line2,
            PolyominoShape.LShape3,
            PolyominoShape.LShape4,
            PolyominoShape.Stairs4,
            PolyominoShape.Square4,
            PolyominoShape.Line5
        };
    }
}
