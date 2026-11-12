using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    [RequireComponent(typeof(ExhibitionIntro))]
    public sealed class ExhibitionView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private ExhibitionCameraController _cameraController;
        [SerializeField] private ExhibitionBehaviorPolicy _policy;
        [SerializeField] private float _zoom = 1;
        [SerializeField] private Vector2 _center;
        private ExhibitionIntro _intro;
        private ExhibitionLayer[] _layers;
        public float TransitionOffset { get; private set; }
        public ExhibitionIntro Intro => _intro;
        public float Zoom => _zoom;
        /// <summary>
        /// 등장 연출과 비활성 자식을 포함한 배경 레이어를 캐시합니다.
        /// </summary>
        private void Awake()
        {
            _intro = GetComponent<ExhibitionIntro>();
            _layers = GetComponentsInChildren<ExhibitionLayer>(true);
            if (!_cameraController) _cameraController = GetComponent<ExhibitionCameraController>();
            if (!_cameraController) _cameraController = gameObject.AddComponent<ExhibitionCameraController>();
            _cameraController.Configure(_camera);
            _cameraController.SetView(_center, _zoom);
        }
        /// <summary>
        /// 홈이 활성 탭이 되었음을 등장 연출에 알립니다. 반복 방문의 재생 여부는 세션 정책에 맡깁니다.
        /// </summary>
        public void NotifyVisible()
        {
            if (_policy && !_policy.PlayIntroduction) _intro.Complete();
            else _intro.Begin();
        }
        /// <summary>
        /// 시차에 사용할 전환 오프셋을 저장하고, 이동이 시작되면 진행 중인 등장 연출을 완료 상태로 정리합니다.
        /// </summary>
        public void SetTransitionOffset(float offset)
        {
            TransitionOffset = offset;
            if (Mathf.Abs(offset) > .0001f) _intro.Complete();
        }
        // Future pinch input calls this; grid data stays in platform local space.
        /// <summary>
        /// 카메라 로컬 중심과 0.75~2로 제한한 줌 배율을 설정합니다. 플랫폼 로컬 격자 데이터는 변경하지 않습니다.
        /// </summary>
        public void SetView(Vector2 center, float zoom)
        {
            _center = center; _zoom = Mathf.Clamp(zoom, .75f, 2);
            if (_cameraController) _cameraController.SetView(_center, _zoom);
        }
        /// <summary>
        /// 화면 비율과 줌에 맞춰 카메라를 배치한 뒤 각 레이어에 등장 시간과 시차 오프셋을 함께 적용합니다.
        /// </summary>
        private void LateUpdate()
        {
            _cameraController.Apply();
            float offset = !_policy || _policy.EnableTabParallax ? TransitionOffset : 0;
            foreach (var layer in _layers) layer.Apply(_intro.Elapsed, offset, _camera);
        }
    }
}
