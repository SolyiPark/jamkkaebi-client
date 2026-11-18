using System;
using System.Collections;
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
    /// 화면 조회(홈·상세)는 가장 마지막에 시작한 것만 그린다(화면 번호). 상자 열기 같은 명령은 조회를 버리지 않는다.
    /// </summary>
    public sealed class WorkbenchScreenController : MonoBehaviour
    {
        public enum Page
        {
            Home = 0,
            RelicDetail = 1,
            Playing = 2,
            Result = 3,
            ReportRetry = 4
        }

        private MirrorSceneRoot _root;
        private RectTransform _canvas;
        private IWorkbenchBackend _backend;
        private WorkbenchViewDataBuilder _builder;
        private WorkbenchHomeView _home;
        private WorkbenchDetailView _detail;
        private int _lifetime;         // 숨길 때마다 올린다. 이전 수명에서 시작한 요청의 응답은 버린다.
        private int _viewSequence;     // 화면 조회마다 올린다. 더 새 조회가 있으면 이전 조회 응답은 버린다.
        private string _pendingStatus; // 다음에 홈을 그린 뒤 안내 자리에 보일 문장(상자 결과 등)
        private bool _initialized;

        private int _relicId;                  // 상세·정화 중인 유물
        private PurificationSession _session;  // 시작한 판
        private PlayResult _reported;          // 보고한 결과(실패 시 같은 내용으로 다시 보낸다)
        private SettlementData _settlement;

        /// <summary>상자 열기·정화 시작·보고처럼 상태를 바꾸는 요청이 진행 중인지. 진행 중에는 버튼을 다시 받지 않는다.</summary>
        public bool Busy { get; private set; }

        public Page CurrentPage { get; private set; } = Page.Home;
        public WorkbenchHomeView Home => _home;
        public WorkbenchDetailView Detail => _detail;
        public IWorkbenchBackend Backend => _backend;

        /// <summary>화면을 만든다. 씬의 WorkbenchPresenter가 Awake에서 부른다.</summary>
        public void Initialize(MirrorSceneRoot root, Font font, IWorkbenchBackend backend = null)
        {
            if (_initialized) return;
            _root = root ? root : throw new ArgumentNullException(nameof(root));
            if (!font) throw new ArgumentNullException(nameof(font));

            _backend = backend ?? WorkbenchDemo.CreateBackend(() => DateTime.UtcNow);
            _builder = new WorkbenchViewDataBuilder(WorkbenchRelicSeed.Instance, IntimacyPolicy.Default, RestorePolicy.Default);
            _canvas = CreateCanvas();

            _home = new WorkbenchHomeView(_canvas, font);
            _home.Rebuilt += ApplyLayer;
            _home.RegisterClicked += Register;
            _home.SlotSelected += OpenDetail;
            _home.SpiritSelected += OnSpiritSelected;

            _detail = new WorkbenchDetailView(_canvas, font);
            _detail.Rebuilt += ApplyLayer;
            _detail.BackClicked += ShowHome;
            _detail.PurifyClicked += StartPurification;
            _detail.ReportClicked += OnReportClicked;
            _detail.QuitPlayClicked += QuitPlay;
            _detail.ConfirmClicked += ConfirmResult;
            _detail.RetryClicked += RetryReport;

            _initialized = true;
            ShowHome();
        }

        private void OnEnable()
        {
            if (!_initialized) return;
            // 숨겼다가 다시 보이면(PauseWhileHidden) 그동안 바뀌었을 수 있으니 보던 화면을 새로 조회한다.
            // 정화 중·결과·재전송 화면은 그대로 둔다(화면 오브젝트는 숨김 동안에도 남아 있다).
            if (CurrentPage == Page.Home) ShowHome();
            else if (CurrentPage == Page.RelicDetail) OpenDetail(_relicId);
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
                _home.SpiritSelected -= OnSpiritSelected;
            }
            if (_detail != null)
            {
                _detail.Rebuilt -= ApplyLayer;
                _detail.BackClicked -= ShowHome;
                _detail.PurifyClicked -= StartPurification;
                _detail.ReportClicked -= OnReportClicked;
                _detail.QuitPlayClicked -= QuitPlay;
                _detail.ConfirmClicked -= ConfirmResult;
                _detail.RetryClicked -= RetryReport;
            }
            if (_root) _root.SetNavigationLocked(false);
        }

        // ---------- 홈 ----------

        /// <summary>작업대를 다시 조회해 홈을 그린다.</summary>
        public void ShowHome()
        {
            SetPage(Page.Home);
            if (!isActiveAndEnabled) return;
            int sequence = ++_viewSequence;
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
            _home.ShowStatus("상자를 여는 중…");
            string requestId = Guid.NewGuid().ToString();
            Request<BoxOpenOutcome>((done, fail) => _backend.OpenBox(BoxSource.Free, requestId, done, fail),
                outcome =>
                {
                    Busy = false;
                    _pendingStatus = _builder.BoxResult(outcome);
                    if (CurrentPage == Page.Home) ShowHome();
                },
                error =>
                {
                    // 실패해도 화면이 지금 상태와 어긋나 있을 수 있으니 다시 조회하고 이유를 보인다.
                    // 모의 백엔드의 실패는 규칙 위반(가득 참 등)이라 같은 번호로 다시 보내도 결과가 같다.
                    // 네트워크 재전송(같은 requestId)은 서버 연결 때 붙인다.
                    Busy = false;
                    _pendingStatus = error;
                    if (CurrentPage == Page.Home) ShowHome();
                });
        }

        private void OnSpiritSelected(int relicId)
        {
            // 정령 상세는 3-6에서 연결한다.
            _home.ShowStatus("정령 상세는 준비 중이에요.");
        }

        // ---------- 유물 상세 ----------

        /// <summary>유물 상세를 조회해 그린다. 최초 정화를 마친 유물이면 홈으로 돌아간다(정령 상세는 3-6).</summary>
        public void OpenDetail(int relicId)
        {
            _relicId = relicId;
            SetPage(Page.RelicDetail);
            if (!isActiveAndEnabled) return;
            int sequence = ++_viewSequence;
            _detail.RenderLoading();
            Request<RelicDetail>((done, fail) => _backend.GetRelicDetail(relicId, done, fail),
                detail =>
                {
                    if (sequence != _viewSequence) return;
                    if (detail.Status == RelicStatus.InProgress)
                    {
                        _detail.RenderRelic(_builder.RelicDetail(detail));
                        return;
                    }
                    _pendingStatus = "최초 정화를 마친 정령이에요. 정령 상세는 준비 중이에요.";
                    ShowHome();
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
            _detail.ShowStatus("정화를 준비하는 중…");
            // 최초 정화는 난이도가 정해져 있어 비워 보낸다(관리 플레이의 난이도 선택은 3-6).
            Request<PurificationSession>((done, fail) => _backend.StartPurification(relicId, null, done, fail),
                session =>
                {
                    Busy = false;
                    _session = session;
                    SetPage(Page.Playing);
                    _detail.RenderPlaying(WorkbenchText.PhaseName(session.Phase),
                        $"{TitleOf(relicId)}\n난이도 {WorkbenchText.DifficultyName(session.Difficulty)}");
                },
                error =>
                {
                    Busy = false;
                    _detail.ShowStatus(error);
                });
        }

        // ---------- 정화 중·결과 ----------

        private void OnReportClicked(bool success) => Report(success ? PlayResult.Success : PlayResult.Failure);

        private void Report(PlayResult result)
        {
            if (Busy || _session == null) return;
            Busy = true;
            _reported = result;
            string token = _session.SessionToken;
            int relicId = _session.RelicId;
            _detail.ShowStatus("결과를 보내는 중…");
            Request<SettlementResult>((done, fail) => _backend.ReportPurification(token, result, done, fail),
                settlement =>
                {
                    Busy = false;
                    _session = null;
                    _settlement = _builder.Settlement(relicId, settlement);
                    SetPage(Page.Result);
                    _detail.RenderResult(_settlement);
                },
                error =>
                {
                    // 보고는 같은 토큰·같은 결과로 다시 보내도 한 번만 정산된다(서버와 모의 백엔드 모두).
                    Busy = false;
                    SetPage(Page.ReportRetry);
                    _detail.RenderRetry("결과를 보내지 못했어요.\n" + error);
                });
        }

        private void RetryReport()
        {
            if (_session == null) return;
            SetPage(Page.Playing);
            Report(_reported);
        }

        /// <summary>그만두면 이번 판은 보고하지 않는다. 같은 유물로 새 판을 시작하면 서버가 이 판을 대체한다.</summary>
        private void QuitPlay()
        {
            _session = null;
            OpenDetail(_relicId);
        }

        private void ConfirmResult()
        {
            SettlementData settlement = _settlement;
            _settlement = null;
            if (settlement == null || settlement.Finished) ShowHome();
            else OpenDetail(settlement.RelicId);
        }

        // ---------- 공통 ----------

        private void SetPage(Page page)
        {
            CurrentPage = page;
            // 정화 중·결과·재전송 화면에서는 탭을 넘기지 못하게 한다(보고 전에 떠나면 판이 남는다).
            if (_root) _root.SetNavigationLocked(page == Page.Playing || page == Page.Result || page == Page.ReportRetry);
        }

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
