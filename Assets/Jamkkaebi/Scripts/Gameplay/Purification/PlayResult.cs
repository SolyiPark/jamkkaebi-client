namespace Jamkkaebi.Scripts.Gameplay.Purification
{
    /// <summary>
    /// 한 판의 정산용 결과. 서버 PlayResult와 같다.
    /// </summary>
    /// <remarks>미니게임 SessionState는 판 진행 중의 보드 상태이고, 판이 끝나면 정화 실행기가 이 값으로 바꿔 보고한다.</remarks>
    public enum PlayResult
    {
        Success = 0,
        Failure = 1
    }
}