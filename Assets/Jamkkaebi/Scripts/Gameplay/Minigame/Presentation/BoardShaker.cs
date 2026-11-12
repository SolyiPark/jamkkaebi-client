using System.Collections;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 손상도가 오르면 보드를 잠깐 흔든다.
    /// </summary>
    /// <remarks>
    /// 카메라는 BoardCameraFitter가 매 프레임 위치를 정하므로 카메라 대신 보드 컨테이너를 흔든다.
    /// 흔들림 크기는 타일 한 칸 기준이라 보드 크기와 상관없이 같은 느낌이다. 힐링 톤에 맞게 약하게 둔다.
    /// </remarks>
    public class BoardShaker : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("흔들 대상. 타일이 들어 있는 보드 컨테이너.")]
        [SerializeField] private Transform _target;
        [Tooltip("최대 흔들림 폭(타일 한 칸 = 1).")]
        [SerializeField] [Min(0f)] private float _amplitude = 0.06f;
        [SerializeField] [Min(0.01f)] private float _duration = 0.18f;
        [Tooltip("1초에 몇 번 흔들리는지.")]
        [SerializeField] [Min(1f)] private float _frequency = 30f;

        private Vector3 _restPosition;
        private Coroutine _shakeRoutine;

        private void OnEnable()
        {
            if (_adapter != null) _adapter.DamageTaken += Shake;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.DamageTaken -= Shake;
            StopShake();
        }

        private void Shake(float damage)
        {
            if (_target == null) return;
            // 흔들리는 도중에 또 맞으면 처음 자리를 기준으로 다시 흔든다
            StopShake();
            _restPosition = _target.localPosition;
            _shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private void StopShake()
        {
            if (_shakeRoutine == null) return;
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
            if (_target != null) _target.localPosition = _restPosition;
        }

        private IEnumerator ShakeRoutine()
        {
            float amplitude = _amplitude * _adapter.TileSize;
            // 매번 다른 흔들림이 나오도록 노이즈를 읽을 출발점을 무작위로 정한다
            float seed = Random.value * 100f;

            for (float elapsed = 0f; elapsed < _duration; elapsed += Time.deltaTime)
            {
                float strength = 1f - elapsed / _duration;   // 점점 약해진다
                float time = seed + elapsed * _frequency;
                // PerlinNoise는 0~1을 돌려주므로 -1~1로 바꾼다. Random보다 덜 떨리고 부드럽게 흔들린다
                var offset = new Vector3(
                    Mathf.PerlinNoise(time, 0f) * 2f - 1f,
                    Mathf.PerlinNoise(0f, time) * 2f - 1f,
                    0f);
                _target.localPosition = _restPosition + offset * (amplitude * strength);
                yield return null;
            }

            _target.localPosition = _restPosition;
            _shakeRoutine = null;
        }
    }
}
