using UnityEngine;

namespace MobilePrototype
{
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
