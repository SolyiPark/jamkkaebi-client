using System;
using MobilePrototype.Exhibition;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobilePrototype
{
    public class TabHost : MonoBehaviour
    {
        public TabCatalog catalog;
        public RectTransform viewport;
        public RawImage mirror;
        public MirrorInput input;
        public GameObject profileBar;
        public RectTransform toolbar;
        public Text status;
        public float headerHeight = 178;
        public float toolbarHeight = 124;
        public int ActiveIndex { get; private set; } = -1;
        public bool IsReady { get; private set; }
        private bool _loading;
        private TabTransition _transition;
        public bool IsBusy { get => _loading || (_transition && _transition.IsActive); private set => _loading = value; }
        public TabTransition Transition => _transition;
        public MirrorSceneRoot ActiveRoot => ActiveIndex >= 0 ? roots[ActiveIndex] : null;
        private MirrorSceneRoot[] roots;
        private RenderTexture[] textures;
        private bool[] visited;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<Image> markers = new List<Image>();
        private readonly List<Image> icons = new List<Image>();
        private Vector2Int textureSize;
        private int pendingTab = -1;

        /// <summary>
        /// 탭 씬을 배경 실행 정책에 따라 로드하고 동기 렌더링으로 초기 미리보기를 캡처한 뒤 시작 탭을 표시합니다.
        /// </summary>
        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            if (!ValidateCatalog(out var error)) { status.text = error; Debug.LogError(error); yield break; }
            roots = new MirrorSceneRoot[catalog.tabs.Count];
            textures = new RenderTexture[roots.Length];
            visited = new bool[roots.Length];
            _transition = gameObject.AddComponent<TabTransition>();
            _transition.Initialize(this);
            input.ConfigureNavigation(this);
            BuildToolbar();
            IsBusy = true;
            int first = Mathf.Clamp(catalog.initialTab, 0, roots.Length - 1);
            Layout(catalog.tabs[first].showProfile);
            Canvas.ForceUpdateCanvases();
            textureSize = MeasureTexture();
            for (int i = 0; i < roots.Length; i++)
            {
                yield return Load(i);
                if (!roots[i]) { IsBusy = false; yield break; }
            }
            // Render synchronously: paused roots never remain active across a simulation frame.
            foreach (var root in roots)
            {
                bool paused = root.IsPaused;
                try
                {
                    root.SetPaused(false);
                    Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(root.sceneCamera,
                        new RenderPipeline.StandardRequest { destination = root.sceneCamera.targetTexture });
                }
                finally { root.SetPaused(paused); }
            }
            IsReady = true; IsBusy = false;
            status.gameObject.SetActive(false);
            Show(first);
        }
        /// <summary>
        /// 탭 개수·ID·씬 경로의 유효성과 중복 및 빌드 포함 여부를 검사하고 실패 시 표시할 오류를 반환합니다.
        /// </summary>
        private bool ValidateCatalog(out string error)
        {
            error = null;
            if (!catalog || catalog.tabs.Count == 0 || catalog.tabs.Count > 24)
                error = "탭 설정을 확인하세요. 탭은 1~24개를 지원합니다.";
            else
            {
                var paths = new HashSet<string>();
                var ids = new HashSet<string>();
                foreach (var tab in catalog.tabs)
                    if (string.IsNullOrWhiteSpace(tab.id) || !ids.Add(tab.id) ||
                        !paths.Add(tab.scenePath ?? "") || !Application.CanStreamedLevelBeLoaded(tab.scenePath))
                    { error = "탭 ID·Scene 경로·빌드 씬 목록을 확인하세요. 각 탭에 서로 다른 Scene이 필요합니다."; break; }
            }
            return error == null;
        }
        /// <summary>
        /// 독립된 2D 물리 공간으로 탭을 추가 로드하고 카메라·렌더 레이어·텍스처를 연결합니다. 비활성 루트는 ContinueRunning 정책일 때만 실행합니다.
        /// </summary>
        private IEnumerator Load(int index)
        {
            var definition = catalog.tabs[index];
            yield return SceneManager.LoadSceneAsync(definition.scenePath,
                new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D));
            var scene = SceneManager.GetSceneByPath(definition.scenePath);
            foreach (var obj in scene.GetRootGameObjects())
                if (obj.TryGetComponent<MirrorSceneRoot>(out var root)) { roots[index] = root; break; }
            if (!roots[index] || !roots[index].sceneCamera)
            {
                status.gameObject.SetActive(true);
                status.text = "Scene에 MirrorSceneRoot와 카메라를 연결하세요.";
                Debug.LogError(status.text + " " + definition.scenePath);
                yield break;
            }
            textures[index] = NewTexture(index);
            roots[index].Configure(8 + index, textures[index]);
            roots[index].SetPaused(index != ActiveIndex && definition.backgroundPolicy != BackgroundPolicy.ContinueRunning);
        }
        /// <summary>
        /// 현재 측정한 뷰포트 크기로 탭 전용 렌더 텍스처를 생성합니다. 수명 종료 시 호스트가 해제합니다.
        /// </summary>
        private RenderTexture NewTexture(int index)
        {
            var texture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32)
            { name = "Mirror_" + catalog.tabs[index].id, filterMode = FilterMode.Bilinear };
            texture.Create();
            return texture;
        }
        /// <summary>
        /// 프로필 표시 여부에 따라 공통 뷰포트의 상단 여백을 조정하고 하단 탭 영역을 확보합니다.
        /// </summary>
        private void Layout(bool showProfile)
        {
            profileBar.SetActive(showProfile);
            viewport.offsetMin = new Vector2(0, toolbarHeight);
            viewport.offsetMax = new Vector2(0, showProfile ? -headerHeight : 0);
        }
        /// <summary>
        /// 캔버스 배율과 뷰포트 비율로 렌더 텍스처 크기를 계산하되 최소 크기와 최대 해상도를 제한합니다.
        /// </summary>
        private Vector2Int MeasureTexture()
        {
            var rect = viewport.rect;
            float aspect = Mathf.Max(0.01f, rect.width / Mathf.Max(1, rect.height));
            float screenWidth = rect.width * viewport.GetComponentInParent<Canvas>().scaleFactor;
            int width = Mathf.Clamp(Mathf.RoundToInt(screenWidth), 256, catalog.maximumTextureWidth);
            int height = Mathf.Clamp(Mathf.RoundToInt(width / aspect), 256, 4096);
            return new Vector2Int(width, height);
        }
        /// <summary>
        /// 전환·로딩 중이 아닐 때 해상도와 배경 실행 정책을 갱신합니다. 텍스처를 교체할 때 숨김 탭의 기존 화면을 복사합니다.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsReady || IsBusy) return;
            var size = MeasureTexture();
            if (size != textureSize)
            {
                input.Cancel(); textureSize = size;
                for (int i = 0; i < roots.Length; i++)
                {
                    if (!roots[i]) continue;
                    roots[i].sceneCamera.targetTexture = null;
                    var previous = textures[i];
                    textures[i] = NewTexture(i);
                    // Preserve paused-tab previews across aspect changes without running their gameplay.
                    Graphics.Blit(previous, textures[i]);
                    previous.Release(); Destroy(previous);
                    roots[i].sceneCamera.targetTexture = textures[i];
                }
                mirror.texture = textures[ActiveIndex];
            }
            for (int i = 0; i < roots.Length; i++)
                if (roots[i]) roots[i].SetPaused(i != ActiveIndex && catalog.tabs[i].backgroundPolicy != BackgroundPolicy.ContinueRunning);
            RefreshStyles();
        }
        /// <summary>
        /// 로드된 루트에 고정 시간 간격의 물리 시뮬레이션을 요청합니다. 일시정지 여부는 각 루트가 검사합니다.
        /// </summary>
        private void FixedUpdate()
        {
            if (roots == null) return;
            foreach (var root in roots) if (root) root.Simulate(Time.fixedDeltaTime);
        }
        /// <summary>
        /// 유효한 탭 선택을 공통 전환 경로로 처리합니다. 작업 중에는 마지막 요청을 보관하고 이미 활성인 탭은 무시합니다.
        /// </summary>
        public void SelectTab(int index)
        {
            if (!IsReady || index < 0 || index >= roots.Length) return;
            if (IsBusy) { pendingTab = index; return; }
            if (index == ActiveIndex) return;
            input.Cancel();
            if (_transition.Begin(index)) _transition.Settle(true);
        }
        /// <summary>
        /// 호스트가 준비되고 다른 작업이 없으면 활성 인덱스에 방향값을 더한 탭으로 전환을 시작합니다. 끝 경계 연출은 전환 컴포넌트가 처리합니다.
        /// </summary>
        public bool BeginSwipe(int direction)
        {
            if (!IsReady || IsBusy) return false;
            return _transition.Begin(ActiveIndex + direction);
        }
        /// <summary>
        /// 전환 결과 탭을 표시하거나 재방문 재시작 정책에 따라 씬을 다시 로드하고 대기 중 선택 요청을 처리합니다.
        /// </summary>
        public void CompleteTransition(int index)
        {
            if (index != ActiveIndex && visited[index] && catalog.tabs[index].backgroundPolicy == BackgroundPolicy.RestartOnReturn)
                StartCoroutine(Restart(index, true));
            else { Show(index); ProcessPending(); }
        }
        /// <summary>
        /// 보관한 마지막 탭 요청을 먼저 비운 뒤 다시 선택해 완료된 요청이 중복 실행되지 않도록 합니다.
        /// </summary>
        private void ProcessPending()
        {
            if (pendingTab < 0) return;
            int next = pendingTab; pendingTab = -1; SelectTab(next);
        }
        /// <summary>
        /// 활성 탭과 숨김 탭의 실행 정책·카메라·입력·프로필을 갱신하고 홈 전시관에 표시 사실을 알립니다.
        /// </summary>
        private void Show(int index)
        {
            input.Bind(null);
            for (int i = 0; i < roots.Length; i++)
            {
                bool active = i == index;
                roots[i].SetPaused(!active && catalog.tabs[i].backgroundPolicy != BackgroundPolicy.ContinueRunning);
                roots[i].sceneCamera.enabled = active;
            }
            ActiveIndex = index; visited[index] = true;
            Layout(catalog.tabs[index].showProfile);
            mirror.texture = textures[index];
            input.Bind(roots[index]);
            var exhibition = roots[index].GetComponent<ExhibitionView>();
            if (exhibition) exhibition.NotifyVisible();
            RefreshStyles();
        }
        /// <summary>
        /// 플레이 중이며 준비가 끝나고 다른 작업이 없을 때 현재 탭을 다시 로드합니다.
        /// </summary>
        [ContextMenu("Restart Current Scene Now")]
        public void RestartCurrent()
        { if (Application.isPlaying && IsReady && !IsBusy) StartCoroutine(Restart(ActiveIndex, true)); }
        /// <summary>
        /// 입력을 해제하고 대상 씬과 텍스처를 교체합니다. 재로드 성공 시 선택 여부를 적용한 뒤 보관한 탭 요청을 처리합니다.
        /// </summary>
        private IEnumerator Restart(int index, bool selectAfter)
        {
            IsBusy = true;
            input.Bind(null);
            var root = roots[index];
            root.SetPaused(true);
            yield return SceneManager.UnloadSceneAsync(root.gameObject.scene);
            textures[index].Release(); Destroy(textures[index]); roots[index] = null;
            yield return Load(index);
            if (!roots[index]) { IsReady = false; IsBusy = false; yield break; }
            if (selectAfter) Show(index);
            IsBusy = false;
            if (pendingTab >= 0) { int next = pendingTab; pendingTab = -1; SelectTab(next); }
        }
        /// <summary>
        /// 기존 하단 항목을 제거하고 카탈로그 순서대로 선택 버튼·마커·아이콘·제목을 생성해 탭 선택에 연결합니다.
        /// </summary>
        private void BuildToolbar()
        {
            for (int i = toolbar.childCount - 1; i >= 0; i--) Destroy(toolbar.GetChild(i).gameObject);
            for (int i = 0; i < catalog.tabs.Count; i++)
            {
                var tab = catalog.tabs[i]; int index = i;
                var min = new Vector2((float)i / catalog.tabs.Count, 0);
                var max = new Vector2((float)(i + 1) / catalog.tabs.Count, 1);
                var background = UIFactory.Image("Tab_" + tab.id, toolbar, catalog.surface, min, max);
                background.raycastTarget = true;
                var button = background.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectTab(index));
                buttons.Add(button);
                markers.Add(UIFactory.Image("Selection", background.transform, tab.markerColor,
                    new Vector2(0,1), Vector2.one, new Vector2(0,-8), Vector2.zero));
                var icon = UIFactory.Image("Icon", background.transform, Color.white,
                    new Vector2(0.5f,0.65f), new Vector2(0.5f,0.65f), new Vector2(-23,-23), new Vector2(23,23));
                icon.preserveAspect = true; icons.Add(icon);
                labels.Add(UIFactory.Label("Label", background.transform, catalog.font, tab.title, 34, tab.normalColor,
                    Vector2.zero, Vector2.one, new Vector2(8,8), new Vector2(-8,-8)));
            }
            RefreshStyles();
        }
        /// <summary>
        /// 준비 상태와 활성 탭에 맞춰 버튼의 입력 가능 여부·배경·마커·아이콘·글자 스타일을 갱신합니다.
        /// </summary>
        private void RefreshStyles()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var tab = catalog.tabs[i]; bool selected = i == ActiveIndex;
                buttons[i].interactable = IsReady;
                var image = (Image)buttons[i].targetGraphic;
                image.sprite = selected && tab.selectedBackground ? tab.selectedBackground : tab.background;
                image.color = image.sprite ? Color.white : catalog.surface;
                markers[i].sprite = tab.selectionMarker; markers[i].color = tab.markerColor;
                markers[i].gameObject.SetActive(selected);
                labels[i].text = tab.title;
                labels[i].color = selected ? tab.selectedColor : tab.normalColor;
                labels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                icons[i].sprite = tab.icon; icons[i].gameObject.SetActive(tab.icon);
                labels[i].rectTransform.anchorMax = new Vector2(1, tab.icon ? 0.50f : 1);
            }
        }
        /// <summary>
        /// 로드된 탭 루트를 반환하며 초기화 전이거나 인덱스가 범위를 벗어나면 null을 반환합니다.
        /// </summary>
        public MirrorSceneRoot GetRoot(int index) => roots != null && index >= 0 && index < roots.Length ? roots[index] : null;
        /// <summary>
        /// 호스트가 소유한 렌더 텍스처의 GPU 자원과 Unity 객체를 해제합니다.
        /// </summary>
        private void OnDestroy()
        {
            if (textures == null) return;
            foreach (var texture in textures) if (texture) { texture.Release(); Destroy(texture); }
        }
    }
}
