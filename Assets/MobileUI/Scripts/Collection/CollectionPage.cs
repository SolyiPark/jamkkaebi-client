namespace MobilePrototype.Collection
{
    /// <summary>하나의 도감 콘텐츠 씬 안에서 전환하는 내부 화면 종류입니다.</summary>
    public enum CollectionPage
    {
        /// <summary>전체 통계와 시대별 수집 카드를 표시하는 시대 목록입니다.</summary>
        Overview,
        /// <summary>선택 시대의 유물, 건물 해금 상태와 설명을 표시합니다.</summary>
        Era,
        /// <summary>복원 완료한 유물과 정령의 상세 기록을 표시합니다.</summary>
        Relic
    }
}
