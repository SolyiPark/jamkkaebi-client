namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 정령의 선명도. 저장하지 않고 마지막 정화 시각에서 계산한다.
    /// 서버 Clarity(CLEAR, FADED, RESEALED)와 같다.
    /// </summary>
    public enum Clarity
    {
        Clear = 0,   // 선명
        Faded = 1,   // 흐려짐
        Resealed = 2 // 재봉인
    }
}