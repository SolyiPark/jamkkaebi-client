namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>유물 하나를 얻은 결과. 서버 BoxResult와 같다.</summary>
    public enum AcquireResult
    {
        New = 0,       // 새 유물. 작업대 슬롯에 올라간다
        Duplicate = 1  // 이미 가진 유물. 목록은 그대로이고 재화로 바뀐다
    }
}