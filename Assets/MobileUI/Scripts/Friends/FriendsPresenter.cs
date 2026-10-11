using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using MobilePrototype.Collection;
using MobilePrototype.Exhibition;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

namespace MobilePrototype.Friends
{
    /// <summary>PDF의 친구 화면을 기존 카메라·입력 브리지 안에서 구성하는 로컬 프론트엔드입니다.</summary>
    [RequireComponent(typeof(MirrorSceneRoot))]
    public sealed class FriendsPresenter : MonoBehaviour
    {
        [SerializeField] private Font _font;
        [SerializeField] private ExhibitionGrid _visitGrid;
        [SerializeField] private ExhibitionPreviewScenario _visitScenario;
        [SerializeField] private string _serverBaseUrl = "";
        private JamkkaebiApiClient _server;
        private Coroutine _socialRequest;
        private bool _serverBusy;
        private string _lookupRelation;
        public bool UsesServer => _server != null;
        public bool ServerBusy => _serverBusy;
        private Coroutine _visitRequest;
        private Coroutine _rewardRequest;
        private bool _claimingReward;
        private float _nextWalk;
        private int _walkStep;
        private readonly Dictionary<string, RectTransform> _visitViews = new Dictionary<string, RectTransform>();
        public ExhibitionVisitSession VisitSession { get; } = new ExhibitionVisitSession();
        public ExhibitionPreviewScenario PreviewScenario { get => _visitScenario; set => _visitScenario = value; }
        private static readonly Color Paper = new Color(.98f, .98f, .96f);
        private static readonly Color Ink = new Color(.12f, .13f, .12f);
        private static readonly Color Muted = new Color(.43f, .45f, .42f);
        private static readonly Color Green = new Color(.25f, .48f, .42f);
        private static readonly Color Pale = new Color(.85f, .9f, .87f);
        private static readonly Color Line = new Color(.86f, .87f, .84f);
        private const float Width = 952;
        private MirrorSceneRoot _root;
        private RectTransform _canvas;
        private RectTransform _content;
        private Text _toast;
        private float _toastUntil;
        private bool _sentRequests;
        private string _code = "";
        private FriendProfile _lookup;
        private FriendProfile _visitor;
        private string _selectedObject = "달이";
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        public FriendsPreviewState State { get; private set; }
        public string Page { get; private set; } = "Friends";
        public CollectionScrollRect Scroll { get; private set; }
        public IReadOnlyDictionary<string, Button> Buttons => _buttons;
        public InputField CodeInput { get; private set; }

