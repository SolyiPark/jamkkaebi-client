using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 정화 페이즈 하나의 연출 값. 페이즈가 오를수록 봉인이 약해지므로 배경 연출은 옅어지고 배경은 밝아진다.
    /// </summary>
    /// <remarks>
    /// 매개체(<see cref="SealMediumData"/>)가 "무엇이 봉인하고 있나"를 정한다면, 페이즈는 "봉인이 얼마나 남았나"를 정한다.
    /// 두 축을 따로 두어 매개체 에셋을 페이즈마다 다시 만들지 않는다.
    /// </remarks>
    [CreateAssetMenu(fileName = "PhaseData", menuName = "Jamkkaebi/Phase Data")]
    public class PhaseData : ScriptableObject
    {
        [SerializeField, Range(1, 3)] private int _phase = 1;

        [Tooltip("배경 연출(안개·그을음·뿌리·녹)의 세기. 1이면 그대로, 낮을수록 판을 시작할 때부터 옅다.")]
        [SerializeField, Range(0f, 1f)] private float _backdropIntensity = 1f;

        [Tooltip("배경 이미지에 곱하는 색. 흰색이면 원래 밝기이고, 어두울수록 봉인이 짙은 느낌이 난다.")]
        [SerializeField] private Color _backgroundTint = Color.white;

        [Tooltip("배경을 원래보다 밝게 하는 정도. 곱하는 색으로는 원래보다 밝아질 수 없어, 옅은 색을 이만큼 덮는다.")]
        [SerializeField, Range(0f, 0.5f)] private float _backgroundLift;

        public int Phase => _phase;
        public float BackdropIntensity => _backdropIntensity;
        public Color BackgroundTint => _backgroundTint;
        public float BackgroundLift => _backgroundLift;
    }
}
