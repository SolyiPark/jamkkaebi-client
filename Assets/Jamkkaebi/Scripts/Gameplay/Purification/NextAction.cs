namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>정화 결과 화면의 주 버튼이 할 일. 서버 NextAction과 같다.</summary>
    public enum NextAction
    {
        NextPhase = 0,       // 최초 정화의 다음 페이즈로
        ContinueRestore = 1, // 관리 플레이의 복구 순서가 남음
        Done = 2,            // 이번 정화로 끝
        Retry = 3            // 실패: 같은 페이즈를 다시 도전
    }
}