        /// <summary>친구 UI의 필수 참조를 검증하고 모의 화면 또는 설정된 서버 연결을 초기화합니다.</summary>
        private void Awake()
        {
            _root = GetComponent<MirrorSceneRoot>();
            if (!_font || !_root.sceneCamera || !_visitGrid || !_visitGrid.IsValid || _visitGrid.Size != new Vector2Int(7, 7))
            { Debug.LogError("친구 화면의 글꼴·카메라·7×7 격자 참조가 필요합니다.", this); enabled = false; return; }
            State = new FriendsPreviewState(DateTime.Now);
            var canvas = new GameObject("Friends UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _root.sceneCamera; canvas.planeDistance = 5;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1600); scaler.matchWidthOrHeight = 0;
            _canvas = (RectTransform)canvas.transform;
            UIFactory.Image("Paper", _canvas, Paper, Vector2.zero, Vector2.one);
            _root.uiRaycasters = new[] { canvas.GetComponent<GraphicRaycaster>() };
            Render();
            string baseUrl = string.IsNullOrWhiteSpace(_serverBaseUrl) ? Environment.GetEnvironmentVariable("JAMKKAEBI_SERVER_URL") : _serverBaseUrl;
            if (!string.IsNullOrWhiteSpace(baseUrl)) ConnectServer(new JamkkaebiApiClient(baseUrl,
                Environment.GetEnvironmentVariable("JAMKKAEBI_ACCESS_TOKEN"), Environment.GetEnvironmentVariable("JAMKKAEBI_REFRESH_TOKEN")));
        }

        /// <summary>진행 중인 방문·소셜 요청을 취소하고 새 인증 클라이언트로 친구 상태를 조회합니다.</summary>
        public void ConnectServer(JamkkaebiApiClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            CancelVisit();
            if (_socialRequest != null) StopCoroutine(_socialRequest);
            _socialRequest = null; _serverBusy = false;
            _server = client;
            State.ClearForServer(); _lookup = null; Page = "Friends"; Render();
            StartSocialRequest(ReloadServer());
        }
        /// <summary>서버 모드에서 다른 소셜 요청이 없을 때 최신 친구 상태를 다시 조회합니다.</summary>
        public void RefreshServer() { if (UsesServer && !_serverBusy) StartSocialRequest(ReloadServer()); }
        /// <summary>소셜 요청의 최상위 코루틴을 시작하고 취소용 핸들을 보관합니다.</summary>
        private void StartSocialRequest(IEnumerator request)
        { _socialRequest = StartCoroutine(RunSocialRequest(request)); }
        /// <summary>중첩 조회·알림까지 요청 소유권을 유지하고 최상위 요청 종료 시 핸들을 해제합니다.</summary>
        private IEnumerator RunSocialRequest(IEnumerator request)
        {
            _serverBusy = true;
            try { yield return request; }
            finally { _serverBusy = false; _socialRequest = null; }
        }
        /// <summary>친구·요청·추천·차단 응답을 모두 받은 뒤 화면 상태를 한 번에 교체합니다.</summary>
        private IEnumerator ReloadServer()
        {
            string[] paths = { "/api/friends", "/api/friend-requests?direction=RECEIVED", "/api/friend-requests?direction=SENT", "/api/friends/recommendations", "/api/blocks" };
            var data = new JToken[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                ServerApiResult result = null;
                yield return _server.Send("GET", paths[i], null, value => result = value);
                if (!result.Success) { Render(); Notify(result.Code + ": " + result.Message); yield break; }
                data[i] = result.Data;
            }
            FriendsPreviewState mapped = null;
            try { mapped = FriendsServerMapper.Create(data[0], data[1], data[2], data[3], data[4]); }
            catch (Exception) { Render(); Notify("친구 응답 형식이 올바르지 않아요."); }
            if (mapped != null) { State = mapped; Render(); }
        }
        /// <summary>중복 변경을 차단하고 서버 상태 변경 요청을 최상위 소셜 작업으로 시작합니다.</summary>
        private void ChangeServer(string method, string path, JObject body = null)
        {
            if (_serverBusy) return;
            StartSocialRequest(ChangeServerRoutine(method, path, body));
        }
        /// <summary>상태 변경 성공 후 목록을 다시 읽고 결과를 알립니다. 실패한 변경은 자동 재시도하지 않습니다.</summary>
        private IEnumerator ChangeServerRoutine(string method, string path, JObject body)
        {
            ServerApiResult result = null;
            yield return _server.Send(method, path, body, value => result = value);
            // 재화 요청은 네트워크 실패 시 자동 재시도하지 않습니다. 성공 후 서버 상태를 다시 읽습니다.
            if (result.Success)
            {
                if (Page == "Visit") Show("Friends");
                _lookup = null;
                yield return ReloadServer();
                Notify(result.Message + (result.Data?["jeongseongGained"] != null ? " (정성 +" + (int)result.Data["jeongseongGained"] + ")" : ""));
            }
            else { Render(); Notify(result.Code + ": " + result.Message); }
        }
        /// <summary>현재 모드에 따라 친구 요청을 서버에 보내거나 모의 상태에 반영합니다.</summary>
        private void RequestFriend(FriendProfile profile)
        {
            if (UsesServer) { ChangeServer("POST", "/api/friend-requests", new JObject { ["friendCode"] = FriendsPreviewState.NormalizeCode(profile.Code) }); return; }
            bool sent = State.Request(profile); Render(); Notify(sent ? "친구 요청을 보냈어요." : "요청을 보낼 수 없어요.");
        }
        /// <summary>받은 요청 수락·거절 또는 보낸 요청 취소를 현재 모드의 상태에 반영합니다.</summary>
        private void ActOnRequest(FriendProfile profile, string action)
        {
            if (UsesServer)
            {
                ChangeServer(action == "cancel" ? "DELETE" : "POST", "/api/friend-requests/" + JamkkaebiApiClient.CodePath(profile.Code) + (action == "cancel" ? "" : "/" + action)); return;
            }
            if (action == "cancel") State.Sent.Remove(profile);
            else if (action == "reject") State.Received.Remove(profile);
            else State.Accept(profile);
            Render();
        }
        /// <summary>친구를 차단하고 모의 모드에서는 친구 목록 화면으로 돌아갑니다.</summary>
        private void BlockFriend(FriendProfile profile)
        {
            if (UsesServer) { ChangeServer("POST", "/api/blocks", new JObject { ["friendCode"] = FriendsPreviewState.NormalizeCode(profile.Code) }); return; }
            State.Block(profile); Show("Friends");
        }
        /// <summary>차단을 해제하되 이전 친구 관계를 자동으로 복원하지 않습니다.</summary>
        private void UnblockFriend(FriendProfile profile)
        {
            if (UsesServer) { ChangeServer("DELETE", "/api/blocks/" + JamkkaebiApiClient.CodePath(profile.Code)); return; }
            State.Blocked.Remove(profile); Render(); Notify("차단을 해제했어요.");
        }
        /// <summary>친구 코드를 조회하고 입력이나 화면이 바뀌지 않았을 때만 검색 결과를 표시합니다.</summary>
        private IEnumerator LookupServer(string code)
        {
            ServerApiResult result = null;
            yield return _server.Send("GET", "/api/friends/lookup/" + JamkkaebiApiClient.CodePath(code), null, value => result = value);
            if (Page != "Add" || FriendsPreviewState.NormalizeCode(_code) != FriendsPreviewState.NormalizeCode(code)) yield break;
            _lookup = null;
            if (result.Success)
            {
                try { _lookup = FriendsServerMapper.Lookup(result.Data, State); _lookupRelation = (string)result.Data["relation"]; }
                catch (Exception) { Notify("친구 조회 응답 형식이 올바르지 않아요."); yield break; }
                Render();
            }
            else { Render(); Notify(result.Code + ": " + result.Message); }
        }

        /// <summary>알림 수명을 처리하고 모의 일일 초기화와 방문 NPC의 임시 이동을 갱신합니다.</summary>
        private void Update()
        {
            if (_toast && Time.unscaledTime > _toastUntil)
            {
                _toast.gameObject.SetActive(false);
                _toast.transform.parent.GetComponent<Image>().color = Color.clear;
            }
            if (State != null && State.RefreshDay(DateTime.Now)) Render();
            if (Page == "Visit" && VisitSession.Scene != null && !VisitSession.Loading && Time.unscaledTime >= _nextWalk)
            {
                _nextWalk = Time.unscaledTime + 1;
                VisitSession.Scene.Walk(_walkStep++); PositionVisitObjects();
            }
        }

        /// <summary>요청한 내부 페이지를 표시하고 방문 페이지를 떠날 때 진행 중인 방문을 취소합니다.</summary>
        public void Show(string page)
        {
            if (page != "Visit") CancelVisit();
            Page = page; Render();
        }

        /// <summary>방문 조회·보상 코루틴을 취소하고 응답 세대와 방문 화면 데이터를 초기화합니다.</summary>
        private void CancelVisit()
        {
            if (_visitRequest != null) StopCoroutine(_visitRequest);
            if (_rewardRequest != null) StopCoroutine(_rewardRequest);
            _visitRequest = null; _rewardRequest = null; _claimingReward = false;
            VisitSession.Leave(); _visitViews.Clear();
        }
        /// <summary>종료 시 진행 중인 방문과 소셜 요청을 취소합니다.</summary>
        private void OnDestroy() { CancelVisit(); if (_socialRequest != null) StopCoroutine(_socialRequest); }

        /// <summary>현재 친구 목록의 프로필에 대한 새 방문 세대를 시작하고 스냅샷을 조회합니다.</summary>
        public void Visit(FriendProfile profile)
        {
            if (!State.Friends.Contains(profile)) return;
            if (_rewardRequest != null) StopCoroutine(_rewardRequest);
            _rewardRequest = null; _claimingReward = false;
            if (_visitRequest != null) StopCoroutine(_visitRequest);
            _visitor = profile; _selectedObject = "";
            int revision = VisitSession.Begin(profile.Code);
            Show("Visit");
            _visitRequest = StartCoroutine(LoadVisit(profile, revision));
        }

        /// <summary>스냅샷을 수신해 유효한 요청 세대에만 적용하고 로딩 또는 오류 화면을 갱신합니다.</summary>
        private IEnumerator LoadVisit(FriendProfile profile, int revision)
        {
            IExhibitionBackend backend = UsesServer ? new ServerExhibitionBackend(_server) : new MockExhibitionBackend(_visitScenario);
            yield return backend.GetExhibition(profile.Code, snapshot =>
            {
                if (VisitSession.Complete(revision, snapshot))
                {
                    _visitor = profile;
                    _selectedObject = VisitSession.Scene.Objects.FirstOrDefault()?.Placement.Key;
                    _walkStep = 0; _nextWalk = Time.unscaledTime + 1;
                }
                if (Page == "Visit") Render();
            }, error => { VisitSession.Fail(revision, error); if (Page == "Visit") Render(); });
            _visitRequest = null;
        }

        /// <summary>전시 로딩·중복 수령을 차단하고 방문 보상을 별도 명령으로 요청합니다.</summary>
        private void ClaimReward(FriendProfile profile)
        {
            if (_claimingReward || VisitSession.Loading || VisitSession.Scene == null || State.VisitClaimed) return;
            if (UsesServer) { ChangeServer("POST", "/api/friends/" + JamkkaebiApiClient.CodePath(profile.Code) + "/visit-reward"); return; }
            _claimingReward = true; Render();
            _rewardRequest = StartCoroutine(LoadReward(profile));
        }
        /// <summary>방문 보상 결과를 화면에 알리고 보상 요청의 진행 상태를 해제합니다.</summary>
        private IEnumerator LoadReward(FriendProfile profile)
        {
            IExhibitionBackend backend = UsesServer ? new ServerExhibitionBackend(_server) : new MockExhibitionBackend(_visitScenario, State);
            yield return backend.ClaimVisitReward(profile.Code, gained =>
            {
                _claimingReward = false; Render();
                Notify(gained > 0 ? "방문 보상을 받았어요. (미리보기)" : "오늘의 방문 보상을 이미 받았어요.");
            }, error => { _claimingReward = false; Render(); Notify(error); });
            _rewardRequest = null;
        }

        /// <summary>기존 내부 UI를 제거하고 현재 페이지를 다시 구성하며 미러 입력과 레이어를 연결합니다.</summary>
        private void Render()
        {
            if (Scroll) Scroll.StopMovement();
            for (int i = _canvas.childCount - 1; i >= 1; i--)
            {
                var child = _canvas.GetChild(i).gameObject; child.SetActive(false); Destroy(child);
            }
            _buttons.Clear(); CodeInput = null;
            _visitViews.Clear();
            _content = MakeScroll(_canvas, 24, 0);
            if (Page == "Visit") BuildVisit();
            else if (Page == "Blocked") BuildBlocked();
            else
            {
                BuildHeader();
                if (Page == "Requests") BuildRequests();
                else if (Page == "Recommendations") BuildRecommendations();
                else BuildFriends();
                if (Page == "Add") BuildAddSheet();
            }
            var toastBackground = Box("Toast", _canvas, Green, 90, 12, 900, 64);
            _toast = Label("Feedback", toastBackground.transform, "", 27, Color.white, 10, 0, 880, 64, TextAnchor.MiddleCenter);
            toastBackground.color = Color.clear;
            _toast.gameObject.SetActive(false);
            MirrorSceneRoot.SetLayer(_canvas, gameObject.layer);
            Canvas.ForceUpdateCanvases(); Scroll.verticalNormalizedPosition = 1;
        }

        /// <summary>친구 화면용 세로 스크롤을 만들고 가로 드래그는 공통 탭 전환에 맡깁니다.</summary>
        private RectTransform MakeScroll(Transform parent, float bottom, float top)
        {
            var viewport = UIFactory.Rect("Friends Scroll", parent, Vector2.zero, Vector2.one,
                new Vector2(64, bottom), new Vector2(-64, -top));
            var hit = viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            Scroll = viewport.gameObject.AddComponent<CollectionScrollRect>();
            Scroll.horizontal = false; Scroll.vertical = true; Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = 35; Scroll.decelerationRate = .08f;
            var body = UIFactory.Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            body.pivot = new Vector2(.5f, 1); body.sizeDelta = new Vector2(0, 1550);
            Scroll.viewport = viewport; Scroll.content = body; return body;
        }

        /// <summary>친구 코드·목록 수·내부 탭과 서버 새로고침 조작을 구성합니다.</summary>
        private void BuildHeader()
        {
            Label("Title", _content, UsesServer ? "친구 · 서버" : "친구 · 모의", 44, Ink, 0, 44, 450, 62);
            if (UsesServer) Button("ServerRefresh", _content, "새로고침", 480, 40, 160, 68, RefreshServer, false);
            Label("Count", _content, $"친구 {State.Friends.Count} / {State.FriendLimit}", 28, Muted, 650, 44, 302, 62, TextAnchor.MiddleRight);
            Label("Introduction", _content, "서로의 전시를 둘러보는 곳", 27, Muted, 0, 112, Width, 45);
            Box("Accent", _content, Green, 0, 178, 60, 4);
            var codeCard = Box("My Code Card", _content, Color.white, 0, 222, Width, 144);
            Border(codeCard);
            Label("Code Label", codeCard.transform, "내 친구 코드", 25, Muted, 32, 24, 390, 40);
            Label("Own Code", codeCard.transform, State.MyCode, 36, Ink, 32, 69, 470, 52);
            Button("Copy", codeCard.transform, "복사", 562, 29, 105, 88, () => CopyCode(), false);
            Button("Add", codeCard.transform, "+ 코드로 추가", 692, 29, 226, 88, () => Show("Add"));
            string[] pages = { "Friends", "Requests", "Recommendations" };
            string[] captions = { $"친구 {State.Friends.Count}", $"요청함 {State.Received.Count}", "오늘의 추천" };
            for (int i = 0; i < 3; i++)
            {
                string page = pages[i];
                var tab = Button("Tab_" + page, _content, captions[i], i * Width / 3, 404, Width / 3, 90, () => Show(page), false, Page == page ? Ink : Muted);
                tab.GetComponent<Outline>().enabled = false;
                Box("Tab Rule", _content, Page == page ? Green : Line, i * Width / 3, 495, Width / 3, Page == page ? 4 : 2);
            }
        }

        /// <summary>일일 행동 현황과 친구 목록·차단 목록 진입 버튼을 표시합니다.</summary>
        private void BuildFriends()
        {
            string[] titles = { "방문 보상", "상자 선물", "받은 선물" };
            string[] values = { UsesServer ? (State.VisitClaimed ? "보상 받음" : "보상 대기") : State.VisitClaimed ? "정성 +5 받음" : "정성 +5 대기", UsesServer ? (State.GiftRecipient != null ? "선물 보냄" : "선물 대기") : State.GiftRecipient != null ? "정성 +3 받음" : "정성 +3 대기", UsesServer ? (State.ReceivedGifts > 0 ? "상자권 있음" : "상자권 없음") : $"상자 +{State.ReceivedGifts} · 홈에서 열기" };
            for (int i = 0; i < 3; i++)
            {
                var cell = Box("Reward", _content, Color.white, i * Width / 3, 535, Width / 3, 118); Border(cell);
                Label("Name", cell.transform, titles[i], 23, Muted, 23, 18, 275, 35);
                Label("Value", cell.transform, values[i], 25, Green, 23, 61, 290, 36);
            }
            var remaining = (State.NextResetAt ?? State.Day.AddDays(1)) - (UsesServer ? DateTime.UtcNow.AddHours(9) : DateTime.Now);
            Label("Reset", _content, $"초기화까지 {Math.Max(0, (int)remaining.TotalHours)}시간 {Math.Max(0, remaining.Minutes)}분", 22, Muted, 400, 665, 552, 42, TextAnchor.MiddleRight);
            float y = 725;
            foreach (var profile in State.Friends)
            {
                FriendRow(profile, y, () => Visit(profile));
                string gift = profile.GiftFull ? "받기 가득" : (State.GiftRecipient == profile.Code || profile.GiftState == "SENT") ? "선물 보냄" : "선물";
                var button = Button("Gift_" + profile.Code, _content, gift, 778, y + 9, 174, 76,
                    () => Gift(profile), !profile.GiftFull);
                button.interactable = !profile.GiftFull && State.GiftRecipient == null && (!UsesServer || profile.GiftState == "AVAILABLE");
                y += 136;
            }
            Button("Blocked", _content, "차단 목록 관리", 0, y + 24, Width, 80, () => Show("Blocked"), false);
            Label("Preview", _content, "화면 미리보기 · 샘플 친구 기록", 22, Muted, 0, y + 113, Width, 40);
            Height(y + 170);
        }

        /// <summary>친구 프로필 행에 방문·선물 조작과 서버 또는 모의 상태에 따른 사용 가능 여부를 표시합니다.</summary>
        private void FriendRow(FriendProfile profile, float y, Action click)
        {
            var profileButton = Button("Profile_" + profile.Code, _content, "", 0, y, 755, 112, click, false);
            profileButton.GetComponent<Outline>().enabled = false;
            Avatar(_content, profile, 0, y, 88);
            Label("Name", _content, profile.Name, 31, Ink, 112, y + 2, 455, 45);
            Label("Stats", _content, $"<color=#B9483E>연속 {profile.Streak}일</color> · 수집률 {profile.CollectionPercent}% · {profile.LastSeen}", 24, Muted, 112, y + 49, 640, 46);
            if (State.Visited.Contains(profile.Code)) Label("Visited", _content, "방문함", 21, Green, 548, y + 4, 140, 38);
            Box("Divider", _content, Line, 0, y + 116, Width, 1);
        }

        /// <summary>받은·보낸 요청 목록과 수락·거절·취소 버튼을 구성합니다.</summary>
        private void BuildRequests()
        {
            Button("Received", _content, $"받은 요청 {State.Received.Count}/20", 0, 535, 275, 72, () => { _sentRequests = false; Render(); }, !_sentRequests);
            Button("Sent", _content, $"보낸 요청 {State.Sent.Count}/10", 293, 535, 275, 72, () => { _sentRequests = true; Render(); }, _sentRequests);
            Label("Expiry", _content, "요청은 14일이 지나면 자동으로 사라져요.", 25, Muted, 0, 626, Width, 46);
            float y = 712;
            foreach (var profile in _sentRequests ? State.Sent : State.Received)
            {
                Avatar(_content, profile, 0, y, 88);
                Label("Name", _content, profile.Name, 31, Ink, 112, y - 10, 475, 48);
                Label("Stats", _content, $"<color=#B9483E>연속 {profile.Streak}일</color> · 수집률 {profile.CollectionPercent}%", 25, Muted, 112, y + 40, 515, 43);
                Label("Time", _content, _sentRequests ? "요청 보냄 · 14일 동안 유지" : profile.LastSeen, 23, Muted, 112, y + 84, 525, 42);
                if (_sentRequests)
                    Button("Cancel_" + profile.Code, _content, "취소", 844, y + 12, 108, 72, () => ActOnRequest(profile, "cancel"), false);
                else
                {
                    Button("Reject_" + profile.Code, _content, "거절", 735, y + 12, 98, 72, () => ActOnRequest(profile, "reject"), false);
                    Button("Accept_" + profile.Code, _content, "수락", 844, y + 12, 108, 72, () => ActOnRequest(profile, "accept"));
                }
                Box("Divider", _content, Line, 0, y + 141, Width, 1); y += 178;
            }
            if ((_sentRequests ? State.Sent : State.Received).Count == 0) Label("Empty", _content, "아직 요청이 없어요.", 29, Muted, 0, 735, Width, 80, TextAnchor.MiddleCenter);
            Height(Math.Max(1150, y + 60));
        }

        /// <summary>추천 프로필과 시대별 복원 현황을 표시하고 중복 요청을 차단합니다.</summary>
        private void BuildRecommendations()
        {
            Label("Intro", _content, $"최근 접속한 사람 중 매일 3명을 골라 드려요 · 남은 추천 {State.RemainingRecommendations}", 25, Ink, 0, 525, Width, 54);
            Label("Reset", _content, "자정에 새로 채워짐", 22, Muted, 0, 585, Width, 38, TextAnchor.MiddleRight);
            float y = 644;
            foreach (var profile in State.Recommendations)
            {
                var card = Box("Recommendation", _content, Color.white, 0, y, Width, 300); Border(card);
                Avatar(card.transform, profile, 32, 32, 96);
                Label("Name", card.transform, profile.Name, 33, Ink, 154, 37, 430, 48);
                Label("Stats", card.transform, $"연속 {profile.Streak}일 · 수집률 {profile.CollectionPercent}%", 25, Muted, 154, 89, 455, 43);
                var button = Button("Recommend_" + profile.Code, card.transform, (State.Sent.Contains(profile) || profile.Requested) ? "요청 보냄" : State.Friends.Contains(profile) ? "친구" : "친구 요청", 720, 42, 200, 80,
                    () => RequestFriend(profile));
                button.interactable = !State.Sent.Contains(profile) && !profile.Requested && !State.Friends.Contains(profile) && !State.Blocked.Contains(profile);
                Restoration(card.transform, profile, 160); y += 326;
            }
            Height(y + 50);
        }

        /// <summary>코드 입력·조회·요청과 공유 준비 상태를 표시하는 추가 시트를 구성합니다.</summary>
        private void BuildAddSheet()
        {
            var overlay = UIFactory.Image("Sheet Shade", _canvas, new Color(0, 0, 0, .38f), Vector2.zero, Vector2.one).rectTransform;
            overlay.GetComponent<Image>().raycastTarget = true;
            var sheet = UIFactory.Image("Add Friend Sheet", overlay, Paper, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -180)).rectTransform;
            var body = MakeScroll(sheet, 24, 0);
            Box("Handle", body, Line, 435, 20, 82, 7);
            Label("Sheet Title", body, "친구 추가", 39, Ink, 0, 68, 640, 62);
            Button("CloseAdd", body, "×", 870, 64, 82, 70, () => Show("Friends"), false, Ink);
            Label("Code Label", body, "친구 코드", 26, Muted, 0, 151, Width, 43);
            var field = Box("Code Input", body, Color.white, 0, 206, 796, 96); Border(field, Ink);
            field.raycastTarget = true;
            CodeInput = field.gameObject.AddComponent<InputField>();
            var inputText = Label("Input Text", field.transform, _code, 36, Ink, 27, 12, 740, 72);
            CodeInput.textComponent = inputText; CodeInput.characterLimit = 32;
            CodeInput.contentType = InputField.ContentType.Standard; CodeInput.lineType = InputField.LineType.SingleLine;
            var placeholder = Label("Placeholder", field.transform, "H2RX-9KTA", 33, Muted, 27, 12, 740, 72);
            CodeInput.placeholder = placeholder; CodeInput.text = _code;
            CodeInput.onValueChanged.AddListener(value =>
            {
                _code = value; _lookup = null;
                if (_buttons.TryGetValue("Lookup", out var lookup)) lookup.interactable = FriendsPreviewState.IsValidCode(value);
                if (_buttons.TryGetValue("SendRequest", out var request)) request.interactable = false;
            });
            var lookupButton = Button("Lookup", body, "조회", 814, 206, 138, 96, Lookup);
            lookupButton.interactable = FriendsPreviewState.IsValidCode(_code);
            Label("Hint", body, "영문·숫자 8자리를 모두 입력하면 조회할 수 있어요.", 23, Muted, 0, 318, Width, 44);
            if (_lookup != null)
            {
                var card = Box("Lookup Result", body, Color.white, 0, 388, Width, 550); Border(card);
                Avatar(card.transform, _lookup, 35, 40, 120);
                Label("Name", card.transform, _lookup.Name, 35, Ink, 184, 36, 650, 52);
                Label("Code", card.transform, _lookup.Code, 26, Muted, 184, 94, 650, 42);
                Label("Stats", card.transform, $"연속 {_lookup.Streak}일 · 수집률 {_lookup.CollectionPercent}%", 25, Muted, 184, 139, 650, 42);
                Restoration(card.transform, _lookup, 214);
                Label("Request Hint", card.transform, "상대가 수락하면 친구가 돼요. 요청은 14일 동안 유지돼요.", 24, Muted, 35, 355, 883, 50);
                var profile = _lookup;
                bool friend = State.Friends.Contains(profile), sent = State.Sent.Contains(profile);
                var request = Button("SendRequest", card.transform, friend ? "이미 친구예요" : sent ? "요청 보냄" : "친구 요청 보내기", 35, 433, 882, 90,
                    () => RequestFriend(profile));
                request.interactable = !friend && !sent && (!UsesServer || (_lookupRelation != "SELF" && _lookupRelation != "FRIEND" && _lookupRelation != "REQUEST_SENT"));
            }
            else Label("Empty Search", body, UsesServer ? "친구 코드를 조회해 보세요." : "친구 코드를 조회해 보세요.\n샘플 코드: G2RY-4ABC", 28, Muted, 0, 423, Width, 145, TextAnchor.MiddleCenter);
            Box("Rule", body, Line, 0, 1095, Width, 2);
            Label("Share Hint", body, "내 코드를 알려 주세요", 25, Muted, 0, 1128, 510, 48);
            Label("Own Code", body, State.MyCode, 33, Ink, 540, 1128, 412, 48, TextAnchor.MiddleRight);
            Button("ShareCopy", body, "코드 복사", 0, 1205, 300, 86, CopyCode, false);
            Button("ShareLink", body, "링크 공유", 326, 1205, 300, 86, () => Notify("공유 링크는 서버 연결 후 사용할 수 있어요."), false);
            Button("ShareQR", body, "QR 코드", 652, 1205, 300, 86, () => Notify("QR 공유는 초대 링크가 정해진 뒤 연결할 예정이에요."), false);
            body.sizeDelta = new Vector2(0, 1340);
        }

