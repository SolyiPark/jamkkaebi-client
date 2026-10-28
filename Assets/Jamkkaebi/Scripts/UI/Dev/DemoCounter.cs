using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Dev
{
    // 2026-10-29 기준 미사용: 홈·도감·친구 탭의 시제품 더미 화면이 전시관·도감·친구 화면으로 교체되면서
    // 어떤 씬·프리팹에도 붙어 있지 않다. IntegratedVerification이 탭에 이 컴포넌트가 남아 있지 않은지 검사할 때만 참조한다.
    // 시제품 흐름을 참고하기 위해 Dev/로 옮겨 보관한다.
    public class DemoCounter : MonoBehaviour
    {
        public Text label;
        public Button button;
        public int count;
        private void Awake() { button.onClick.AddListener(Increment); Refresh(); }
        public void Increment() { count++; Refresh(); }
        private void Refresh() => label.text = $"터치  {count}";
        private void OnDestroy() { if (button) button.onClick.RemoveListener(Increment); }
    }
}
