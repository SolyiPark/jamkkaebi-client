using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Minigame
{
    /// <summary>
    /// 미니게임 화면의 HUD를 세션 상태에 맞춰 갱신하고, 버튼 입력을 어댑터에 전달한다.
    /// </summary>
    /// <remarks>
    /// 상태는 매 프레임 세션에서 직접 읽는다. 도움 타일 연쇄처럼 한 번의 입력으로 여러 값이 바뀌어도
    /// 이벤트를 하나하나 구독하지 않고 화면이 항상 세션과 같아진다.
    /// </remarks>
    public class MinigameHudPresenter : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;

        [Header("도구 남은 횟수")]
        [SerializeField] private TMP_Text _destructiveCountText;
        [SerializeField] private TMP_Text _scoutCountText;

        [Header("손상도")]
        [Tooltip("게이지 배경. 채움 폭의 기준이 된다.")]
        [SerializeField] private RectTransform _damageGaugeBackground;
        [Tooltip("왼쪽 기준으로 폭이 늘어나는 채움 막대.")]
        [SerializeField] private RectTransform _damageGaugeFill;
        [SerializeField] private TMP_Text _damageLabelText;
        [SerializeField] private TMP_Text _damagePercentText;
        [Tooltip("선택. 게이지 끝을 따라 움직이는 유물 도깨비 그림. 유물이 없으면 숨긴다.")]
        [SerializeField] private Image _damageGaugeRider;
        [SerializeField] private Color _damageLabelOnFillColor = Color.white;
        [SerializeField] private Color _damageLabelOnEmptyColor = new Color(0.61f, 0.25f, 0.25f);
        [Tooltip("게이지가 1초에 움직이는 양(1 = 게이지 전체). 매 프레임 같은 속도로 차오른다. 0이면 바로 바뀐다.")]
        [SerializeField] [Min(0f)] private float _damageGaugeFillSpeed = 0.5f;

        [Header("찾을 형상")]
        [Tooltip("형상 미리보기를 가로로 늘어놓을 영역.")]
        [SerializeField] private RectTransform _shapePreviewContainer;
        [Tooltip("미리보기 한 칸의 최대 크기(px). 형상이 많으면 영역에 맞게 줄인다.")]
        [SerializeField] private float _previewCellSize = 94f;
        [SerializeField] private float _previewSpacing = 60f;
        [Tooltip("다 찾은 형상에 곱할 색.")]
        [SerializeField] private Color _foundShapeTint = new Color(0.35f, 0.35f, 0.35f);

        [Header("도구 버튼 (SafeDestroy, AttackDestroy, Scout 순서)")]
        [SerializeField] private Button[] _toolButtons = new Button[3];
        [SerializeField] private Color _unselectedToolTint = new Color(0.78f, 0.78f, 0.78f);
        [SerializeField] private Color _disabledToolTint = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        [SerializeField] [Range(0.5f, 1f)] private float _unselectedToolScale = 0.92f;

        [Header("기타 버튼")]
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _infoButton;

        [Header("결과")]
        [SerializeField] private GameObject _resultRoot;
        [SerializeField] private TMP_Text _resultTitleText;
        [SerializeField] private TMP_Text _resultBodyText;
        [SerializeField] private Button _restartButton;

        private readonly List<(PolyominoGroup Group, Image Image)> _shapePreviews = new List<(PolyominoGroup, Image)>();
        private MinigameSession _shownSession;
        private float _builtPreviewWidth = -1f;
        private float _displayedDamage;   // 화면에 보이는 손상도. 실제 값을 일정한 속도로 따라간다

        private void Awake()
        {
            if (_adapter == null)
            {
                Debug.LogError($"{nameof(MinigameHudPresenter)}: _adapter가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < _toolButtons.Length; i++)
            {
                if (_toolButtons[i] == null) continue;
                ToolMode mode = (ToolMode)i;   // 람다가 반복 변수 대신 이 값을 붙잡도록 지역 변수에 복사한다
                _toolButtons[i].onClick.AddListener(() => _adapter.SelectTool(mode));
            }

            // 결과 화면 연출이 정해지기 전까지 포기는 새 판 시작으로 처리한다
            if (_quitButton != null) _quitButton.onClick.AddListener(RestartSession);
            if (_restartButton != null) _restartButton.onClick.AddListener(RestartSession);
            if (_resultRoot != null) _resultRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (Button button in _toolButtons)
            {
                if (button != null) button.onClick.RemoveAllListeners();
            }
            if (_quitButton != null) _quitButton.onClick.RemoveListener(RestartSession);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(RestartSession);
        }

        private void LateUpdate()
        {
            MinigameSession session = _adapter.Session;
            if (session == null) return;

            // 패널이 보드 폭에 맞춰 늘거나 줄면 미리보기 칸 크기도 다시 계산한다
            bool previewWidthChanged = _shapePreviewContainer != null
                && Mathf.Abs(_shapePreviewContainer.rect.width - _builtPreviewWidth) > 0.5f;
            bool isNewSession = session != _shownSession;
            if (isNewSession || previewWidthChanged)
            {
                _shownSession = session;
                RebuildShapePreviews(session);
                RefreshGaugeRiderSprite();
            }
            if (isNewSession)
            {
                // 새 판은 이전 판의 손상도에서 내려오는 연출 없이 바로 시작 값으로 맞춘다
                _displayedDamage = session.DamageGauge;
            }

            RefreshToolCounts(session);
            RefreshDamageGauge(StepDisplayedDamage(session.DamageGauge));
            RefreshShapePreviews(session);
            RefreshToolButtons(session);
            RefreshResult(session);
        }

        private void RestartSession()
        {
            if (_adapter.CanAcceptInput) _adapter.BeginSession();
        }

        private void RefreshToolCounts(MinigameSession session)
        {
            if (_destructiveCountText != null) _destructiveCountText.text = session.RemainingDestructiveToolUses.ToString();
            if (_scoutCountText != null) _scoutCountText.text = session.RemainingScoutToolUses.ToString();
        }

        /// <summary>
        /// 화면에 보이는 손상도를 실제 손상도 쪽으로 한 프레임만큼 움직이고 그 값을 돌려준다.
        /// </summary>
        /// <remarks>
        /// MoveTowards는 매 프레임 같은 양만큼 다가가므로 게이지가 일정한 속도로 차오르고, 목표에 닿으면 정확히 멈춘다.
        /// 결과 화면 등으로 시간이 멈춰도(timeScale 0) 마지막 손상도까지 채워지도록 unscaledDeltaTime을 쓴다.
        /// </remarks>
        private float StepDisplayedDamage(float target)
        {
            if (_damageGaugeFillSpeed <= 0f)
            {
                _displayedDamage = target;
                return target;
            }

            _displayedDamage = Mathf.MoveTowards(_displayedDamage, target, _damageGaugeFillSpeed * Time.unscaledDeltaTime);
            return _displayedDamage;
        }

        private void RefreshDamageGauge(float damage)
        {
            if (_damageGaugeBackground == null || _damageGaugeFill == null) return;

            float width = _damageGaugeBackground.rect.width * damage;
            _damageGaugeFill.gameObject.SetActive(damage > 0f);
            _damageGaugeFill.sizeDelta = new Vector2(width, _damageGaugeFill.sizeDelta.y);

            if (_damageLabelText != null)
            {
                _damageLabelText.color = damage > 0f ? _damageLabelOnFillColor : _damageLabelOnEmptyColor;
            }
            if (_damagePercentText != null)
            {
                _damagePercentText.text = $"{Mathf.RoundToInt(damage * 100f)}%";
            }
            if (_damageGaugeRider != null)
            {
                // 손상도가 0이나 100%에 가까워도 그림이 게이지 양 끝 밖으로 나가지 않게 한다
                RectTransform rider = _damageGaugeRider.rectTransform;
                float minX = rider.rect.width * rider.pivot.x;
                float maxX = _damageGaugeBackground.rect.width - rider.rect.width * (1f - rider.pivot.x);
                float x = Mathf.Clamp(width, minX, Mathf.Max(minX, maxX));
                rider.anchoredPosition = new Vector2(x, rider.anchoredPosition.y);
            }
        }

        private void RefreshGaugeRiderSprite()
        {
            if (_damageGaugeRider == null) return;

            Sprite portrait = _adapter.CurrentRelic != null ? _adapter.CurrentRelic.SpiritPortrait : null;
            _damageGaugeRider.gameObject.SetActive(portrait != null);
            if (portrait == null) return;

            _damageGaugeRider.sprite = portrait;
            _damageGaugeRider.preserveAspect = true;
        }

        private void RebuildShapePreviews(MinigameSession session)
        {
            _shapePreviews.Clear();
            if (_shapePreviewContainer == null) return;
            _builtPreviewWidth = _shapePreviewContainer.rect.width;

            for (int i = _shapePreviewContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_shapePreviewContainer.GetChild(i).gameObject);
            }

            SealMediumData skin = _adapter.CurrentSkin;
            IReadOnlyList<PolyominoGroup> groups = session.Grid.PolyominoGroups;

            // 1. 형상마다 미리보기 크기(칸 수)를 구한다. 세로로 3칸 이상이면 눕혀서 영역 높이를 넘지 않게 한다
            var layouts = new List<(PolyominoGroup Group, Vector2Int Cells, bool Rotated)>();
            int totalColumns = 0;
            foreach (PolyominoGroup group in groups)
            {
                Vector2Int cells = GetBoundingCells(group.Shape);
                bool rotated = cells.y > 2;
                Vector2Int visualCells = rotated ? new Vector2Int(cells.y, cells.x) : cells;
                layouts.Add((group, visualCells, rotated));
                totalColumns += visualCells.x;
            }

            // 2. 모두 한 줄에 들어가도록 칸 크기를 정한다
            Rect area = _shapePreviewContainer.rect;
            float spacingTotal = _previewSpacing * Mathf.Max(0, layouts.Count - 1);
            float cellSize = _previewCellSize;
            if (totalColumns > 0) cellSize = Mathf.Min(cellSize, (area.width - spacingTotal) / totalColumns);
            cellSize = Mathf.Min(cellSize, area.height / 2f);

            // 3. 감싸는 칸(레이아웃용)과 그 안의 그림(회전용)을 나눠 만든다
            //    레이아웃 그룹은 회전을 모르기 때문에, 눕힌 그림도 감싸는 칸은 눕힌 크기로 잡아야 겹치지 않는다
            foreach (var (group, visualCells, rotated) in layouts)
            {
                var slot = new GameObject($"Shape_{group.Shape}", typeof(RectTransform));
                var slotRect = (RectTransform)slot.transform;
                slotRect.SetParent(_shapePreviewContainer, false);
                slotRect.sizeDelta = new Vector2(visualCells.x * cellSize, visualCells.y * cellSize);

                var picture = new GameObject("Picture", typeof(RectTransform), typeof(Image));
                var pictureRect = (RectTransform)picture.transform;
                pictureRect.SetParent(slotRect, false);
                Vector2 pictureSize = rotated
                    ? new Vector2(visualCells.y * cellSize, visualCells.x * cellSize)
                    : slotRect.sizeDelta;
                pictureRect.sizeDelta = pictureSize;
                pictureRect.localRotation = rotated ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

                Image image = picture.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                if (skin != null && skin.TryGetPolyominoPreview(group.Shape, out Sprite sprite)) image.sprite = sprite;

                _shapePreviews.Add((group, image));
            }
        }

        private void RefreshShapePreviews(MinigameSession session)
        {
            foreach (var (group, image) in _shapePreviews)
            {
                if (image != null) image.color = session.Grid.IsExcavated(group) ? _foundShapeTint : Color.white;
            }
        }

        private void RefreshToolButtons(MinigameSession session)
        {
            bool playing = session.State == SessionState.InProgress && _adapter.CanAcceptInput;
            for (int i = 0; i < _toolButtons.Length; i++)
            {
                Button button = _toolButtons[i];
                if (button == null) continue;

                ToolMode mode = (ToolMode)i;
                int remaining = mode == ToolMode.Scout ? session.RemainingScoutToolUses : session.RemainingDestructiveToolUses;
                bool usable = playing && remaining > 0;
                bool selected = _adapter.SelectedTool == mode;

                button.interactable = usable;
                if (button.targetGraphic != null)
                {
                    button.targetGraphic.color = !usable ? _disabledToolTint : selected ? Color.white : _unselectedToolTint;
                }
                float scale = selected && usable ? 1f : _unselectedToolScale;
                button.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void RefreshResult(MinigameSession session)
        {
            if (_resultRoot == null) return;

            bool ended = session.State != SessionState.InProgress;
            if (_resultRoot.activeSelf != ended) _resultRoot.SetActive(ended);
            if (!ended) return;

            bool succeeded = session.State == SessionState.Succeeded;
            if (_resultTitleText != null) _resultTitleText.text = succeeded ? "정화 성공" : "정화 실패";
            if (_resultBodyText != null)
            {
                _resultBodyText.text = succeeded
                    ? $"손상도 {Mathf.RoundToInt(session.DamageGauge * 100f)}% · 정성 {session.RevealedBonusTileCount}\n화면을 눌러 다시 하기"
                    : "유물이 더 상하기 전에 작업을 멈췄어요.\n화면을 눌러 다시 하기";
            }
        }

        private static Vector2Int GetBoundingCells(PolyominoShape shape)
        {
            int maxX = 0, maxY = 0;
            foreach (Vector2Int cell in PolyominoShapes.Definitions[shape])
            {
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }
            return new Vector2Int(maxX + 1, maxY + 1);
        }
    }
}
