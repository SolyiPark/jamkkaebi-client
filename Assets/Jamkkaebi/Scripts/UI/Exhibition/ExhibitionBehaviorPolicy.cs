using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>화면별 기능 허용 설정입니다. 실행 중 선택·카메라·요청 상태는 보관하지 않습니다.</summary>
    [CreateAssetMenu(menuName = "Jamkkaebi/Exhibition/Behavior Policy")]
    public sealed class ExhibitionBehaviorPolicy : ScriptableObject
    {
        [SerializeField] private bool _allowObjectSelection = true;
        [SerializeField] private bool _enableSelectionFocus;
        [SerializeField] private bool _allowLayoutEditing = true;
        [SerializeField] private bool _playIntroduction = true;
        [SerializeField] private bool _enableTabParallax = true;
        [SerializeField] private bool _enableAmbientMovement = true;
        [SerializeField] private bool _lockNavigationDuringVisit;
        public bool AllowObjectSelection => _allowObjectSelection;
        public bool EnableSelectionFocus => _enableSelectionFocus;
        public bool AllowLayoutEditing => _allowLayoutEditing;
        public bool PlayIntroduction => _playIntroduction;
        public bool EnableTabParallax => _enableTabParallax;
        public bool EnableAmbientMovement => _enableAmbientMovement;
        public bool LockNavigationDuringVisit => _lockNavigationDuringVisit;
    }
}
