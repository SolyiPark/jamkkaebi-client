using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype
{
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
