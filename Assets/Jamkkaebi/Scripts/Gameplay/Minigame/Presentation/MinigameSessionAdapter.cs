using System;
using System.Collections.Generic;
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
        
        [Header("봉인 매개체 스킨")]
        [SerializeField] private SealMediumCatalog _sealMediumCatalog;
        [SerializeField] private BackgroundView _backgroundView;
        [SerializeField] private SealMedium _sealMedium;     // TODO: 서버 PurificationStartResponse.sealMedium 연동 후 제거

        private MinigameSession _session;
        private Dictionary<Vector2Int, TileView> _tileViews;
        private ToolMode _selectedTool = ToolMode.SafeDestroy;
        private bool _hasHandledEnd;
        
        public event Action<ToolMode> ToolSelectionChanged;
        public event Action<string> FeedbackChanged;
        /// <summary>정찰에 성공했을 때. 정찰한 칸 좌표와 3×3 범위의 위협 타일 수를 전달한다.</summary>
        public event Action<Vector2Int, int> ScoutRevealed;
        public MinigameSession Session => _session;
        /// <summary>현재 판의 봉인 매개체 스킨. 형상 미리보기 등 UI가 같은 그림을 쓰도록 노출한다.</summary>
        public SealMediumData CurrentSkin { get; private set; }
        public IReadOnlyDictionary<Vector2Int, TileView> TileViews => _tileViews;
        public ToolMode SelectedTool => _selectedTool;
        public float TileSize => _tileSize;
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

        public void BeginSession()
        {
            long seed = ResolveSeed(_useFixedSeed, _fixedSeed);
            BeginSession(seed);
        }

        // 서버 연동 후 호출
        public void BeginSession(long seed)
        {
            SealMediumData skin = _sealMediumCatalog.GetSealMediumData(_sealMedium);
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
                {
                    DestroyResult result = _session.UseDestructiveTool(coord, DestructiveToolType.Safe);
                    Debug.Log($"({coord.x}, {coord.y}) 안전 파괴 결과: {result}");
                    HandleSessionEndIfNeeded();
                    break;
                }
                case ToolMode.AttackDestroy:
                {
                    DestroyResult result = _session.UseDestructiveTool(coord, DestructiveToolType.Attack);
                    Debug.Log($"({coord.x}, {coord.y}) 공격 파괴 결과: {result}");
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
            
            TileView view = _tileViews[result.Coordinate];

            switch (result.Outcome)
            {
                case RevealOutcome.AlreadyRevealed:
                    break;
                case RevealOutcome.ReinforcementConsumed:
                    view.ShowCovered(false);
                    break;
                case RevealOutcome.Revealed:
                    if (result.Content == TileContent.Target &&
                        _session.Grid.TryGetPolyominoCell(result.Coordinate, out PolyominoGroup group, out int cellIndex))
                    {
                        view.ShowPolyominoCell(group.Shape, cellIndex, group.RotationCount);
                    }
                    else
                    {
                        view.ShowRevealed(result.Content);
                    }
                    break;
            }
        }

        private void HandleSessionEndIfNeeded()
        {
            if (_hasHandledEnd || _session.State == SessionState.InProgress) return;
            _hasHandledEnd = true;
            Debug.Log($"세션 종료: {_session.State} (손상도: {_session.DamageGauge:P0}, 정성 보너스: {_session.RevealedBonusTileCount})");
            // TODO: 세션 종료 결과 화면 처리
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