        /// <summary>유효한 입력 코드만 조회하고 현재 모드의 검색 결과를 표시합니다.</summary>
        private void Lookup()
        {
            if (!FriendsPreviewState.IsValidCode(_code)) return;
            if (UsesServer) { if (!_serverBusy) StartSocialRequest(LookupServer(_code)); return; }
            _lookup = State.Find(_code); Render();
            if (_lookup == null) Notify(FriendsPreviewState.NormalizeCode(_code) == FriendsPreviewState.NormalizeCode(State.MyCode) ? "내 코드는 추가할 수 없어요." : "조회할 수 있는 친구가 없어요.");
        }

        /// <summary>방문 소유자·객체·선택 정보·오류와 보상·선물 조작을 스냅샷 상태에 맞게 표시합니다.</summary>
        private void BuildVisit()
        {
            var scene = VisitSession.Scene;
            var visibleProfile = scene == null ? _visitor : State.Friends.FirstOrDefault(p => p.Code == scene.Snapshot.OwnerCode) ?? _visitor;
            Button("VisitBack", _content, "‹", 0, 30, 70, 80, () => Show("Friends"), false, Ink);
            Avatar(_content, visibleProfile, 85, 33, 76);
            Label("Title", _content, visibleProfile.Name + "의 전시", 35, Ink, 187, 25, 650, 55);
            Label("Stats", _content, $"연속 {visibleProfile.Streak}일 · 수집률 {visibleProfile.CollectionPercent}%", 25, Muted, 187, 85, 650, 42);
            Button("VisitMenu", _content, "···", 872, 30, 80, 80, () => Confirm("이 친구를 차단할까요?\n차단을 해제해도 친구 관계는 돌아오지 않아요.", () => BlockFriend(visibleProfile)), false, Ink);
            Label("Snapshot", _content, scene == null ? "저장된 전시를 불러와요" : string.IsNullOrEmpty(scene.Snapshot.CapturedAt) ? "저장된 전시가 없어요" : scene.Snapshot.CapturedAt + " 때의 모습", 24, Muted, 0, 162, 680, 48);
            int spiritCount = scene?.Objects.Count(o => !o.Placement.IsBuilding) ?? 0;
            int buildingCount = scene?.Objects.Count(o => o.Placement.IsBuilding) ?? 0;
            Label("Objects", _content, $"도깨비 {spiritCount} · 건물 {buildingCount}", 24, Muted, 700, 162, 252, 48, TextAnchor.MiddleRight);
            var board = Box("Exhibition Preview", _content, Color.white, 0, 225, Width, 790); Border(board);
            if (scene != null)
            {
                // 보이지 않는 7×7 셀 중심을 기존 ExhibitionGrid의 두 축으로 변환합니다.
                foreach (var obj in scene.Objects)
                {
                    var placement = obj.Placement;
                    string key = placement.Key;
                    var view = Button("VisitObject_" + key, board.transform,
                        (obj.Unknown ? "? " : "") + placement.Name + (placement.IsBuilding ? "\nLv." + placement.Level : ""),
                        0, 0, placement.IsBuilding ? 230 : 138, placement.IsBuilding ? 120 : 95,
                        () => { _selectedObject = key; Render(); }, false);
                    view.GetComponent<Image>().color = obj.Unknown ? new Color(.88f, .81f, .65f) : placement.IsBuilding ? Pale : new Color(.55f, .7f, .65f);
                    if (key == _selectedObject) view.GetComponent<Outline>().effectColor = Ink;
                    _visitViews[key] = (RectTransform)view.transform;
                }
                PositionVisitObjects();
                if (scene.Objects.Count == 0) Label("Empty Exhibition", board.transform, "아직 전시된 도깨비와 건물이 없어요.", 29, Muted, 50, 340, Width - 100, 100, TextAnchor.MiddleCenter);
            }
            if (VisitSession.Loading)
                Label("Loading", board.transform, "전시를 불러오는 중이에요…", 29, Ink, 50, 30, Width - 100, 70, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(VisitSession.Error))
            {
                Label("Visit Error", board.transform, VisitSession.Error, 29, Ink, 50, 25, Width - 100, 130, TextAnchor.MiddleCenter);
                Button("VisitRetry", board.transform, "다시 시도", 330, 160, 292, 85, () => Visit(_visitor));
            }
            var detail = Box("Object Detail", _content, new Color(.95f, .95f, .93f), 0, 1040, Width, 120);
            var selected = scene?.Objects.FirstOrDefault(o => o.Placement.Key == _selectedObject);
            Label("Selected", detail.transform, selected == null ? "전시 객체를 선택해 보세요" : selected.Placement.Name + (selected.Placement.IsBuilding ? " · Lv." + selected.Placement.Level : " · " + StageLabel(selected.Placement.Stage)), 29, Ink, 28, 12, 880, 43);
            Label("Description", detail.transform, scene != null && scene.Warnings.Count > 0 ? scene.Warnings[0] : "친구의 저장된 배치 · 방문 중에는 편집할 수 없어요", 24, Muted, 28, 61, 880, 44);
            bool ready = scene != null && !VisitSession.Loading;
            var reward = Button("VisitReward", _content, UsesServer ? (State.VisitClaimed ? "방문 보상 받음" : "방문 보상 받기\n오늘 1회") : State.VisitClaimed ? "방문 보상 받음\n정성 +5" : "방문 보상 받기\n오늘 1회 · 정성 +5", 0, 1225, 468, 130,
                () => ClaimReward(visibleProfile));
            reward.interactable = ready && !_claimingReward && !State.VisitClaimed && (!UsesServer || scene.Snapshot.VisitRewardAvailable);
            var gift = Button("VisitGift", _content, State.GiftRecipient == visibleProfile.Code ? "상자 선물 보냄" : (UsesServer ? "상자 선물하기\n하루 1명" : "상자 선물하기\n하루 1명 · 정성 +3"), 484, 1225, 468, 130, () => Gift(visibleProfile), false);
            gift.interactable = ready && State.GiftRecipient == null && !visibleProfile.GiftFull && (!UsesServer || scene.Snapshot.GiftState == "AVAILABLE");
            Height(1400);
        }

