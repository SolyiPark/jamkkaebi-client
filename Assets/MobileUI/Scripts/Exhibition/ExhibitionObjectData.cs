using UnityEngine;

namespace MobilePrototype.Exhibition
{
    /// <summary>로컬 표현과 서버 소유 객체의 식별자를 분리한 불변 배치 데이터.</summary>
    public abstract class ExhibitionObjectData
    {
        public string LocalId { get; }
        public int ServerId { get; }
        public abstract string Type { get; }
        public bool Placed { get; }
        public Vector2Int Cell { get; }
        public Vector2Int Footprint { get; }
        public int QuarterTurns { get; }
        public string Key => Type + ":" + ServerId;

        protected ExhibitionObjectData(string localId, int serverId, bool placed, Vector2Int cell,
            Vector2Int footprint, int quarterTurns)
        {
            LocalId = localId; ServerId = serverId; Placed = placed; Cell = cell;
            Footprint = footprint; QuarterTurns = quarterTurns;
        }
    }
}
