using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// 판이 시작될 때 이번 페이즈를 알리는 족자(두루마리) 연출.
    /// </summary>
    /// <remarks>
    /// 순서: 배경 어둡게 → 가운데에 말린 족자 두 축이 양옆으로 벌어지며 족자가 펼쳐짐 → 제목이 톡 찍히듯 나타남
    /// → 잠시 보여 줌(아래에 "화면을 터치하면 넘어가요" 안내가 깜빡임) → 축이 다시 가운데로 모이며 말림 → 배경 밝게.
    /// 펼쳐지는 모습은 RectMask2D(<see cref="_reveal"/>)의 폭을 0에서 족자 폭까지 늘려 만든다.
    /// 마스크 안의 족자 그림은 가운데에 고정되어 있으므로 가운데부터 양옆으로 드러난다.
    /// 화면을 누르면 보여 주는 시간을 건너뛰고 바로 닫는다.
    /// </remarks>
    public class PhaseIntroPanel : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _dim;
        [Tooltip("폭을 늘려 족자를 드러내는 마스크(RectMask2D). 족자 그림과 제목을 자식으로 둔다.")]
        [SerializeField] private RectTransform _reveal;
        [SerializeField] private RectTransform _leftRod;
        [SerializeField] private RectTransform _rightRod;
        [SerializeField] private Image _title;
        [Tooltip("선택. 족자가 다 펼쳐진 동안 깜빡이는 건너뛰기 안내 글자.")]
        [SerializeField] private TMP_Text _skipHint;

        // using System이 있어 Range만 쓰면 System.Range(C# 범위 타입)와 헷갈리므로 UnityEngine을 붙인다
        [Header("모양")]
        [SerializeField] [Min(1f)] private float _panelWidth = 1080f;
        [Tooltip("배경을 덮는 검정의 진하기. 흰 안내 글자가 읽히도록 절반 정도로 둔다.")]
        [SerializeField] [UnityEngine.Range(0f, 1f)] private float _dimAlpha = 0.5f;

        [Header("시간(초)")]
        [SerializeField] [Min(0.01f)] private float _fadeDuration = 0.2f;
        [SerializeField] [Min(0.01f)] private float _openDuration = 0.55f;
        [Tooltip("족자가 이만큼(0~1) 펼쳐졌을 때 제목이 나타나기 시작한다.")]
        [SerializeField] [UnityEngine.Range(0f, 1f)] private float _titleStart = 0.6f;
        [SerializeField] [Min(0.01f)] private float _titleDuration = 0.3f;
        [Tooltip("족자를 펼친 채로 보여 주는 시간. 안내 글자를 읽을 수 있도록 넉넉히 둔다.")]
        [SerializeField] [Min(0f)] private float _holdDuration = 1.8f;
        [Tooltip("안내 글자가 한 번 흐려졌다 또렷해지는 데 걸리는 시간.")]
        [SerializeField] [Min(0.1f)] private float _hintPulsePeriod = 1.2f;
        [SerializeField] [Min(0.01f)] private float _closeDuration = 0.35f;

        private bool _skipRequested;

        /// <summary>연출이 끝나고 창이 닫혔을 때.</summary>
        public event Action Finished;

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// 족자 연출을 처음부터 재생한다.
        /// </summary>
        /// <param name="titleSprite">족자에 띄울 제목 그림. null이면 지금 걸려 있는 그림을 그대로 쓴다.</param>
        /// <remarks>
        /// 어떤 페이즈에 어떤 제목을 쓸지는 이 패널이 아니라 페이즈 데이터가 정한다.
        /// 패널은 받은 그림을 펼쳐 보여 주기만 하므로 페이즈가 아닌 다른 안내에도 쓸 수 있다.
        /// </remarks>
        public void Play(Sprite titleSprite)
        {
            if (titleSprite != null && _title != null)
            {
                _title.sprite = titleSprite;
            }

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(PlayRoutine());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _skipRequested = true;
        }

        private void OnDisable()
        {
            // 연출 도중 꺼지면 코루틴이 멈춘다. 입력을 막고 있던 쪽이 풀 수 있도록 끝난 것으로 알린다
            if (!IsPlaying) return;
            IsPlaying = false;
            Finished?.Invoke();
        }

        private IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            _skipRequested = false;
            SetOpenAmount(0f);
            SetTitle(0f);
            SetDim(0f);
            SetHintAlpha(0f);

            yield return Animate(_fadeDuration, SetDim);

            // 펼치기: 끝에서 살짝 더 벌어졌다가 제자리로 돌아와(EaseOutBack) 족자가 툭 펼쳐지는 느낌을 준다
            bool titleStarted = false;
            float titleElapsed = 0f;
            for (float elapsed = 0f; elapsed < _openDuration || titleElapsed < _titleDuration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / _openDuration);
                SetOpenAmount(EaseOutBack(t));

                if (!titleStarted && t >= _titleStart) titleStarted = true;
                if (titleStarted)
                {
                    SetTitle(Mathf.Clamp01(titleElapsed / _titleDuration));
                    titleElapsed += Time.unscaledDeltaTime;
                }
                yield return null;
            }
            SetOpenAmount(1f);
            SetTitle(1f);

            // 보여 주기. 화면을 누르면 바로 넘어간다
            _skipRequested = false;
            for (float elapsed = 0f; elapsed < _holdDuration && !_skipRequested; elapsed += Time.unscaledDeltaTime)
            {
                SetHintAlpha(GetHintAlpha(elapsed));
                yield return null;
            }
            SetHintAlpha(0f);

            // 말기: 처음엔 천천히, 끝으로 갈수록 빠르게 가운데로 모인다
            yield return Animate(_closeDuration, t => SetOpenAmount(1f - t * t * t));
            yield return Animate(_fadeDuration, t => SetDim(1f - t));

            IsPlaying = false;
            gameObject.SetActive(false);
            Finished?.Invoke();
        }

        /// <summary>duration 동안 0→1로 가는 값을 매 프레임 apply에 넘긴다.</summary>
        private static IEnumerator Animate(float duration, Action<float> apply)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                apply(elapsed / duration);
                yield return null;
            }
            apply(1f);
        }

        /// <summary>
        /// 족자가 펼쳐진 정도(0 = 말려서 가운데, 1 = 다 펼침)에 맞춰 마스크 폭과 두 축 위치를 정한다.
        /// </summary>
        /// <remarks>1을 조금 넘는 값도 받는다(펼칠 때 살짝 더 벌어지는 연출).</remarks>
        private void SetOpenAmount(float amount)
        {
            float width = Mathf.Max(0f, _panelWidth * amount);
            if (_reveal != null) _reveal.sizeDelta = new Vector2(width, _reveal.sizeDelta.y);
            // 왼쪽 축은 오른쪽 끝이, 오른쪽 축은 왼쪽 끝이 펼쳐진 족자의 가장자리에 붙는다(기준점을 그 끝에 둠)
            if (_leftRod != null) _leftRod.anchoredPosition = new Vector2(-width * 0.5f, _leftRod.anchoredPosition.y);
            if (_rightRod != null) _rightRod.anchoredPosition = new Vector2(width * 0.5f, _rightRod.anchoredPosition.y);
        }

        /// <summary>제목이 투명하고 크게 시작해 또렷해지며 제 크기로 찍히듯 줄어든다.</summary>
        private void SetTitle(float t)
        {
            if (_title == null) return;
            Color color = _title.color;
            color.a = Mathf.Clamp01(t * 2f);   // 앞쪽 절반 동안 나타난다
            _title.color = color;
            float scale = Mathf.LerpUnclamped(1.3f, 1f, EaseOutBack(t));
            _title.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 안내 글자의 투명도. 처음 0.2초 동안 나타난 뒤 0.4~1 사이를 천천히 오가며 "누를 수 있다"는 신호를 준다.
        /// </summary>
        private float GetHintAlpha(float elapsed)
        {
            float appear = Mathf.Clamp01(elapsed / 0.2f);
            // 코사인은 1에서 시작하므로 또렷한 상태에서 흐려지기 시작한다
            float pulse = Mathf.Lerp(0.4f, 1f, 0.5f + 0.5f * Mathf.Cos(elapsed * Mathf.PI * 2f / _hintPulsePeriod));
            return appear * pulse;
        }

        private void SetHintAlpha(float alpha)
        {
            if (_skipHint == null) return;
            Color color = _skipHint.color;
            color.a = alpha;
            _skipHint.color = color;
        }

        private void SetDim(float t)
        {
            if (_dim == null) return;
            _dim.color = new Color(0f, 0f, 0f, _dimAlpha * t);
        }

        // 끝에서 목표를 살짝 넘었다가 돌아오는 곡선. overshoot가 클수록 많이 넘는다
        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.2f;
            float u = t - 1f;
            return 1f + u * u * ((overshoot + 1f) * u + overshoot);
        }
    }
}