        /// <summary>서버 각성 상태 식별자를 화면에 표시할 한국어 문구로 변환합니다.</summary>
        private static string StageLabel(string stage) => stage == "FULLY_AWAKENED" ? "완전각성" : stage == "AWAKENED_PLUS" ? "추가각성" : "각성";
        /// <summary>방문 객체의 논리 셀을 UI 좌표로 옮기고 바닥 접점에 따라 표시 순서를 정합니다.</summary>
        private void PositionVisitObjects()
        {
            var scene = VisitSession.Scene;
            if (scene == null) return;
            int index = 0;
            foreach (var obj in scene.Objects.OrderByDescending(o => scene.LocalPosition(o, _visitGrid).y).ThenBy(o => o.Placement.Key))
            {
                if (!_visitViews.TryGetValue(obj.Placement.Key, out var view)) continue;
                var position = scene.LocalPosition(obj, _visitGrid);
                view.anchoredPosition = new Vector2(Width / 2 + position.x * 200, -155 - (_visitGrid.Origin.y - position.y) * 220);
                view.SetSiblingIndex(index++);
            }
        }
        /// <summary>차단한 프로필과 해제 확인 조작을 표시합니다.</summary>
        private void BuildBlocked()
        {
            Button("BlockedBack", _content, "‹", 0, 36, 70, 82, () => Show("Friends"), false, Ink);
            Label("Title", _content, "차단 목록", 37, Ink, 85, 37, 650, 70);
            Label("Count", _content, State.Blocked.Count + "명", 28, Muted, 760, 37, 192, 70, TextAnchor.MiddleRight);
            var note = Box("Notice", _content, new Color(.95f, .95f, .93f), 0, 153, Width, 128);
            Label("Explanation", note.transform, "차단한 사람과는 서로 코드로 찾거나 친구 요청을 보낼 수 없어요. 해제해도 친구 관계는 돌아오지 않아요.", 25, Muted, 28, 14, 896, 102);
            float y = 325;
            foreach (var profile in State.Blocked)
            {
                Avatar(_content, profile, 0, y, 88);
                Label("Name", _content, profile.Name, 32, Ink, 112, y, 640, 46);
                Label("Code", _content, profile.Code + " · " + State.BlockLabel(profile), 25, Muted, 112, y + 51, 640, 44);
                Button("Unblock_" + profile.Code, _content, "해제", 846, y + 10, 106, 72, () => Confirm("차단을 해제할까요?\n친구 관계는 자동으로 복구되지 않아요.", () => UnblockFriend(profile)), false);
                Box("Divider", _content, Line, 0, y + 119, Width, 1); y += 147;
            }
            if (State.Blocked.Count == 0) Label("Empty", _content, "차단한 사람이 없어요.", 29, Muted, 0, y, Width, 80, TextAnchor.MiddleCenter);
            Height(Math.Max(1250, y + 90));
        }

