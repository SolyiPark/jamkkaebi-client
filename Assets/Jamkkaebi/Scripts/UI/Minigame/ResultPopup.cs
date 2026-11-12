using System;
using Jamkkaebi.Scripts.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 한 판이 끝났을 때 띄우는 결과 창. 성공·실패에 따라 패널 그림, 내용, 버튼을 바꾼다.
    /// </summary>
    /// <remarks>
    /// <see cref="UILoader"/>가 프리팹에서 만든다.
    /// - 성공: 결과 수치 + [다음 단계](다음 페이즈가 있을 때) + [나가기]
    /// - 실패: 안내 문구와 떠다니는 위협 아이콘 + [다시 하기] + [나가기]
    /// 버튼을 눌러도 직접 판을 바꾸지 않고 이벤트로 알리기만 한다. 어떻게 처리할지는 받는 쪽이 정한다.
    /// </remarks>
    public class ResultPopup : UIPopup
    {
        [Header("패널")]
        [SerializeField] private Image _panelImage;
        [SerializeField] private Sprite _successPanelSprite;
        [SerializeField] private Sprite _failPanelSprite;

        [Header("결과별 내용")]
        [Tooltip("성공했을 때만 켜는 묶음. 제목, 수치, 정성 그림.")]
        [SerializeField] private GameObject _successContent;
        [Tooltip("실패했을 때만 켜는 묶음. 제목, 안내 문구, 위협 아이콘.")]
        [SerializeField] private GameObject _failContent;
        [Tooltip("성공했을 때만 켜는 연출. 패널 앞 반짝임처럼 패널 밖에 따로 둔 오브젝트.")]
        [SerializeField] private GameObject[] _successEffects = Array.Empty<GameObject>();

        [Header("성공 수치")]
        [SerializeField] private TMP_Text _damageText;
        [SerializeField] private TMP_Text _bonusText;
        [SerializeField] private Color _damageValueColor = new Color32(171, 77, 80, 255);
        [SerializeField] private Color _bonusValueColor = new Color32(205, 139, 0, 255);

        [Header("버튼")]
        [Tooltip("성공했고 다음 페이즈가 있을 때만 보인다.")]
        [SerializeField] private Button _nextButton;
        [Tooltip("실패했을 때만 보인다.")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _exitButton;

        /// <summary>다음 단계를 눌렀을 때.</summary>
        public event Action NextRequested;
        /// <summary>다시 하기를 눌렀을 때.</summary>
        public event Action RetryRequested;
        /// <summary>나가기를 눌렀을 때.</summary>
        public event Action ExitRequested;

        private void Awake()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(OnNextClicked);
            if (_retryButton != null) _retryButton.onClick.AddListener(OnRetryClicked);
            if (_exitButton != null) _exitButton.onClick.AddListener(OnExitClicked);
        }

        private void OnDestroy()
        {
            if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNextClicked);
            if (_retryButton != null) _retryButton.onClick.RemoveListener(OnRetryClicked);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(OnExitClicked);
        }

        /// <summary>결과를 창에 채운다. 열기 전에 부른다.</summary>
        public void Bind(ResultPopupData data)
        {
            bool succeeded = data.Succeeded;

            if (_panelImage != null)
            {
                Sprite panel = succeeded ? _successPanelSprite : _failPanelSprite;
                if (panel != null) _panelImage.sprite = panel;
            }
            if (_successContent != null) _successContent.SetActive(succeeded);
            if (_failContent != null) _failContent.SetActive(!succeeded);
            foreach (GameObject effect in _successEffects)
            {
                if (effect != null) effect.SetActive(succeeded);
            }

            // 숫자만 색을 바꾸려고 TMP 리치 텍스트의 <color> 태그를 쓴다
            if (_damageText != null)
                _damageText.text = $"손상도 {Colorize($"{Mathf.RoundToInt(data.Damage * 100f)}%", _damageValueColor)}";
            if (_bonusText != null)
                _bonusText.text = $"획득한 정성 {Colorize($"{data.BonusCount}개", _bonusValueColor)}";

            if (_nextButton != null) _nextButton.gameObject.SetActive(succeeded && data.HasNextPhase);
            if (_retryButton != null) _retryButton.gameObject.SetActive(!succeeded);
        }

        private static string Colorize(string text, Color color) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

        private void OnNextClicked() => NextRequested?.Invoke();
        private void OnRetryClicked() => RetryRequested?.Invoke();
        private void OnExitClicked() => ExitRequested?.Invoke();
    }
}
