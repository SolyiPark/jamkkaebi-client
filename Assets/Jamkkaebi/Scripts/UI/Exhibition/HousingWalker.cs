using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 격자 위를 걸어 다니는 객체 하나의 논리 상태. 지금 걷는 길(From → To)을 갖는다.
    /// 값은 <see cref="HousingLayout"/>만 바꾼다.
    /// </summary>
    public sealed class HousingWalker
    {
        /// <summary>배치 인스턴스 ID. 전시물의 배치 ID와 같은 번호 공간을 쓴다.</summary>
        public int Id { get; }

        /// <summary>
        /// 저장 기준 자리. 마지막으로 도착한 칸이며 걸어 다니면 따라 바뀐다.
        /// 칸 사이를 걷는 동안에는 떠나온 칸이고, 다음 칸에 도착하면 그 칸이 된다.
        /// </summary>
        public Vector2Int Anchor => From;

        public Vector2Int From { get; internal set; }
        public Vector2Int To { get; internal set; }

        public HousingWalker(int id, Vector2Int cell)
        { Id = id; From = To = cell; }

        /// <summary>지금 서 있거나 걸어 들어가는 칸인지.</summary>
        public bool Reserves(Vector2Int cell) => From == cell || To == cell;
    }
}
