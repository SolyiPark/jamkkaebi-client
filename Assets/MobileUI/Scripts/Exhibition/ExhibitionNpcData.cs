using UnityEngine;

namespace MobilePrototype.Exhibition
{
    /// <summary>ServerId는 보유한 완성 유물의 artifactId. Cell은 걷기 좌표가 아닌 배치 기준점.</summary>
    public sealed class ExhibitionNpcData : ExhibitionObjectData
    {
        public override string Type => "SPIRIT";
        public ExhibitionNpcData(string localId, int artifactId, bool placed, Vector2Int anchor)
            : base(localId, artifactId, placed, anchor, Vector2Int.one, 0) { }
    }
}
