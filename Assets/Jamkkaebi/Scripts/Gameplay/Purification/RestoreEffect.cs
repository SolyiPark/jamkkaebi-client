namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>관리 플레이 한 판을 성공했을 때의 복구 효과. 난이도가 정한다.</summary>
    public enum RestoreEffect
    {
        RefreshOnly = 0, // 선명할 때 재봉인까지 남은 시간만 다시 채움
        OneStep = 1, // 남은 순서에서 한 페이즈 복구
        Full = 2 // 한 판으로 선명까지 전체 복구
    }
}