using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    public class MinigameSessionAdapter : MonoBehaviour
    {
        [Header("보드 설정")]
        [SerializeField] private MinigameDifficultyTable _difficultyTable;   // TODO: 서버 PurificationStartResponse.board 연동 후 서버 값으로 교체
        [SerializeField] private MinigameDifficulty _difficulty = MinigameDifficulty.Normal;
        
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private float _tileSize = 1f;
        [SerializeField] [Range(0.5f, 1f)] private float _tileFillRatio = 0.9f;
        [Tooltip("비워 두면 격자 선을 그리지 않는다. (작업대 탭처럼 이 기능이 없는 씬)")]
        [SerializeField] private BoardGridLineView _gridLineView;
        [Tooltip("비워 두면 카메라를 조정하지 않는다. (작업대 탭처럼 카메라를 따로 관리하는 씬)")]
        [SerializeField] private BoardCameraFitter _cameraFitter;
        [SerializeField] private Camera _inputCamera;
        [SerializeField] private bool _usePointerEvents;
        
        [Header("고정 시드 사용")]
        [SerializeField] private bool _useFixedSeed;
        [SerializeField] private long _fixedSeed;
        
        [Header("유물")]
        [Tooltip("선택. 정화할 유물. 연결하면 유물의 봉인 매개체를 쓰고, 비우면 아래 Seal Medium 값을 쓴다.")]
        [SerializeField] private RelicData _relic;          // TODO: 작업대·전시관에서 진입할 때 넘겨받도록 교체

        [Header("연쇄 연출")]
        [Tooltip("도움 타일이 한 줄을 열 때, 도움 타일에서 한 칸 멀어질 때마다 늦게 깨지는 시간(초). 0이면 한 번에 깨진다.")]
        [SerializeField] [Min(0f)] private float _chainStepDelay = 0.05f;

        [Header("종료 연출")]
        [Tooltip("판이 끝난 뒤 남은 타일을 열기 시작할 때까지 기다리는 시간(초). 마지막으로 깬 조각이 흩어질 시간을 준다.")]
        [SerializeField] [Min(0f)] private float _endRevealDelay = 0.4f;
        [Tooltip("남은 타일을 왼쪽 위부터 하나씩 여는 간격(초). 0이면 한 번에 모두 연다.")]
        [SerializeField] [Min(0f)] private float _endRevealInterval = 0.04f;
        [Tooltip("마지막 타일이 열린 뒤 결과 화면을 띄우기까지 기다리는 시간(초).")]
        [SerializeField] [Min(0f)] private float _resultDelay = 0.8f;

        [Header("봉인 매개체 스킨")]
        [SerializeField] private SealMediumCatalog _sealMediumCatalog;
        [SerializeField] private BackgroundView _backgroundView;
        [SerializeField] private SealMedium _sealMedium;     // TODO: 서버 PurificationStartResponse.sealMedium 연동 후 제거

        private MinigameSession _session;
        private Dictionary<Vector2Int, TileView> _tileViews;
        private ToolMode _selectedTool = ToolMode.SafeDestroy;
        private bool _hasHandledEnd;
        private Coroutine _endRevealRoutine;
        // 도구 한 번에 열린 타일들. 바로 그리지 않고 모았다가 연쇄 순서대로 그린다
        private readonly List<TileRevealResult> _pendingResults = new List<TileRevealResult>();
        private bool _isCollectingResults;
        // 연쇄로 늦게 깨지는 타일이 모두 깨지는 시각(Time.time 기준)
        private float _chainVisualsEndTime;
        
        public event Action<ToolMode> ToolSelectionChanged;
        public event Action<string> FeedbackChanged;
        /// <summary>정찰에 성공했을 때. 정찰한 칸 좌표와 3×3 범위의 위협 타일 수를 전달한다.</summary>
        public event Action<Vector2Int, int> ScoutRevealed;
        /// <summary>타일 덮개가 깨지거나 강화가 벗겨졌을 때. 깨짐 연출이 쓴다.</summary>
        public event Action<TileBreakInfo> TileBroken;
        /// <summary>도구를 써서 손상도가 올랐을 때. 오른 양(0~1)을 전달한다.</summary>
        public event Action<float> DamageTaken;
        /// <summary>
        /// 도움 타일이 깨져 한 줄을 열기 시작할 때. 도움 타일 좌표와 가로 방향인지(false면 세로)를 전달한다.
        /// 줄의 타일은 <see cref="ChainStepDelay"/> 간격으로 도움 타일에서 가까운 칸부터 깨진다.
        /// </summary>
        public event Action<Vector2Int, bool> HelperChainStarted;
        public MinigameSession Session => _session;
        /// <summary>현재 판의 봉인 매개체 스킨. 형상 미리보기 등 UI가 같은 그림을 쓰도록 노출한다.</summary>
        public SealMediumData CurrentSkin { get; private set; }
        /// <summary>판이 끝나고 정답 공개 연출까지 마쳐 결과 화면을 띄워도 되면 true.</summary>
        public bool IsResultReady { get; private set; }
        /// <summary>현재 판에서 정화하는 유물. 연결되지 않았으면 null.</summary>
        public RelicData CurrentRelic => _relic;
        public IReadOnlyDictionary<Vector2Int, TileView> TileViews => _tileViews;
        public ToolMode SelectedTool => _selectedTool;
        public float TileSize => _tileSize;
        public float ChainStepDelay => _chainStepDelay;
        public bool CanAcceptInput => isActiveAndEnabled && Time.timeScale > 0f &&
            (!_inputCamera || _inputCamera.isActiveAndEnabled);

        private void Start()
        {
            if (_difficultyTable == null)
            {
                Debug.LogError($"{nameof(MinigameSessionAdapter)}: _difficultyTable이 연결되지 않았습니다.", this);
                return;
            }
            if (_sealMediumCatalog == null)
            {
                Debug.LogError($"{nameof(MinigameSessionAdapter)}: _sealMediumCatalog가 연결되지 않았습니다.", this);
                return;
            }
            if (_backgroundView == null)
            {
                Debug.LogError($"{nameof(MinigameSessionAdapter)}: _backgroundView가 연결되지 않았습니다.", this);
                return;
            }
            
            BeginSession();
        }

        private SealMediumData ResolveSkin()
        {
            if (_relic == null) return _sealMediumCatalog.GetSealMediumData(_sealMedium);
            if (_relic.SealMedium != null) return _relic.SealMedium;

            Debug.LogWarning($"{nameof(MinigameSessionAdapter)}: 유물 '{_relic.name}'에 봉인 매개체가 없어 {_sealMedium}을 씁니다.", this);
            return _sealMediumCatalog.GetSealMediumData(_sealMedium);
        }

        public void BeginSession()
        {
            long seed = ResolveSeed(_useFixedSeed, _fixedSeed);
            BeginSession(seed);
        }

        // 서버 연동 후 호출
        public void BeginSession(long seed)
        {
            SealMediumData skin = ResolveSkin();
            CurrentSkin = skin;
            
            // 1. 이전 세션 정리
            if (_session != null)
            {
                _session.TileRevealAttempted -= OnTileRevealAttempted;
            }

            if (_tileViews != null)
            {
                foreach (TileView view in _tileViews.Values)
                {
                    view.Clicked -= HandleTileClicked;
                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                }
            }
            
            _hasHandledEnd = false;
            IsResultReady = false;
            // 이전 판의 연쇄·정답 공개 연출이 새 판 타일을 건드리지 않도록 모두 멈춘다
            StopAllCoroutines();
            _endRevealRoutine = null;
            _pendingResults.Clear();
            _isCollectingResults = false;
            _chainVisualsEndTime = 0f;
            
            // 2. 새 세션 생성
            MinigameBoardConfig config = _difficultyTable.GetConfig(_difficulty);
            _session = new MinigameSession(config, seed);
            Debug.Log($"[Minigame] seed: {_session.Seed}");
            _tileViews = new Dictionary<Vector2Int, TileView>();
            _backgroundView.Apply(skin);
            
            foreach (Vector2Int coord in _session.Grid.AllCoordinates())
            {
                Tile tile = _session.Grid.GetTile(coord.x, coord.y);
                
                GameObject instance = Instantiate(_tilePrefab, _gridContainer);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = _gridContainer.gameObject.layer;
                instance.transform.localPosition = GridToWorldPosition(coord);
                instance.transform.localScale = new Vector3(_tileSize * _tileFillRatio, _tileSize * _tileFillRatio, 1f);
                
                TileView view = instance.GetComponent<TileView>();
                view.Initialize(coord, skin);
                view.UsesPointerEvents = _usePointerEvents;
                view.ShowCovered(tile.IsReinforced);
                view.Clicked += HandleTileClicked;
                
                _tileViews[coord] = view;
            }
            
            if (_gridLineView != null)
            {
                _gridLineView.Build(_gridContainer, _session.Grid.Width, _session.Grid.Height, _tileSize, skin.GridLineColor);
            }

            if (_cameraFitter != null)
            {
                _cameraFitter.Fit(_gridContainer, _session.Grid.Width, _session.Grid.Height, _tileSize);
            }
            
            _session.TileRevealAttempted += OnTileRevealAttempted;
            FeedbackChanged?.Invoke("타일을 선택해 발굴하세요.");
        }

        private void Update()
        {
            if (!CanAcceptInput) return;
            
            // TODO: 정식 도구 선택 UI 완성되면 해당 Update는 필요 없음
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(ToolMode.SafeDestroy);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(ToolMode.AttackDestroy);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(ToolMode.Scout);
        }

        private void OnEnable()
        {
            if (_session != null)
            {
                _session.TileRevealAttempted -= OnTileRevealAttempted;
                _session.TileRevealAttempted += OnTileRevealAttempted;
            }
        }

        private void OnDisable()
        {
            if (_session != null)
            {
                _session.TileRevealAttempted -= OnTileRevealAttempted;
            }

            // 컴포넌트만 꺼져도 코루틴이 돌지 않도록 직접 멈춘다
            StopAllCoroutines();
            _chainVisualsEndTime = 0f;
            if (_session == null || _tileViews == null) return;

            // 연쇄 연출을 기다리던 타일은 세션 상태대로 바로 맞춘다
            SyncAllViews();
            // 정답 공개 도중이었다면 남은 칸을 바로 열고 결과를 띄울 수 있게 한다
            if (_endRevealRoutine != null)
            {
                _endRevealRoutine = null;
                foreach (Vector2Int coord in GetEndRevealOrder())
                {
                    if (_tileViews[coord] != null) ShowTileContent(_tileViews[coord], coord);
                }
                IsResultReady = true;
            }
        }

        /// <summary>
        /// 모든 타일 그림을 세션의 현재 상태(열림·강화)에 맞춘다. 연출 없이 바로 바꾼다.
        /// </summary>
        private void SyncAllViews()
        {
            foreach (KeyValuePair<Vector2Int, TileView> pair in _tileViews)
            {
                // 씬을 닫는 중이면 타일이 이미 지워졌을 수 있다
                if (pair.Value == null) continue;
                Tile tile = _session.Grid.GetTile(pair.Key.x, pair.Key.y);
                if (tile.IsRevealed) ShowTileContent(pair.Value, pair.Key);
                else pair.Value.ShowCovered(tile.IsReinforced);
            }
        }

        /// <summary>
        /// 타일의 월드 좌표를 구한다. 타일 위에 UI를 띄울 때 쓴다.
        /// </summary>
        /// <returns>현재 판에 그 좌표의 타일이 있으면 true.</returns>
        public bool TryGetTileWorldPosition(Vector2Int coord, out Vector3 position)
        {
            position = Vector3.zero;
            if (_tileViews == null || !_tileViews.TryGetValue(coord, out TileView view) || view == null) return false;
            position = view.transform.position;
            return true;
        }

        private Vector2 GridToWorldPosition(Vector2Int coord)
        {
            float offsetX = (_session.Grid.Width - 1) * _tileSize * 0.5f;
            float offsetY = (_session.Grid.Height - 1) * _tileSize * 0.5f;
            return new Vector2(coord.x * _tileSize - offsetX, coord.y * _tileSize - offsetY);
        }

        public void SelectTool(ToolMode mode)
        {
            if (!CanAcceptInput) return;
            _selectedTool = mode;
            Debug.Log($"도구 변경: {mode}");
            ToolSelectionChanged?.Invoke(mode);
        }

        private void HandleTileClicked(Vector2Int coord)
        {
            if (!CanAcceptInput || _session == null) return;
            
            switch (_selectedTool)
            {
                case ToolMode.SafeDestroy:
                case ToolMode.AttackDestroy:
                {
                    DestructiveToolType type = _selectedTool == ToolMode.SafeDestroy ? DestructiveToolType.Safe : DestructiveToolType.Attack;
                    float damageBefore = _session.DamageGauge;

                    // 세션이 열린 타일을 이벤트로 하나씩 알려 주면 모아 두었다가, 다 끝난 뒤 연쇄 순서를 계산해 그린다
                    _isCollectingResults = true;
                    DestroyResult result;
                    try
                    {
                        result = _session.UseDestructiveTool(coord, type);
                    }
                    finally
                    {
                        // 예외가 나도 모으기 상태가 남지 않게 한다
                        _isCollectingResults = false;
                    }
                    Debug.Log($"({coord.x}, {coord.y}) {type} 파괴 결과: {result}");

                    PlayRevealBatch(_selectedTool);
                    float damage = _session.DamageGauge - damageBefore;
                    if (damage > 0f) DamageTaken?.Invoke(damage);
                    HandleSessionEndIfNeeded();
                    break;
                }
                case ToolMode.Scout:
                {
                    ScoutResult result = _session.UseScoutTool(coord, out int threatCount);
                    Debug.Log($"({coord.x}, {coord.y}) 정찰 결과: {result}, 위협 타일 {threatCount}개");
                    FeedbackChanged?.Invoke($"정찰: {result} · 주변 위협 {threatCount}개");
                    if (result == ScoutResult.Success) ScoutRevealed?.Invoke(coord, threatCount);
                    break;
                }
            }
        }

        private void OnTileRevealAttempted(TileRevealResult result)
        {
            Debug.Log($"({result.Coordinate.x}, {result.Coordinate.y}) {result.Content} / {result.Outcome}");
            if (result.Outcome == RevealOutcome.AlreadyRevealed) return;

            _pendingResults.Add(result);
            // 도구 사용 밖에서 열린 경우(앞으로 다른 경로가 생길 때)는 모으지 않고 바로 그린다
            if (!_isCollectingResults) PlayRevealBatch(_selectedTool);
        }

        /// <summary>도구 한 번으로 열린 타일 하나와, 그 타일을 그릴 때까지 기다릴 시간.</summary>
        private readonly struct RevealStep
        {
            public readonly TileRevealResult Result;
            public readonly float Delay;
            /// <summary>도움 타일일 때 연쇄 방향. 가로면 true.</summary>
            public readonly bool IsHorizontalChain;

            public RevealStep(TileRevealResult result, float delay, bool isHorizontalChain)
            {
                Result = result;
                Delay = delay;
                IsHorizontalChain = isHorizontalChain;
            }
        }

        /// <summary>
        /// 모아 둔 결과를 연쇄 순서대로 그린다. 도움 타일과 같은 줄의 타일은 도움 타일에서 먼 칸일수록 늦게 깨진다.
        /// </summary>
        /// <remarks>
        /// 세션 상태는 이미 모두 바뀌었고, 여기서는 화면에 보이는 순서만 늦춘다. 기다리는 동안 그 칸을 다시 눌러도 이미 열린 칸으로 처리된다.
        /// </remarks>
        private void PlayRevealBatch(ToolMode tool)
        {
            if (_pendingResults.Count == 0) return;
            List<RevealStep> steps = BuildRevealSteps(_pendingResults);
            _pendingResults.Clear();

            // 늦출 타일이 없으면 이번 프레임에 바로 그린다
            float lastDelay = steps.Max(step => step.Delay);
            if (lastDelay <= 0f)
            {
                foreach (RevealStep step in steps) ApplyRevealStep(step, tool);
                return;
            }

            _chainVisualsEndTime = Mathf.Max(_chainVisualsEndTime, Time.time + lastDelay);
            StartCoroutine(PlayRevealSteps(steps, tool));
        }

        private IEnumerator PlayRevealSteps(List<RevealStep> steps, ToolMode tool)
        {
            float elapsed = 0f;
            foreach (RevealStep step in steps)   // Delay 순서로 정렬되어 있다
            {
                while (elapsed < step.Delay)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                }
                ApplyRevealStep(step, tool);
            }
        }

        /// <summary>
        /// 각 결과가 언제 깨질지 정한다.
        /// </summary>
        /// <remarks>
        /// 세션은 도구로 친 칸을 먼저, 그다음 도움 타일 연쇄로 추가된 칸을 알려 준다.
        /// 앞서 열린 도움 타일과 같은 줄에 있으면 (그 도움 타일이 깨진 시각 + 거리 × 간격)에 깨지게 한다.
        /// 연쇄 방향은 같은 줄에서 함께 열린 칸이 더 많은 쪽으로 본다(세션 규칙과 같은 기준).
        /// </remarks>
        private List<RevealStep> BuildRevealSteps(List<TileRevealResult> results)
        {
            var helperDelays = new List<(Vector2Int Coord, float Delay)>();
            var steps = new List<RevealStep>();

            foreach (TileRevealResult result in results)
            {
                Vector2Int coord = result.Coordinate;
                float delay = 0f;
                bool hasSource = false;
                foreach ((Vector2Int helper, float helperDelay) in helperDelays)
                {
                    if (helper.x != coord.x && helper.y != coord.y) continue;
                    int distance = Mathf.Abs(helper.x - coord.x) + Mathf.Abs(helper.y - coord.y);
                    float candidate = helperDelay + distance * _chainStepDelay;
                    if (!hasSource || candidate < delay)
                    {
                        delay = candidate;
                        hasSource = true;
                    }
                }

                bool isHelper = result.Outcome == RevealOutcome.Revealed && result.Content == TileContent.Helper;
                bool isHorizontal = false;
                if (isHelper)
                {
                    int rowCount = results.Count(r => r.Coordinate.y == coord.y && r.Coordinate != coord);
                    int columnCount = results.Count(r => r.Coordinate.x == coord.x && r.Coordinate != coord);
                    isHorizontal = rowCount >= columnCount;
                    helperDelays.Add((coord, delay));
                }
                steps.Add(new RevealStep(result, delay, isHorizontal));
            }

            // OrderBy는 같은 값끼리 원래 순서를 지키므로, 같은 칸의 강화 벗김 → 열림 순서가 뒤집히지 않는다
            return steps.OrderBy(step => step.Delay).ToList();
        }

        private void ApplyRevealStep(RevealStep step, ToolMode tool)
        {
            TileRevealResult result = step.Result;
            if (!_tileViews.TryGetValue(result.Coordinate, out TileView view) || view == null) return;

            if (result.Outcome == RevealOutcome.ReinforcementConsumed)
            {
                // 그림을 바꾸기 전에 덮개 그림을 기억해 두어야 깨짐 연출이 그 그림을 조각낼 수 있다
                Sprite coverSprite = view.CurrentSprite;
                Color coverColor = view.CurrentColor;
                view.ShowCovered(false);
                view.PlayPunch(0.9f, 0.12f);
                TileBroken?.Invoke(new TileBreakInfo(view, coverSprite, coverColor, false, tool, result.Content, false));
                return;
            }

            BreakTile(view, result.Coordinate, tool, false);
            if (result.Content == TileContent.Helper) HelperChainStarted?.Invoke(result.Coordinate, step.IsHorizontalChain);
        }

        /// <summary>
        /// 타일 덮개를 깨고 내용물을 보여 준다. 깨짐 연출에 쓸 수 있도록 깨지기 전 덮개 그림을 이벤트로 알린다.
        /// </summary>
        private void BreakTile(TileView view, Vector2Int coord, ToolMode tool, bool isEndReveal)
        {
            Sprite coverSprite = view.CurrentSprite;
            Color coverColor = view.CurrentColor;

            ShowTileContent(view, coord);
            // 덮개 조각이 흩어지는 동안 아래 내용물이 톡 튀어나오듯 보이게 한다
            view.PlayPunch(0.8f, 0.15f);
            TileContent content = _session.Grid.GetTile(coord.x, coord.y).Content;
            TileBroken?.Invoke(new TileBreakInfo(view, coverSprite, coverColor, true, tool, content, isEndReveal));
        }

        /// <summary>
        /// 타일 내용물을 그린다. 목표 칸은 폴리오미노 조각 그림으로, 나머지는 내용물 색으로 표시한다.
        /// </summary>
        private void ShowTileContent(TileView view, Vector2Int coord)
        {
            TileContent content = _session.Grid.GetTile(coord.x, coord.y).Content;
            if (content == TileContent.Target &&
                _session.Grid.TryGetPolyominoCell(coord, out PolyominoGroup group, out int cellIndex))
            {
                view.ShowPolyominoCell(group.Shape, cellIndex, group.RotationCount);
                return;
            }
            view.ShowRevealed(content);
        }

        private void HandleSessionEndIfNeeded()
        {
            if (_hasHandledEnd || _session.State == SessionState.InProgress) return;
            _hasHandledEnd = true;
            Debug.Log($"세션 종료: {_session.State} (손상도: {_session.DamageGauge:P0}, 정성 보너스: {_session.RevealedBonusTileCount})");
            _endRevealRoutine = StartCoroutine(RevealRemainingTiles());
        }

        /// <summary>
        /// 판이 끝나면 아직 깨지지 않은 타일을 왼쪽 위부터 차례로 깨서 정답을 보여 준 뒤 결과 화면을 허락한다.
        /// </summary>
        /// <remarks>
        /// 화면에만 보여 주는 연출이라 세션의 타일 상태(IsRevealed)는 바꾸지 않는다. 판이 이미 끝나 점수에는 영향이 없다.
        /// </remarks>
        private IEnumerator RevealRemainingTiles()
        {
            // 마지막 도구의 연쇄 연출이 끝난 뒤에 시작한다
            while (Time.time < _chainVisualsEndTime) yield return null;
            if (_endRevealDelay > 0f) yield return new WaitForSeconds(_endRevealDelay);

            // 목록을 미리 만들어 두어야, 기다리는 동안 상태가 바뀌어도 순서가 흔들리지 않는다
            List<Vector2Int> order = GetEndRevealOrder();
            var interval = _endRevealInterval > 0f ? new WaitForSeconds(_endRevealInterval) : null;
            foreach (Vector2Int coord in order)
            {
                BreakTile(_tileViews[coord], coord, ToolMode.SafeDestroy, true);
                if (interval != null) yield return interval;
            }

            if (_resultDelay > 0f) yield return new WaitForSeconds(_resultDelay);
            _endRevealRoutine = null;
            IsResultReady = true;
        }

        /// <summary>
        /// 아직 열리지 않은 칸을 왼쪽 위부터 오른쪽으로, 한 줄씩 아래로 내려가는 순서로 돌려준다.
        /// </summary>
        /// <remarks>그리드 좌표는 y가 클수록 위쪽(<see cref="GridToWorldPosition"/>)이므로 y를 큰 값부터 돈다.</remarks>
        private List<Vector2Int> GetEndRevealOrder()
        {
            var order = new List<Vector2Int>();
            for (int y = _session.Grid.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < _session.Grid.Width; x++)
                {
                    if (!_session.Grid.GetTile(x, y).IsRevealed) order.Add(new Vector2Int(x, y));
                }
            }
            return order;
        }

        private long ResolveSeed(bool isUsingFixedSeed, long fixedSeed)
        {
            if (isUsingFixedSeed)
            {
                return fixedSeed;
            }

            return DateTime.UtcNow.Ticks;
        }
    }
}
