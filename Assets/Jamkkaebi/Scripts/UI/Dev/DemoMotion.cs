using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Dev
{
    // 2026-10-29 기준 미사용: 홈·도감·친구 탭의 시제품 더미 화면이 전시관·도감·친구 화면으로 교체되면서
    // 어떤 씬·프리팹에도 붙어 있지 않다. 코드에서도 참조하는 곳이 없다.
    // 시제품 흐름을 참고하기 위해 Dev/로 옮겨 보관한다.
    public class DemoMotion : MonoBehaviour
    {
        public float elapsed;
        private Vector3 origin;
        private void Awake() => origin = transform.localPosition;
        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.localPosition = origin + new Vector3(Mathf.Sin(elapsed * 1.2f) * 1.2f, Mathf.Cos(elapsed * 0.8f) * 0.2f, 0);
            transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(elapsed) * 12);
        }
    }
}
