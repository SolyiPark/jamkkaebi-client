using System;
using System.Collections;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// 화면 위에 잠깐 띄우는 창의 공통 부모.
    /// </summary>
    /// <remarks>
    /// 창 프리팹의 뿌리 오브젝트에 이 클래스를 상속한 스크립트를 붙이고 <see cref="UILoader"/>에 등록한다.
    /// 열고 닫기는 오브젝트를 켜고 끄는 것으로 처리하고, 창마다 다른 일은 <see cref="OnOpened"/>·<see cref="OnClosed"/>를 재정의해서 한다.
    /// 열릴 때 <see cref="_popTarget"/>이 살짝 커졌다가 원래 크기로 돌아오는 팝 연출을 모든 창이 같이 쓴다.
    /// </remarks>
    public abstract class UIPopup : MonoBehaviour
    {
        [Header("열기 연출")]
        [Tooltip("열릴 때 커졌다 돌아오는 대상. 어두운 배경은 빼고 창 내용만 묶은 오브젝트를 넣는다. 비우면 연출 없이 바로 뜬다.")]
        [SerializeField] private RectTransform _popTarget;
        [Tooltip("연출 전체 시간(초).")]
        [SerializeField] [Min(0.01f)] private float _popDuration = 0.28f;
        [Tooltip("처음 나타날 때의 크기 배율.")]
        [SerializeField] [UnityEngine.Range(0.5f, 1f)] private float _popStartScale = 0.9f;
        [Tooltip("가장 커질 때의 크기 배율.")]
        [SerializeField] [UnityEngine.Range(1f, 1.3f)] private float _popPeakScale = 1.06f;

        private Coroutine _popRoutine;

        /// <summary>창이 열렸을 때.</summary>
        public event Action Opened;
        /// <summary>창이 닫혔을 때.</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>창을 켜고 다른 창보다 위에 그린다. 이미 열려 있으면 아무 일도 하지 않는다.</summary>
        public void Open()
        {
            if (IsOpen) return;
            transform.SetAsLastSibling();   // 같은 부모 아래에서는 뒤에 있는 형제가 위에 그려진다
            gameObject.SetActive(true);
            // 코루틴은 켜진 오브젝트에서만 시작할 수 있으므로 SetActive(true) 다음에 부른다
            if (_popTarget != null) _popRoutine = StartCoroutine(PlayPop());
            OnOpened();
            Opened?.Invoke();
        }

        /// <summary>창을 끈다. 이미 닫혀 있으면 아무 일도 하지 않는다.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            // 오브젝트를 끄면 코루틴도 멈추므로, 연출 도중에 닫혀도 다음에 열 때 크기가 어긋나지 않게 되돌려 둔다
            if (_popRoutine != null) StopCoroutine(_popRoutine);
            _popRoutine = null;
            if (_popTarget != null) _popTarget.localScale = Vector3.one;

            gameObject.SetActive(false);
            OnClosed();
            Closed?.Invoke();
        }

        /// <summary>창이 켜진 직후에 호출된다.</summary>
        protected virtual void OnOpened() { }

        /// <summary>창이 꺼진 직후에 호출된다.</summary>
        protected virtual void OnClosed() { }

        /// <summary>시작 크기 → 최대 크기 → 1배 순서로 크기를 바꾼다. 앞 60%는 커지고 뒤 40%는 돌아온다.</summary>
        private IEnumerator PlayPop()
        {
            const float growPart = 0.6f;
            float elapsed = 0f;
            while (elapsed < _popDuration)
            {
                float t = elapsed / _popDuration;
                float scale = t < growPart
                    ? Mathf.Lerp(_popStartScale, _popPeakScale, EaseOut(t / growPart))
                    : Mathf.Lerp(_popPeakScale, 1f, Mathf.SmoothStep(0f, 1f, (t - growPart) / (1f - growPart)));
                _popTarget.localScale = new Vector3(scale, scale, 1f);

                yield return null;   // 다음 프레임까지 기다린다
                // 게임이 멈춰 있어도(timeScale 0) 창은 움직이도록 unscaledDeltaTime을 쓴다
                elapsed += Time.unscaledDeltaTime;
            }
            _popTarget.localScale = Vector3.one;
            _popRoutine = null;
        }

        // 처음엔 빠르고 끝에서 느려진다
        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
