using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype.Collection
{
    [RequireComponent(typeof(MirrorSceneRoot))]
    public sealed class CollectionPresenter : MonoBehaviour
    {
        [SerializeField] private CollectionCatalog _catalog;
        [SerializeField] private Font _font;
        [SerializeField] private CollectionAppearance _appearance;
        private MirrorSceneRoot _root;
        private RectTransform _content;
        private Image _pageBackground;
        private Image _headerRule;
        private Text _backLabel;
        private Image _backIcon;
        private Text _heading;
        private Text _subtitle;
        private bool _refreshRequested;
        private readonly Dictionary<string, Button> _eraButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _relicButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, float> _scrollPositions = new Dictionary<string, float>();
        private static readonly Color Paper = new Color(.97f, .96f, .93f);
        private static readonly Color Ink = new Color(.14f, .23f, .22f);
        private static readonly Color Muted = new Color(.43f, .48f, .44f);
        private static readonly Color Gold = new Color(.61f, .47f, .26f);
        private const float ContentWidth = 984;

        public CollectionPage Page { get; private set; }
        public CollectionCatalog Catalog => _catalog;
        public CollectionAppearance Appearance => _appearance;
        public CollectionProgress Progress { get; private set; }
        public string SelectedEraId { get; private set; }
        public string SelectedRelicId { get; private set; }
        public CollectionScrollRect Scroll { get; private set; }
        public Button BackButton { get; private set; }
        public IReadOnlyDictionary<string, Button> EraButtons => _eraButtons;
        public IReadOnlyDictionary<string, Button> RelicButtons => _relicButtons;
        public Text StatsText { get; private set; }
        public Text BuildingStatusText { get; private set; }

        private void Awake()
        {
            _root = GetComponent<MirrorSceneRoot>();
            if (!_catalog || !_font)
            {
                Debug.LogError("도감 카탈로그와 글꼴을 연결하세요.", this);
                enabled = false;
                return;
            }
            Progress = CollectionDemoData.CreateProgress(_catalog);
            BuildFrame();
            RenderCurrent(1);
        }

        private void OnEnable()
        {
            if (Progress == null) return;
            Progress.Changed += RequestRefresh;
            _refreshRequested = true;
        }

        private void OnDisable()
        {
            if (Progress != null) Progress.Changed -= RequestRefresh;
        }

        private void RequestRefresh() => _refreshRequested = true;

        private void LateUpdate()
        {
            if (!_refreshRequested || !Scroll) return;
            _refreshRequested = false;
            RenderCurrent(Scroll.verticalNormalizedPosition);
        }

        // The future composition layer injects the shared read model here. It owns persistence.
        public void BindProgress(CollectionProgress progress)
        {
            if (progress == null || progress.Catalog != _catalog)
                throw new System.ArgumentException("도감과 같은 카탈로그의 기록을 전달하세요.", nameof(progress));
            if (Progress != null) Progress.Changed -= RequestRefresh;
            Progress = progress;
            if (Page == CollectionPage.Relic && !Progress.IsRestored(SelectedRelicId))
            {
                Page = CollectionPage.Era;
                SelectedRelicId = null;
            }
            else if (Page == CollectionPage.Relic) Progress.MarkViewed(SelectedRelicId);
            if (isActiveAndEnabled) Progress.Changed += RequestRefresh;
            RequestRefresh();
        }

        /// <summary>
        /// 공통 그림을 교체하고 현재 페이지와 스크롤 위치를 유지합니다. null은 기본 표현을 사용합니다.
        /// </summary>
        public void BindAppearance(CollectionAppearance appearance)
        {
            _appearance = appearance;
            if (Scroll && Progress != null)
            {
                RenderCurrent(Scroll.verticalNormalizedPosition);
                _refreshRequested = false;
            }
            else RequestRefresh();
        }

        public void ShowOverview()
        {
            RememberScroll();
            Page = CollectionPage.Overview;
            SelectedEraId = null;
            SelectedRelicId = null;
            RenderCurrent(SavedScroll());
        }

        public void ShowEra(string eraId)
        {
            if (_catalog.FindEra(eraId) == null) return;
            RememberScroll();
            Page = CollectionPage.Era;
            SelectedEraId = eraId;
            SelectedRelicId = null;
            RenderCurrent(SavedScroll());
        }

        public void ShowRelic(string relicId)
        {
            var relic = _catalog.FindRelic(relicId);
            if (relic == null || !Progress.IsRestored(relicId)) return;
            RememberScroll();
            Page = CollectionPage.Relic;
            SelectedEraId = relic.EraId;
            SelectedRelicId = relicId;
            Progress.MarkViewed(relicId);
            RenderCurrent(SavedScroll());
            _refreshRequested = false;
        }

        public void GoBack()
        {
            if (Page == CollectionPage.Relic) ShowEra(SelectedEraId);
            else if (Page == CollectionPage.Era) ShowOverview();
        }

        private string ScrollKey => Page + ":" + (Page == CollectionPage.Relic ? SelectedRelicId : SelectedEraId);
        private void RememberScroll()
        {
            if (Scroll) _scrollPositions[ScrollKey] = Scroll.verticalNormalizedPosition;
        }
        private float SavedScroll() => _scrollPositions.TryGetValue(ScrollKey, out float position) ? position : 1;

        private void BuildFrame()
        {
            var canvas = new GameObject("Collection UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _root.sceneCamera;
            canvas.planeDistance = 5;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1600);
            scaler.matchWidthOrHeight = 0;
            _pageBackground = UIFactory.Image("Paper", canvas.transform, Paper, Vector2.zero, Vector2.one);
            var header = UIFactory.Rect("Header", canvas.transform, new Vector2(0, 1), Vector2.one,
                new Vector2(48, -144), new Vector2(-48, 0));
            BackButton = MakeButton("Back", header, "‹", 0, 35, 86, 80, Ink, Paper, 58);
            BackButton.onClick.AddListener(GoBack);
            _backLabel = BackButton.GetComponentInChildren<Text>(true);
            _backIcon = TopRect("Back Icon", BackButton.transform, 0, 0, 86, 80).gameObject.AddComponent<Image>();
            _backIcon.raycastTarget = false;
            _backIcon.preserveAspect = true;
            _heading = Label("Title", header, "도감", 48, Ink, 108, 27, 650, 65);
            _subtitle = Label("Subtitle", header, "복원으로 되찾은 우리의 기록", 25, Muted, 110, 92, 730, 42);
            _headerRule = Box("Header Rule", header, new Color(.79f, .79f, .72f), 0, 143, ContentWidth, 2);

            var viewport = UIFactory.Rect("Collection Scroll", canvas.transform, Vector2.zero, Vector2.one,
                new Vector2(48, 24), new Vector2(-48, -164));
            var backdrop = viewport.gameObject.AddComponent<Image>();
            backdrop.color = Color.clear;
            backdrop.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            Scroll = viewport.gameObject.AddComponent<CollectionScrollRect>();
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.decelerationRate = .08f;
            Scroll.scrollSensitivity = 35;
            _content = UIFactory.Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            _content.pivot = new Vector2(.5f, 1);
            _content.sizeDelta = new Vector2(0, 1320);
            Scroll.viewport = viewport;
            Scroll.content = _content;
            _root.uiRaycasters = new[] { canvas.GetComponent<GraphicRaycaster>() };
            MirrorSceneRoot.SetLayer(canvas.transform, gameObject.layer);
        }

        private void RenderCurrent(float scrollPosition)
        {
            Scroll.StopMovement();
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            _eraButtons.Clear();
            _relicButtons.Clear();
            StatsText = null;
            BuildingStatusText = null;
            ApplyFrameAppearance();
            BackButton.gameObject.SetActive(Page != CollectionPage.Overview);
            _heading.text = Page == CollectionPage.Overview ? "도감" : Page == CollectionPage.Era ? "시대별 기록" : "유물 기록";
            _heading.rectTransform.offsetMin = new Vector2(Page == CollectionPage.Overview ? 0 : 108, -92);
            _subtitle.text = Page == CollectionPage.Overview ? "복원으로 되찾은 우리의 기록" : "기억을 모아, 시간을 이어가다";
            _subtitle.rectTransform.offsetMin = new Vector2(Page == CollectionPage.Overview ? 0 : 110, -134);
            if (Page == CollectionPage.Overview) BuildOverview();
            else if (Page == CollectionPage.Era) BuildEra();
            else BuildRelic();
            MirrorSceneRoot.SetLayer(_content, gameObject.layer);
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = Mathf.Clamp01(scrollPosition);
        }

        private void ApplyFrameAppearance()
        {
            Sprite background = _appearance ? _appearance.PageBackground : null;
            if (Page != CollectionPage.Overview)
            {
                var era = _catalog.FindEra(SelectedEraId);
                if (era != null) background = FirstSprite(era.BackgroundIllustration, background);
                if (Page == CollectionPage.Relic)
                {
                    var relic = _catalog.FindRelic(SelectedRelicId);
                    if (relic != null) background = FirstSprite(relic.BackgroundIllustration, background);
                }
            }
            ApplySurface(_pageBackground, background, Paper);
            ApplySurface(_headerRule, _appearance ? _appearance.Divider : null, new Color(.79f, .79f, .72f));
            ApplySurface((Image)BackButton.targetGraphic, _appearance ? _appearance.BackButtonBackground : null, Paper);
            Sprite icon = _appearance ? _appearance.BackIcon : null;
            _backLabel.gameObject.SetActive(!icon);
            _backIcon.gameObject.SetActive(icon);
            _backIcon.sprite = icon;
            _backIcon.color = Color.white;
        }

        private void BuildOverview()
        {
            float summaryY = 145 + _catalog.Eras.Count * 295;
            _content.sizeDelta = new Vector2(0, summaryY + 290);
            Label("Eyebrow", _content, "시대의 조각을 모으다", 32, Ink, 0, 0, ContentWidth, 54);
            Label("Introduction", _content, "복원을 마친 유물과 정령을 만나보세요.", 26, Muted, 0, 57, ContentWidth, 44);
            for (int i = 0; i < _catalog.Eras.Count; i++)
            {
                var era = _catalog.Eras[i];
                float y = 125 + i * 295;
                var card = MakeButton("Era_" + era.Id, _content, "", 0, y, ContentWidth, 270, Ink,
                    Color.Lerp(Paper, era.Accent, .12f), sprite: _appearance ? _appearance.EraCardBackground : null);
                card.onClick.AddListener(() => ShowEra(era.Id));
                _eraButtons.Add(era.Id, card);
                Box("Accent", card.transform, era.Accent, 0, 0, 8, 270, _appearance ? _appearance.AccentStrip : null);
                Label("Index", card.transform, "0" + (i + 1), 25, era.Accent, 36, 23, 110, 40);
                Label("Era", card.transform, era.Name, 46, Ink, 36, 80, 530, 66);
                Label("Period", card.transform, era.Period, 25, Muted, 38, 145, 500, 44);
                bool complete = Progress.IsBuildingUnlocked(era.Id);
                Sprite completionIcon = _appearance ? _appearance.CompletionIcon : null;
                string count = complete ? (completionIcon ? "수집 완료" : "수집 완료  ✓") :
                    $"{Progress.RestoredInEra(era.Id)} / {Progress.TotalInEra(era.Id)}";
                Label("Count", card.transform, count, 28, complete ? era.Accent : Ink, 38, 210, 470, 38);
                if (complete && completionIcon)
                {
                    var icon = TopRect("Complete Icon", card.transform, 220, 209, 40, 40).gameObject.AddComponent<Image>();
                    icon.sprite = completionIcon;
                    icon.color = Color.white;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                }
                Artwork("Era Emblem", card.transform, KindForEra(era.Id), era.Accent, 660, 35, 260, 220, era.OverviewIllustration);
                if (Progress.HasUnreadInEra(era.Id)) NewBadge(card.transform, 545, 36);
            }
            Box("Stats Rule", _content, new Color(.79f, .79f, .72f), 0, summaryY, ContentWidth, 2, _appearance ? _appearance.Divider : null);
            Label("Stats Heading", _content, "나의 수집 기록", 30, Ink, 0, summaryY + 24, 440, 48);
            StatsText = Label("Statistics", _content,
                $"복원한 유물  {Progress.RestoredCount} / {Progress.TotalCount}\n해금한 건물  {Progress.CompletedBuildingCount} / {_catalog.Eras.Count}\n수집률  {Percent(Progress.CollectionRate)}%",
                28, Ink, 450, summaryY + 22, 534, 154, TextAnchor.MiddleRight);
            ProgressBar(_content, 0, summaryY + 190, ContentWidth, Progress.CollectionRate, Gold);
            Label("Sample Notice", _content, "미리보기 · 샘플 수집 기록", 22, Muted, 0, summaryY + 220, ContentWidth, 40);
        }

        private void BuildEra()
        {
            var era = _catalog.FindEra(SelectedEraId);
            Label("Era Name", _content, era.Name, 58, Ink, 0, 0, ContentWidth, 80);
            Label("Era Period", _content, era.Period, 28, Muted, 0, 82, ContentWidth, 46);
            float rate = (float)Progress.RestoredInEra(era.Id) / Mathf.Max(1, Progress.TotalInEra(era.Id));
            Label("Era Rate", _content, $"수집률 {Percent(rate)}%   ·   {Progress.RestoredInEra(era.Id)} / {Progress.TotalInEra(era.Id)}", 30, era.Accent,
                0, 150, ContentWidth, 48);
            ProgressBar(_content, 0, 215, ContentWidth, rate, era.Accent);
            var relics = _catalog.Relics.Where(relic => relic.EraId == era.Id).ToArray();
            float buildingY = 290 + Mathf.CeilToInt(relics.Length / 3f) * 380;
            float contextY = buildingY + 340;
            _content.sizeDelta = new Vector2(0, contextY + 670);
            for (int i = 0; i < relics.Length; i++)
            {
                var relic = relics[i];
                bool restored = Progress.IsRestored(relic.Id);
                float x = i % 3 * 336;
                float y = 270 + i / 3 * 380;
                var card = MakeButton("Relic_" + relic.Id, _content, "", x, y, 312, 350, Ink,
                    restored ? Color.Lerp(Paper, era.Accent, .14f) : new Color(.89f, .89f, .85f),
                    sprite: _appearance ? (restored ? _appearance.RestoredRelicCardBackground : _appearance.LockedRelicCardBackground) : null);
                card.interactable = restored;
                card.onClick.AddListener(() => ShowRelic(relic.Id));
                _relicButtons.Add(relic.Id, card);
                Artwork("Relic Emblem", card.transform, restored ? KindFor(relic) : CollectionArtworkKind.Lock,
                    restored ? era.Accent : Muted, 60, 44, 192, 158,
                    restored ? FirstSprite(relic.Thumbnail, relic.Illustration) : FirstSprite(relic.LockedIllustration, CommonLock));
                Label("Relic Name", card.transform, restored ? relic.Name : "???", 28, Ink, 12, 212, 288, 70, TextAnchor.MiddleCenter);
                Label("Spirit Name", card.transform, restored ? relic.SpiritName : "???", 23, Muted, 12, 284, 288, 50, TextAnchor.MiddleCenter);
                if (Progress.IsUnread(relic.Id)) NewBadge(card.transform, 18, 16);
            }
            bool unlocked = Progress.IsBuildingUnlocked(era.Id);
            var building = Box("Building Card", _content, Color.Lerp(Paper, era.Accent, .08f), 0, buildingY, ContentWidth, 275,
                _appearance ? _appearance.BuildingCardBackground : null);
            Artwork("Building Emblem", building.transform, unlocked ? CollectionArtworkKind.Building : CollectionArtworkKind.Lock,
                unlocked ? era.Accent : Muted, 38, 42, 205, 170,
                unlocked ? era.BuildingIllustration : FirstSprite(era.LockedBuildingIllustration, CommonLock));
            Label("Building Name", building.transform, unlocked ? era.BuildingName : "시대의 건물", 35, Ink, 278, 32, 675, 60);
            BuildingStatusText = Label("Building Status", building.transform, unlocked ? "해금 완료" : "복원 완료 후 해금", 27,
                era.Accent, 280, 99, 665, 45);
            Label("Building Hint", building.transform, unlocked ? "이 시대의 모든 유물을 복원했습니다." :
                $"유물 {Progress.TotalInEra(era.Id)}종을 모두 복원하면\n건물이 해금됩니다.", 26, Muted, 280, 150, 665, 94);
            Label("Era Context Heading", _content, "시대를 들여다보다", 34, Ink, 0, contextY, ContentWidth, 60);
            Artwork("Context Emblem", _content, KindForEra(era.Id), era.Accent, 655, contextY + 100, 270, 220,
                FirstSprite(era.ContextIllustration, era.OverviewIllustration));
            Label("Era Context", _content, era.Description, 28, Muted, 0, contextY + 80, 605, 315, TextAnchor.UpperLeft);
            Label("Sample Notice", _content, "미리보기 · 정령·건물 이름과 설명, 그림은 임시 콘텐츠입니다.", 22, Muted, 0, contextY + 470, ContentWidth, 70);
        }

        private void BuildRelic()
        {
            var relic = _catalog.FindRelic(SelectedRelicId);
            var era = _catalog.FindEra(relic.EraId);
            _content.sizeDelta = new Vector2(0, 2020);
            Label("Era Tag", _content, era.Name + "  /  복원 완료", 25, era.Accent, 0, 0, ContentWidth, 45);
            Label("Relic Title", _content, relic.Name, 56, Ink, 0, 52, ContentWidth, 92);
            Label("Spirit Title", _content, "함께 깨어난 정령 · " + relic.SpiritName, 30, Muted, 0, 146, ContentWidth, 60);
            var plate = Box("Illustration Plate", _content, Color.Lerp(Paper, era.Accent, .10f), 0, 248, ContentWidth, 555,
                _appearance ? _appearance.RelicPlateBackground : null);
            Label("Plate Index", plate.transform, "RESTORED  /  " + era.Name, 23, era.Accent, 32, 25, 650, 42);
            Artwork("Relic Illustration", plate.transform, KindFor(relic), era.Accent, 110, 104, 580, 350, relic.Illustration);
            var spiritPlate = Box("Spirit Plate", plate.transform, Paper, 699, 314, 240, 204,
                _appearance ? _appearance.SpiritPlateBackground : null);
            Artwork("Spirit Illustration", spiritPlate.transform, CollectionArtworkKind.Spirit, era.Accent, 38, 12, 165, 141, relic.SpiritIllustration);
            Label("Spirit Caption", spiritPlate.transform, relic.SpiritName, 23, Ink, 8, 150, 224, 45, TextAnchor.MiddleCenter);
            Label("Illustration Caption", plate.transform, relic.Illustration ? "유물과 정령의 기록" : "유물 · 정령 임시 일러스트", 22, Muted, 32, 481, 650, 42);
            Label("Description Heading", _content, "유물이 품은 이야기", 36, Ink, 0, 873, ContentWidth, 64);
            Label("Description", _content, relic.Description, 29, Ink, 0, 950, ContentWidth, 335, TextAnchor.UpperLeft);
            Box("Description Rule", _content, new Color(.79f, .79f, .72f), 0, 1310, ContentWidth, 2,
                _appearance ? _appearance.Divider : null);
            Label("Conservation Heading", _content, "발굴과 보존의 기록", 34, Ink, 0, 1360, ContentWidth, 62);
            Label("Conservation", _content, relic.ConservationNote, 28, Muted, 0, 1445, ContentWidth, 330, TextAnchor.UpperLeft);
            Label("Draft Notice", _content, "미리보기 · 정령·건물 이름과 설명, 그림은 임시 콘텐츠입니다.\n실제 유물 자료와 정령 아트는 추후 교체됩니다.", 22,
                Muted, 0, 1865, ContentWidth, 90);
        }

        private static int Percent(float rate) => Mathf.FloorToInt(rate * 100 + .001f);
        private Sprite CommonLock => _appearance ? _appearance.LockIllustration : null;
        private static Sprite FirstSprite(Sprite preferred, Sprite fallback) => preferred ? preferred : fallback;

        private static void ApplySurface(Image image, Sprite sprite, Color fallbackTint)
        {
            image.sprite = sprite;
            image.color = sprite ? Color.white : fallbackTint;
            image.type = sprite && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
        }

        private static CollectionArtworkKind KindFor(CollectionRelicDefinition relic)
        {
            switch (relic.Id)
            {
                case "samguk-standing-buddha":
                case "goryeo-stone-buddha":
                    return CollectionArtworkKind.Buddha;
                case "samguk-incense-burner":
                    return CollectionArtworkKind.IncenseBurner;
                case "samguk-crown":
                    return CollectionArtworkKind.Crown;
                case "goryeo-celadon":
                    return CollectionArtworkKind.Vessel;
                case "goryeo-lacquerware":
                    return CollectionArtworkKind.Lacquerware;
                case "joseon-royal-seal":
                    return CollectionArtworkKind.RoyalSeal;
                case "joseon-horse-medallion":
                    return CollectionArtworkKind.HorseMedallion;
                case "joseon-cat-sparrow-painting":
                    return CollectionArtworkKind.Painting;
                default:
                    return CollectionArtworkKind.Vessel;
            }
        }

        private static CollectionArtworkKind KindForEra(string eraId)
        {
            switch (eraId)
            {
                case "samguk": return CollectionArtworkKind.Crown;
                case "goryeo": return CollectionArtworkKind.Vessel;
                case "joseon": return CollectionArtworkKind.RoyalSeal;
                default: return CollectionArtworkKind.Building;
            }
        }

        private RectTransform TopRect(string name, Transform parent, float x, float y, float width, float height)
        {
            return UIFactory.Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - height), new Vector2(x + width, -y));
        }

        private Image Box(string name, Transform parent, Color tint, float x, float y, float width, float height, Sprite sprite = null)
        {
            var image = TopRect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            ApplySurface(image, sprite, tint);
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string name, Transform parent, string text, int size, Color tint, float x, float y,
            float width, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = TopRect(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = tint;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Noto CJK line metrics exceed several single-line design boxes; overflow keeps
            // the full glyph visible while the viewport mask handles page clipping.
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1;
            return label;
        }

        private Button MakeButton(string name, Transform parent, string text, float x, float y, float width,
            float height, Color ink, Color fill, int size = 28, Sprite sprite = null)
        {
            var image = Box(name, parent, fill, x, y, width, height, sprite);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(.96f, .96f, .94f);
            colors.pressedColor = new Color(.83f, .88f, .84f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            if (!string.IsNullOrEmpty(text)) Label("Label", image.transform, text, size, ink, 0, 0, width, height, TextAnchor.MiddleCenter);
            return button;
        }

        private void Artwork(string name, Transform parent, CollectionArtworkKind kind, Color tint,
            float x, float y, float width, float height, Sprite sprite = null)
        {
            var rect = TopRect(name, parent, x, y, width, height);
            if (sprite)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else rect.gameObject.AddComponent<CollectionArtwork>().Configure(kind, tint);
        }

        private void NewBadge(Transform parent, float x, float y)
        {
            var badge = Box("New", parent, new Color(.63f, .29f, .20f), x, y, 45, 45,
                _appearance ? _appearance.NewBadgeBackground : null);
            Sprite icon = _appearance ? _appearance.NewBadgeIcon : null;
            if (icon)
            {
                var image = TopRect("New Icon", badge.transform, 0, 0, 45, 45).gameObject.AddComponent<Image>();
                image.sprite = icon;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else Label("N", badge.transform, "N", 25, Color.white, 0, 0, 45, 45, TextAnchor.MiddleCenter);
        }

        private void ProgressBar(Transform parent, float x, float y, float width, float rate, Color tint)
        {
            Box("Progress Track", parent, new Color(.85f, .86f, .81f), x, y, width, 8,
                _appearance ? _appearance.ProgressTrack : null);
            if (rate > 0) Box("Progress Fill", parent, tint, x, y, width * Mathf.Clamp01(rate), 8,
                _appearance ? _appearance.ProgressFill : null);
        }
    }
}
