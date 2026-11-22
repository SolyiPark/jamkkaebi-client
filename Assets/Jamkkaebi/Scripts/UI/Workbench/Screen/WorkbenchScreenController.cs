using System;
using System.Collections;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 작업대 화면의 흐름을 맡는다. 백엔드를 부르고, 응답을 화면 값으로 바꿔 View에 넘긴다.
    /// 작업대 탭은 숨기면 루트가 비활성화되어(PauseWhileHidden) 코루틴이 멈추므로,
    /// 다시 보일 때 보던 화면을 새로 조회하고, 숨기기 전에 시작한 요청의 늦은 응답은 버린다(수명 번호).
    /// 화면을 바꿀 때마다 화면 번호를 올린다. 조회(홈·상세)와 명령(상자 열기·정화 시작·보고·이름 저장)의 응답은
    /// 시작할 때의 화면 번호가 그대로일 때만 화면을 바꾼다. 그래서 사용자가 다른 화면으로 옮긴 뒤 늦게 온 응답이
    /// 새 화면을 덮지 않는다. 명령이 진행 중(Busy)이면 화면을 떠나는 버튼(뒤로·홈으로·그만두기)을 받지 않는다.
    /// </summary>
    public sealed class WorkbenchScreenController : MonoBehaviour
    {
        public enum Page
        {
            Home = 0,
            RelicDetail = 1,
            SpiritDetail = 2,
            Playing = 3,
            Result = 4,
            ReportRetry = 5
        }

        private MirrorSceneRoot _root;
        private RectTransform _canvas;
        private IWorkbenchBackend _backend;
        private WorkbenchViewDataBuilder _builder;
        private WorkbenchHomeView _home;
        private WorkbenchDetailView _detail;
        private int _lifetime;         // 숨길 때마다 올린다. 이전 수명에서 시작한 요청의 응답은 버린다.
        private int _viewSequence;     // 화면을 바꿀 때마다 올린다. 이전 화면에서 시작한 요청의 응답은 화면을 바꾸지 않는다.
        private string _pendingStatus; // 다음에 홈을 그린 뒤 안내 자리에 보일 문장(상자 결과 등)
        private bool _initialized;

        private Func<DateTime> _clock;
        private int _relicId;                  // 상세·정화 중인 유물
        private SpiritDetailData _spirit;      // 보고 있는 정령 상세(난이도를 고를 때 다시 그린다)
        private MinigameDifficulty _difficulty = MinigameDifficulty.Normal; // 관리 플레이에서 고른 난이도
        private string _pendingDetailStatus;   // 다음에 상세를 그린 뒤 보일 문장(이름 저장 등)
        private PurificationSession _session;  // 시작한 판
        private MinigameLauncher _launcher;
        private PlayReport _reported;          // 보낸 보고. 보고가 실패하면 이것을 그대로 다시 보낸다(서버는 같은 내용이어야 정산을 돌려줌)
        private SettlementData _settlement;
        private string _playingTitle;          // 정화 중 화면의 제목. 재전송할 때 같은 화면을 다시 그린다.
        private string _playingDetail;         // 정화 중 화면의 두 번째 줄(유물·정령 이름과 난이도)

        /// <summary>상자 열기·정화 시작·보고처럼 상태를 바꾸는 요청이 진행 중인지. 진행 중에는 버튼을 다시 받지 않는다.</summary>
        public bool Busy { get; private set; }

        public Page CurrentPage { get; private set; } = Page.Home;
        public WorkbenchHomeView Home => _home;
        public WorkbenchDetailView Detail => _detail;
        public IWorkbenchBackend Backend => _backend;

        /// <summary>화면을 만든다. 씬의 WorkbenchPresenter가 Awake에서 부른다.</summary>
        /// <param name="clock">지금 시각. 백엔드와 같은 시계를 써야 경과일이 맞는다. 생략하면 UTC 현재 시각.</param>
        public void Initialize(MirrorSceneRoot root, Font font, IWorkbenchBackend backend = null, Func<DateTime> clock = null)
        {
            if (_initialized) return;
            _root = root ? root : throw new ArgumentNullException(nameof(root));
            if (!font) throw new ArgumentNullException(nameof(font));

            _clock = clock ?? (() => DateTime.UtcNow);
            _backend = backend ?? WorkbenchDemo.CreateBackend(_clock);
            _builder = new WorkbenchViewDataBuilder(WorkbenchRelicSeed.Instance, IntimacyPolicy.Default, RestorePolicy.Default);
            _canvas = CreateCanvas();
            _launcher = GetComponent<MinigameLauncher>();
            if (!_launcher) _launcher = gameObject.AddComponent<MinigameLauncher>();
            _launcher.Initialize(_root);

            _home = new WorkbenchHomeView(_canvas, font);
            _home.Rebuilt += ApplyLayer;
            _home.RegisterClicked += Register;
            _home.SlotSelected += OpenDetail;
            _home.SpiritSelected += OpenDetail;

            _detail = new WorkbenchDetailView(_canvas, font);
            _detail.Rebuilt += ApplyLayer;
            _detail.BackClicked += OnBack;
            _detail.PurifyClicked += StartPurification;
            _detail.ConfirmClicked += ConfirmResult;
            _detail.RetryClicked += RetryReport;
            _detail.DifficultySelected += SelectDifficulty;
            _detail.RenameClicked += OpenRename;
            _detail.RenameSubmitted += Rename;
            _detail.RenameCancelled += _detail.HideRenameSheet;

            _initialized = true;
            ShowHome();
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            // 숨겼다가 다시 보이면(PauseWhileHidden) 그동안 바뀌었을 수 있으니 보던 화면을 새로 조회한다.
            // 정화 중·결과·재전송 화면은 그대로 둔다(화면 오브젝트는 숨김 동안에도 남아 있다).
            if (CurrentPage == Page.Home) ShowHome();
            else if (CurrentPage == Page.RelicDetail || CurrentPage == Page.SpiritDetail) OpenDetail(_relicId);
        }

        private void OnDisable()
        {
            // 비활성화되면 코루틴은 멈춘다. 끝나지 못한 요청의 늦은 콜백이 와도 무시하도록 수명을 올린다.
            _lifetime++;
            Busy = false;
            _pendingStatus = null;
        }

        private void OnDestroy()
        {
            if (_home != null)
            {
                _home.Rebuilt -= ApplyLayer;
                _home.RegisterClicked -= Register;
                _home.SlotSelected -= OpenDetail;
                _home.SpiritSelected -= OpenDetail;
            }
            if (_detail != null)
            {
                _detail.Rebuilt -= ApplyLayer;
                _detail.BackClicked -= OnBack;
                _detail.PurifyClicked -= StartPurification;
                _detail.ConfirmClicked -= ConfirmResult;
                _detail.RetryClicked -= RetryReport;
                _detail.DifficultySelected -= SelectDifficulty;
                _detail.RenameClicked -= OpenRename;
                _detail.RenameSubmitted -= Rename;
                _detail.RenameCancelled -= _detail.HideRenameSheet;
            }
            if (_root) _root.SetNavigationLocked(false);
        }

        // ---------- 홈 ----------

        /// <summary>작업대를 다시 조회해 홈을 그린다.</summary>
        public void ShowHome()
        {
            SetPage(Page.Home);
            if (!isActiveAndEnabled) return;
            int sequence = _viewSequence;
            Request<WorkbenchSnapshot>((done, fail) => _backend.GetWorkbench(done, fail),
                snapshot =>
                {
                    if (sequence != _viewSequence) return;
                    _home.Render(_builder.Home(snapshot));
                    ShowPendingStatus();
                },
                error =>
                {
                    if (sequence != _viewSequence) return;
                    _home.RenderMessage("작업대를 불러오지 못했어요.\n" + error);
                    _pendingStatus = null;
                });
        }

        private void Register()
        {
            if (Busy) return;
            Busy = true;
            int sequence = _viewSequence;
            _home.ShowStatus("상자를 여는 중…");
            string requestId = Guid.NewGuid().ToString();
            Request<BoxOpenOutcome>((done, fail) => _backend.OpenBox(BoxSource.Free, requestId, done, fail),
                outcome =>
                {
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    _pendingStatus = _builder.BoxResult(outcome);
                    ShowHome();
                },
                error =>
                {
                    // 실패해도 화면이 지금 상태와 어긋나 있을 수 있으니 다시 조회하고 이유를 보인다.
                    // 모의 백엔드의 실패는 규칙 위반(가득 참 등)이라 같은 번호로 다시 보내도 결과가 같다.
                    // 네트워크 재전송(같은 requestId)은 서버 연결 때 붙인다.
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    _pendingStatus = error;
                    ShowHome();
                });
        }

        // ---------- 유물 상세 ----------

        /// <summary>유물 상세를 조회해 그린다. 최초 정화 중이면 유물 상세, 마쳤으면 정령 상세.</summary>
        public void OpenDetail(int relicId)
        {
            if (relicId != _relicId) _spirit = null;
            _relicId = relicId;
            SetPage(_spirit != null ? Page.SpiritDetail : Page.RelicDetail);
            if (!isActiveAndEnabled) return;
            int sequence = _viewSequence;
            _detail.RenderLoading();
            Request<RelicDetail>((done, fail) => _backend.GetRelicDetail(relicId, done, fail),
                detail =>
                {
                    if (sequence != _viewSequence) return;
                    if (detail.Status == RelicStatus.InProgress)
                    {
                        _spirit = null;
                        SetPage(Page.RelicDetail, keepSequence: true);
                        _detail.RenderRelic(_builder.RelicDetail(detail));
                    }
                    else
                    {
                        _spirit = _builder.SpiritDetail(detail, _clock());
                        _difficulty = ChooseDifficulty(_spirit, _difficulty);
                        SetPage(Page.SpiritDetail, keepSequence: true);
                        _detail.RenderSpirit(_spirit, _difficulty);
                    }
                    if (_pendingDetailStatus != null) _detail.ShowStatus(_pendingDetailStatus);
                    _pendingDetailStatus = null;
                },
                error =>
                {
                    if (sequence != _viewSequence) return;
                    _pendingStatus = error;
                    ShowHome();
                });
        }

        private void StartPurification()
        {
            if (Busy) return;
            Busy = true;
            int relicId = _relicId;
            int sequence = _viewSequence;
            // 최초 정화나 난이도가 하나뿐인 계획은 비워 보내고, 고를 수 있으면 고른 난이도를 보낸다.
            MinigameDifficulty? difficulty = CurrentPage == Page.SpiritDetail && _spirit != null && _spirit.DifficultySelectable
                ? _difficulty : (MinigameDifficulty?)null;
            _detail.ShowStatus("정화를 준비하는 중…");
            Request<PurificationSession>((done, fail) => _backend.StartPurification(relicId, difficulty, done, fail),
                session =>
                {
                    Busy = false;
                    // 시작을 기다리는 동안 화면을 떠났으면 이 판은 쓰지 않는다(같은 유물로 새 판을 시작하면 대체된다).
                    if (sequence != _viewSequence) return;
                    _session = session;
                    SetPage(Page.Playing);
                    _playingTitle = WorkbenchText.PhaseName(session.Phase)
                        + (session.Route == EntryRoute.Care ? " · 관리 플레이" : "");
                    _playingDetail = $"{PlayingNameOf(session)}\n난이도 {WorkbenchText.DifficultyName(session.Difficulty)}";
                    _detail.RenderPlaying(_playingTitle, _playingDetail);
                    int playSequence = _viewSequence;
                    StartCoroutine(_launcher.Run(session.ToSetup(), report =>
                    {
                        if (playSequence != _viewSequence) return;
                        Report(report);
                    }, error =>
                    {
                        if (playSequence != _viewSequence) return;
                        _session = null;
                        _pendingDetailStatus = error;
                        OpenDetail(relicId);
                    }));
                },
                error =>
                {
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    _detail.ShowStatus(error);
                });
        }

        // ---------- 정화 중·결과 ----------
        
        private void Report(PlayReport report)
        {
            if (Busy || _session == null || report == null) return;
            Busy = true;
            _reported = report;
            string token = _session.SessionToken;
            int relicId = _session.RelicId;
            int sequence = _viewSequence;
            _detail.ShowStatus("결과를 보내는 중…");
            Request<SettlementResult>((done, fail) => _backend.ReportPurification(token, report, done, fail),
                settlement =>
                {
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    _session = null;
                    _settlement = _builder.Settlement(relicId, settlement);
                    SetPage(Page.Result);
                    _detail.RenderResult(_settlement);
                },
                error =>
                {
                    // 보고는 같은 토큰·같은 결과로 다시 보내도 한 번만 정산된다(서버와 모의 백엔드 모두).
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    SetPage(Page.ReportRetry);
                    _detail.RenderRetry("결과를 보내지 못했어요.\n" + error);
                });
        }

        private void RetryReport()
        {
            if (_session == null) return;
            SetPage(Page.Playing);
            // 재전송 화면의 버튼을 지우고, 보내는 중 문장을 보일 자리(Status)가 있는 정화 중 화면으로 되돌린다.
            _detail.RenderPlaying(_playingTitle, _playingDetail);
            Report(_reported);
        }

        /// <summary>
        /// 정화 중 화면에 보일 이름. 정령이 된 유물의 관리 플레이는 정령 상세와 같은 정령 이름을,
        /// 최초 정화(아직 정령이 아님)는 유물 별칭을 쓴다. 정령 상세를 받지 못했으면 별칭으로 둔다.
        /// </summary>
        private string PlayingNameOf(PurificationSession session)
        {
            bool spiritShown = session.Route == EntryRoute.Care && _spirit != null && _spirit.RelicId == session.RelicId;
            return spiritShown ? _spirit.SpiritName : TitleOf(session.RelicId);
        }

        private void ConfirmResult()
        {
            // 결과를 확인하면 그 유물의 상세로 돌아간다. 최초 정화를 마쳤으면 정령 상세가 열린다.
            SettlementData settlement = _settlement;
            _settlement = null;
            if (settlement == null) ShowHome();
            else OpenDetail(settlement.RelicId);
        }
        
        /// <summary>개발용. Play 중 이 컴포넌트를 우클릭해 다음 보고의 응답 유실을 흉내 낸다(모의 백엔드일 때만).</summary>
        [ContextMenu("Dev/다음 보고 응답 유실")]
        private void DropNextReportResponse()
        {
            if (_backend is MockWorkbenchBackend mock)
            {
                mock.DropReportResponses += 1;
                Debug.Log("[WorkbenchScreenController] 다음 응답을 유실시킵니다.");
                return;
            }
            
            Debug.LogWarning("[WorkbenchScreenController] 실제 서버 백엔드에서는 응답 유실 흉내를 낼 수 없습니다.");
        }

        // ---------- 정령 상세 ----------

        private void SelectDifficulty(MinigameDifficulty difficulty)
        {
            if (_spirit == null || CurrentPage != Page.SpiritDetail) return;
            _difficulty = difficulty;
            _detail.RenderSpirit(_spirit, _difficulty);
        }

        /// <summary>계획이 허용하는 난이도 중 이전에 고른 것을, 없으면 보통을, 그것도 없으면 첫 번째를 고른다.</summary>
        public static MinigameDifficulty ChooseDifficulty(SpiritDetailData spirit, MinigameDifficulty previous)
        {
            if (spirit.Difficulties.Count == 0) return previous;
            if (spirit.Difficulties.Any(option => option.Difficulty == previous)) return previous;
            if (spirit.Difficulties.Any(option => option.Difficulty == MinigameDifficulty.Normal)) return MinigameDifficulty.Normal;
            return spirit.Difficulties[0].Difficulty;
        }

        private void OpenRename()
        {
            if (_spirit == null || Busy) return;
            _detail.ShowRenameSheet(_spirit.SpiritName, DefaultSpiritNameOf(_relicId));
        }

        private void Rename(string name)
        {
            if (Busy) return;
            // 서버에 보내기 전에 같은 규칙으로 먼저 걸러 바로 알려 준다(금칙어는 서버만 검사).
            if (!SpiritNameRule.TryNormalize(name, out _, out string error))
            {
                _detail.ShowStatus(error);
                return;
            }

            Busy = true;
            int relicId = _relicId;
            int sequence = _viewSequence;
            _detail.ShowStatus("저장하는 중…");
            Request<string>((done, fail) => _backend.RenameSpirit(relicId, name, done, fail),
                saved =>
                {
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    _pendingDetailStatus = $"이름을 '{saved}'(으)로 저장했어요.";
                    OpenDetail(relicId);
                },
                error2 =>
                {
                    Busy = false;
                    if (sequence != _viewSequence) return;
                    _detail.ShowStatus(error2);
                });
        }

        // ---------- 공통 ----------

        /// <summary>뒤로가기·홈으로. 명령이 진행 중이면 응답을 받을 화면을 지키기 위해 무시한다.</summary>
        private void OnBack()
        {
            if (Busy) return;
            ShowHome();
        }

        /// <param name="keepSequence">조회 응답이 같은 상세 안에서 유물/정령 화면을 고를 때만 true. 그 외 화면 전환은 이전 요청을 무효화한다.</param>
        private void SetPage(Page page, bool keepSequence = false)
        {
            if (!keepSequence) _viewSequence++;
            CurrentPage = page;
            // 정화 중·결과·재전송 화면에서는 탭을 넘기지 못하게 한다(보고 전에 떠나면 판이 남는다).
            if (_root) _root.SetNavigationLocked(page == Page.Playing || page == Page.Result || page == Page.ReportRetry);
        }

        private static string DefaultSpiritNameOf(int relicId) =>
            WorkbenchRelicSeed.Instance.TryFind(relicId, out WorkbenchRelicInfo info) ? info.DefaultSpiritName : "기본 이름";

        private string TitleOf(int relicId) =>
            WorkbenchRelicSeed.Instance.TryFind(relicId, out WorkbenchRelicInfo info) ? info.PoeticAlias : WorkbenchText.UnknownRelic;

        private void ShowPendingStatus()
        {
            if (_pendingStatus == null) return;
            _home.ShowStatus(_pendingStatus);
            _pendingStatus = null;
        }

        /// <summary>
        /// 백엔드 코루틴을 이 컴포넌트에서 돌린다. 시작한 뒤 화면이 숨겨졌으면 늦게 온 콜백을 버린다
        /// (친구 탭의 늦은 응답 세대 검사와 같은 방식).
        /// </summary>
        private void Request<T>(Func<Action<T>, Action<string>, IEnumerator> call, Action<T> completed, Action<string> failed)
        {
            int lifetime = _lifetime;
            StartCoroutine(call(
                value => { if (lifetime == _lifetime) completed(value); },
                error => { if (lifetime == _lifetime) failed(error); }));
        }

        private RectTransform CreateCanvas()
        {
            Canvas canvas = new GameObject("Workbench Screen UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(_root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _root.sceneCamera;
            canvas.planeDistance = 5;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1600);
            scaler.matchWidthOrHeight = 0;
            return (RectTransform)canvas.transform;
        }

        // 실행 중에 만든 UI도 콘텐츠 씬의 렌더 레이어를 따라야 탭 카메라에 보인다.
        private void ApplyLayer() => MirrorSceneRoot.SetLayer(_canvas, _root.gameObject.layer);
    }
}
