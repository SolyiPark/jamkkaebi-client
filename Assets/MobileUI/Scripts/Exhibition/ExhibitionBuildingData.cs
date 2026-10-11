using UnityEngine;

namespace MobilePrototype.Exhibition
{
    /// <summary>ServerId는 보유 건물의 buildingId. 로컬 배치 번호를 서버 ID로 사용하지 않는다.</summary>
    public sealed class ExhibitionBuildingData : ExhibitionObjectData
    {
        public override string Type => "BUILDING";
        /// <summary>Stores a building using its owned building ID, logical cell, unrotated footprint in cells, and rotation in 90-degree steps.</summary>
        public ExhibitionBuildingData(string localId, int buildingId, bool placed, Vector2Int cell,
            Vector2Int footprint, int quarterTurns = 0)
            : base(localId, buildingId, placed, cell, footprint, quarterTurns) { }
    }
}
