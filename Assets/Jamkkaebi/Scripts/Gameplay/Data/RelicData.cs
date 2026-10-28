using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 유물 한 종류의 도메인 정보.
    /// </summary>
    /// <remarks>
    /// 미니게임 보드 수치는 난이도가 정하므로 여기에 두지 않는다(서버 보드 파라미터 또는 <see cref="MinigameDifficultyTable"/>).
    /// 필드는 서버 relic API와 이름·ID를 맞춰 추가한다.
    /// </remarks>
    [CreateAssetMenu(fileName = "RelicData", menuName = "Jamkkaebi/Relic Data")]
    public class RelicData : ScriptableObject
    {
    }
}
