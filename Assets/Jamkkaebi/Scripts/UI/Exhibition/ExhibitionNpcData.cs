using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>ServerId는 보유한 완성 유물의 artifactId. Cell은 걷기 좌표가 아닌 배치 기준점.</summary>
    public sealed class ExhibitionNpcData : ExhibitionObjectData
    {
        public override string Type => "SPIRIT";
        /// <summary>보유 artifactId와 고정 앵커를 사용하는 불변 NPC 배치 정보를 생성합니다.</summary>
        public ExhibitionNpcData(string localId, int artifactId, bool placed, Vector2Int anchor)
            : base(localId, artifactId, placed, anchor, Vector2Int.one, 0) { }
    }
}
