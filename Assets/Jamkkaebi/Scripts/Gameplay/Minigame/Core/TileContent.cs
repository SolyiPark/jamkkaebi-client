namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public enum TileContent
    {
        Empty,
        Threat,
        Helper,
        Target,
        /// <summary>정성 보너스 타일. 열면 정성을 추가로 얻는다. 형상 밖 일반 칸에 놓이며 강화될 수 있다.</summary>
        Bonus
    }
}