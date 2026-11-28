using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 격자 위를 걸어 다니는 객체 하나의 논리 상태. 저장 기준 자리(Anchor)와 지금 걷는 길(From → To)을 따로 둔다.
    /// 값은 <see cref="HousingLayout"/>만 바꾼다.
    /// </summary>
    public sealed class HousingWalker
    {
        /// <summary>배치 인스턴스 ID. 전시물의 배치 ID와 같은 번호 공간을 쓴다.</summary>
        public int Id { get; }

        /// <summary>서버로 보내는 기준 자리. 걸어 다녀도 바뀌지 않는다.</summary>
        public Vector2Int Anchor { get; internal set; }

        public Vector2Int From { get; internal set; }
        public Vector2Int To { get; internal set; }

        public HousingWalker(int id, Vector2Int anchor)
        { Id = id; Anchor = From = To = anchor; }

        /// <summary>지금 서 있거나 걸어 들어가는 칸인지.</summary>
        public bool Reserves(Vector2Int cell) => From == cell || To == cell;
    }
}