        /// <summary>선물 확인 창을 열고 현재 모드의 일일 선물 명령을 실행합니다.</summary>
        private void Gift(FriendProfile profile)
        {
            Confirm(profile.Name + "님에게 상자를 선물할까요?\n오늘은 한 명에게 보낼 수 있어요.", () =>
            {
                if (UsesServer) { ChangeServer("POST", "/api/friends/" + JamkkaebiApiClient.CodePath(profile.Code) + "/gift"); return; }
                bool sent = State.SendGift(profile); Render(); Notify(sent ? "상자를 선물했어요. (미리보기)" : "오늘 선물을 보냈거나 상대의 보관함이 가득 찼어요.");
            });
        }

        /// <summary>확인·취소 모달을 만들고 확인한 경우에만 지정 동작을 실행합니다.</summary>
        private void Confirm(string message, Action action)
        {
            var shade = UIFactory.Image("Confirmation Shade", _canvas, new Color(0, 0, 0, .38f), Vector2.zero, Vector2.one);
            shade.raycastTarget = true;
            var card = UIFactory.Image("Confirmation", shade.transform, Paper, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-425, -175), new Vector2(425, 175));
            Label("Message", card.transform, message, 29, Ink, 36, 35, 778, 165, TextAnchor.MiddleCenter);
            Button("Dismiss", card.transform, "취소", 35, 235, 380, 80, () => { shade.gameObject.SetActive(false); Destroy(shade.gameObject); }, false);
            Button("Confirm", card.transform, "확인", 435, 235, 380, 80, action);
            MirrorSceneRoot.SetLayer(shade.transform, gameObject.layer);
        }

