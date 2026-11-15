namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 유물 정화의 세 페이즈. 순서가 곧 진행도다.
    /// 서버 Phase(UNSEAL, RECALL, GUARD)와 순서 및 의미가 같다.
    /// </summary>
    /// <remarks>
    /// 미니게임 PhaseData의 정수 페이즈(1~3)와는 <see cref="PhaseExtensions.Order"/>로 대응한다.
    /// </remarks>
    public enum Phase
    {
        Unseal = 0, // 1페이즈 봉인 해제
        Recall = 1, // 2페이즈 기억 회복
        Guard = 2 // 3페이즈 오염 관리
    }
}