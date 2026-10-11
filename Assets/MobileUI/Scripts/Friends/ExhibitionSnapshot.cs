using System.Collections.Generic;
using System.Linq;

namespace MobilePrototype.Friends
{
    /// <summary>조회 한 번으로 전달할 전시 패키지. 서버 DTO와 구분되는 모의 어댑터의 출력입니다.</summary>
    public sealed class ExhibitionSnapshot
    {
        public string OwnerCode { get; }
        public string CapturedAt { get; }
        public bool HasGrid { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<SnapshotPlacement> Placements { get; }
        public string GiftState { get; }
        public bool VisitRewardAvailable { get; }
        /// <summary>Copies the placement sequence into a read-only snapshot without validating its grid or placements.</summary>
        /// <param name="hasGrid">Whether the snapshot supplies a grid; the visit scene permits a missing grid only when there are no placements.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="placements"/> is null.</exception>
        public ExhibitionSnapshot(string code, IEnumerable<SnapshotPlacement> placements, bool hasGrid = true,
            int width = 7, int height = 7, string capturedAt = "2026-10-08 15:00", string giftState = "AVAILABLE", bool visitRewardAvailable = true)
        {
            OwnerCode = code; CapturedAt = capturedAt; HasGrid = hasGrid; Width = width; Height = height;
            GiftState = giftState; VisitRewardAvailable = visitRewardAvailable;
            Placements = System.Array.AsReadOnly(placements.ToArray());
        }
    }
}
