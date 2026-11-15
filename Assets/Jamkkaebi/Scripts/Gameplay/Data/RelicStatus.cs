namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 보유 유물 한 종의 상태. 서버 RelicStatus와 같다.
    /// 오염 관리(3페이즈)까지 마치면 Completed가 되어 작업대 슬롯에서 빠지고 정령 컬렉션으로 옮겨진다.
    /// </summary>
    /// <remarks>도감의 "복원 완료"는 Completed와 같은 뜻이다.</remarks>
    public enum RelicStatus
    {
        InProgress = 0, // 작업대에서 최초 정화 진행 중
        Completed = 1   // 오염 관리까지 완료
    }
}