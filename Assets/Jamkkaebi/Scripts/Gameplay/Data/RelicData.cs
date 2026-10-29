using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 유물 한 종류의 도메인 정보.
    /// </summary>
    /// <remarks>
    /// 미니게임 보드 수치는 난이도가 정하므로 여기에 두지 않는다(서버 보드 파라미터 또는 <see cref="MinigameDifficultyTable"/>).
    /// 필드는 서버 relic API와 이름·ID를 맞춰 추가한다. 이름·이야기 같은 문구는 서버에서 받으므로 두지 않는다.
    /// 에셋 이름은 <c>Relic{relicId:00}_{영문명}</c>으로 짓는다(예: Relic02_IncenseBurner).
    /// </remarks>
    [CreateAssetMenu(fileName = "RelicData", menuName = "Jamkkaebi/Relic Data")]
    public class RelicData : ScriptableObject
    {
        [Tooltip("서버 RelicMaster.id와 같은 고정 번호(1~9). 재시딩해도 바뀌지 않는다.")]
        [SerializeField] [Min(1)] private int _relicId;
        [Tooltip("이 유물의 봉인 매개체. 보드 배경·타일·폴리오미노 그림을 정한다.")]
        [SerializeField] private SealMediumData _sealMedium;
        [Tooltip("이 유물의 도깨비 그림. 미니게임 손상도 바 위 등 UI에 쓴다.")]
        [SerializeField] private Sprite _spiritPortrait;

        public int RelicId => _relicId;
        public SealMediumData SealMedium => _sealMedium;
        public Sprite SpiritPortrait => _spiritPortrait;
    }
}
