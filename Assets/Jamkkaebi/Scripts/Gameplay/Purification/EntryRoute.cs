namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>정화에 들어온 경로. 서버 EntryRoute(FIRST, CARE)와 같다.</summary>
    public enum EntryRoute
    {
        First = 0, // 최초 정화: 작업대에서 봉인 해제 → 기억 회복 → 오염 관리 순서 고정
        Care = 1   // 관리 플레이: 정령 컬렉션의 유물을 선명도가 정한 페이즈부터 다시 정화
    }
}