        /// <summary>내 친구 코드를 실제 클립보드에 복사하고 완료를 알립니다.</summary>
        private void CopyCode() { GUIUtility.systemCopyBuffer = State.MyCode; Notify("친구 코드를 복사했어요."); }
        /// <summary>현재 화면 위에 일정 시간 표시할 결과 메시지를 갱신합니다.</summary>
        private void Notify(string message)
        {
            _toast.text = message; _toast.gameObject.SetActive(true);
            _toast.transform.parent.GetComponent<Image>().color = Green;
            _toast.transform.parent.SetAsLastSibling(); _toastUntil = Time.unscaledTime + 4;
        }
        /// <summary>내부 목록의 콘텐츠 높이를 설정해 세로 스크롤 범위를 조정합니다.</summary>
        private void Height(float height) => _content.sizeDelta = new Vector2(0, height);
        /// <summary>프로필 이름의 첫 글자를 사용하는 임시 아바타를 표시합니다.</summary>
        private void Avatar(Transform parent, FriendProfile profile, float x, float y, float size)
        {
            var avatar = Box("Avatar", parent, new Color(.55f, .7f, .65f), x, y, size, size);
            Label("Initial", avatar.transform, profile.Name.Substring(0, 1), 34, Color.white, 0, 0, size, size, TextAnchor.MiddleCenter);
        }
        /// <summary>세 시대의 복원 완료 수를 진행 표시로 구성합니다.</summary>
        private void Restoration(Transform parent, FriendProfile profile, float y)
        {
            string[] eras = { "삼국 복원", "고려 복원", "조선 복원" };
            for (int i = 0; i < 3; i++)
            {
                var cell = Box("Restoration", parent, new Color(.95f, .95f, .93f), 34 + i * 297, y, 280, 113);
                Label("Era", cell.transform, eras[i], 23, Muted, 20, 13, 250, 40);
                int count = profile.RestoredByEra[i];
                for (int j = 0; j < 3; j++) Box("Progress", cell.transform, j < count ? Green : Pale, 20 + j * 36, 65, 28, 28);
            }
        }
        /// <summary>상단 기준 위치와 크기로 UI 사각형을 생성합니다.</summary>
        private RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            return UIFactory.Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y - height), new Vector2(x + width, -y));
        }
        /// <summary>장식용 이미지를 만들고 포인터 입력을 가로채지 않도록 설정합니다.</summary>
        private Image Box(string name, Transform parent, Color color, float x, float y, float width, float height)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        /// <summary>이미지에 지정 색상의 테두리를 적용합니다.</summary>
        private void Border(Image image, Color? color = null)
        {
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = color ?? Line; outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        /// <summary>지정 영역에 글꼴·색·정렬을 적용한 장식용 텍스트를 만듭니다.</summary>
        private Text Label(string name, Transform parent, string value, int size, Color color, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = _font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Noto CJK의 줄 높이가 지정한 Rect보다 커도 한 줄 제목을 생략하지 않습니다.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (name == "Title" || name == "Sheet Title" || name == "Name" || name == "Own Code" || name == "Initial") text.fontStyle = FontStyle.Bold;
            return text;
        }
        /// <summary>버튼의 표시·색상·입력 영역을 구성하고 클릭 동작을 연결합니다.</summary>
        private Button Button(string id, Transform parent, string caption, float x, float y, float width, float height, Action click, bool filled = true, Color? textColor = null)
        {
            var image = Box(id, parent, filled ? Green : Paper, x, y, width, height); image.raycastTarget = true;
            if (!filled) Border(image);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            var label = Label("Caption", image.transform, caption, caption == "‹" ? 48 : caption == "×" ? 42 : 27, textColor ?? (filled ? Color.white : Green), 8, 0, width - 16, height, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            var colors = button.colors; colors.disabledColor = new Color(.82f, .85f, .82f); colors.pressedColor = new Color(.72f, .81f, .75f); button.colors = colors;
            _buttons[id] = button; return button;
        }
    }
}
