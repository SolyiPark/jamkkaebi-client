using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 정화 페이즈 하나의 연출 값(족자 제목, 배경 밝기, 배경 연출 세기).
    /// 페이즈가 오를수록 봉인이 약해지므로 배경 연출은 옅어지고 배경은 밝아진다.
    /// </summary>
    /// <remarks>
    /// 매개체(<see cref="SealMediumData"/>)가 "무엇이 봉인하고 있나"를 정한다면, 페이즈는 "봉인이 얼마나 남았나"를 정한다.
    /// 두 축을 따로 두어 매개체 에셋을 페이즈마다 다시 만들지 않는다.
    /// </remarks>
    [CreateAssetMenu(fileName = "PhaseData", menuName = "Jamkkaebi/Phase Data")]
    public class PhaseData : ScriptableObject
    {
        [SerializeField, Range(1, 3)] private int _phase = 1;

        [Tooltip("판이 시작될 때 족자에 띄우는 페이즈 제목 그림.")]
        [SerializeField] private Sprite _titleSprite;

        [Tooltip("배경 연출(안개·그을음·뿌리·녹)의 세기. 1이면 그대로, 낮을수록 판을 시작할 때부터 옅다.")]
        [SerializeField, Range(0f, 1f)] private float _backdropIntensity = 1f;

        [Tooltip("봉인된 배경에 곱하는 색. 흰색이면 원래 밝기이고, 어두울수록 봉인이 짙은 느낌이 난다.")]
        [SerializeField] private Color _backgroundTint = Color.white;

        [Tooltip("봉인이 풀린 정도. 이 비율만큼 깨어난(밝은) 배경이 섞이고, 배경 연출도 밝은 배경에 맞는 색으로 바뀐다.")]
        [SerializeField, Range(0f, 1f)] private float _awakening;

        public int Phase => _phase;
        public Sprite TitleSprite => _titleSprite;
        public float BackdropIntensity => _backdropIntensity;
        public Color BackgroundTint => _backgroundTint;
        public float Awakening => _awakening;
    }
}
