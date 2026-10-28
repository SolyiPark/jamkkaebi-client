using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>ServerId는 보유 건물의 buildingId. 로컬 배치 번호를 서버 ID로 사용하지 않는다.</summary>
    public sealed class ExhibitionBuildingData : ExhibitionObjectData
    {
        public override string Type => "BUILDING";
        /// <summary>로컬 인스턴스와 보유 buildingId를 분리한 불변 건물 배치 정보를 생성합니다.</summary>
        public ExhibitionBuildingData(string localId, int buildingId, bool placed, Vector2Int cell,
            Vector2Int footprint, int quarterTurns = 0)
            : base(localId, buildingId, placed, cell, footprint, quarterTurns) { }
    }
}
