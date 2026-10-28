namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 정화 미니게임의 난이도. 서버 Difficulty(VERY_EASY, EASY, NORMAL, HARD)와 순서·의미가 같다.
    /// </summary>
    /// <remarks>
    /// Unity는 enum을 정수로 씬과 에셋에 저장하므로 값을 고정한다. 새 난이도는 끝에 새 값으로 추가하고, 기존 값은 바꾸지 않는다.
    /// </remarks>
    public enum MinigameDifficulty
    {
        VeryEasy = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3
    }
}
