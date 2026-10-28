using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Collection
{
    /// <summary>기존 도감의 배치와 그림 선택을 유지하는 기본 View입니다. 수집 기록을 변경하지 않습니다.</summary>
    /// <remarks>
    /// 기존 Presenter에 있던 '어디에, 어떤 크기와 색으로 그릴지'를 모아 둔 표시 구현입니다.
    /// 이 파일은 아직 프리팹으로 바뀐 완성형 디자인 도구가 아닙니다. 현 디자인을 유지하기 위한 코드 생성 방식입니다.
    /// 새 프리팹 디자인을 만들면 CollectionView를 상속한 별도 View로 교체할 수 있습니다.
    /// 여기서는 전달받은 판정으로 잠금·신규 표시를 그릴 뿐, 원본 기록을 변경하거나 화면 이동을 확정하지 않습니다.
    /// </remarks>
    public sealed class CollectionDefaultView : CollectionView
    {
        private Font _font;
        // 이번 화면에 사용할 안내지입니다. CollectionProgress 원본을 보관하지 않습니다.
        private CollectionViewData _data;
        private MirrorSceneRoot _root;
        private RectTransform _content;
        private Image _pageBackground;
        private Image _headerRule;
        private Text _backLabel;
        private Image _backIcon;
        private Text _heading;
        private Text _subtitle;
        private readonly Dictionary<string, Button> _eraButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _relicButtons = new Dictionary<string, Button>();
        private static readonly Color Paper = new Color(.97f, .96f, .93f);
        private static readonly Color Ink = new Color(.14f, .23f, .22f);
        private static readonly Color Muted = new Color(.43f, .48f, .44f);
        private static readonly Color Gold = new Color(.61f, .47f, .26f);
        // 카드의 크기·간격·색상은 표시 구현의 선택이므로 이 View에만 둡니다.
        // 새 디자인이 이 좌표를 그대로 써야 할 의무는 없습니다.
        private const float ContentWidth = 984;


        public override IReadOnlyDictionary<string, Button> EraButtons => _eraButtons;
        public override IReadOnlyDictionary<string, Button> RelicButtons => _relicButtons;

        /// <summary>기존 방식의 공통 프레임을 한 번 만듭니다. 페이지 본문은 이후 Render에서 채웁니다.</summary>
        /// <param name="root">이 도감 탭이 사용하는 카메라와 UI 입력의 소유자입니다.</param>
        /// <param name="font">본문과 버튼에 사용할 기존 글꼴입니다.</param>
        public override void Initialize(MirrorSceneRoot root, Font font)
        {
            _root = root;
            _font = font;
            BuildFrame();
        }
        /// <summary>머리글·뒤로가기·스크롤처럼 페이지를 이동해도 공통으로 남는 UI 틀을 만듭니다.</summary>
        private void BuildFrame()
        {
            var canvas = new GameObject("Collection UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            // 독립적인 전체 화면 UI를 추가하지 않고 탭의 카메라에 그립니다.
            // 공통 셸이 이 카메라 화면을 표시하므로 탭 전환·입력 전달 경계를 그대로 유지해야 합니다.
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
            // 뒤로 갈 목적지는 Presenter가 알고 있습니다. View는 버튼을 눌렀다는 사실만 전달합니다.
            BackButton.onClick.AddListener(RequestBack);
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
            // Raycaster는 화면의 어느 버튼을 눌렀는지 찾는 부품입니다.
            // 별도 EventSystem을 만들지 않고 기존 MirrorInput이 이 탭의 버튼을 찾을 수 있게 등록합니다.
            _root.uiRaycasters = new[] { canvas.GetComponent<GraphicRaycaster>() };
            MirrorSceneRoot.SetLayer(canvas.transform, gameObject.layer);
        }

        /// <summary>이전 본문을 제거하고 현재 화면을 다시 생성한 뒤 레이어와 스크롤 위치를 적용합니다.</summary>
        /// <param name="data">이 갱신에서 표시할 수집 상태 복사본과 그림 자산입니다.</param>
        /// <param name="scrollPosition">본문 생성 후 적용할 정규화된 세로 위치입니다.</param>
        public override void Render(CollectionViewData data, float scrollPosition)
        {
            _data = data;
            Scroll.StopMovement();
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                // Destroy는 프레임 끝에 실제 삭제됩니다. 먼저 숨겨야 이전 버튼이 잠시 남아 입력을 받지 않습니다.
                child.SetActive(false);
                Destroy(child);
            }
            // 화면을 새로 만들었으므로 기존 버튼·문구 참조를 비웁니다. 수집 기록을 지우는 작업은 아닙니다.
            _eraButtons.Clear();
            _relicButtons.Clear();
            StatsText = null;
            BuildingStatusText = null;
            ApplyFrameAppearance();
            BackButton.gameObject.SetActive(_data.Page != CollectionPage.Overview);
            _heading.text = _data.Page == CollectionPage.Overview ? "도감" : _data.Page == CollectionPage.Era ? "시대별 기록" : "유물 기록";
            _heading.rectTransform.offsetMin = new Vector2(_data.Page == CollectionPage.Overview ? 0 : 108, -92);
            _subtitle.text = _data.Page == CollectionPage.Overview ? "복원으로 되찾은 우리의 기록" : "기억을 모아, 시간을 이어가다";
            _subtitle.rectTransform.offsetMin = new Vector2(_data.Page == CollectionPage.Overview ? 0 : 110, -134);
            if (_data.Page == CollectionPage.Overview) BuildOverview();
            else if (_data.Page == CollectionPage.Era) BuildEra();
            else BuildRelic();
            // 새 UI도 이 탭의 레이어를 따라야 다른 탭 카메라에 섞여 보이지 않습니다.
            MirrorSceneRoot.SetLayer(_content, gameObject.layer);
            // UI 크기 계산을 먼저 마친 뒤 저장해 둔 스크롤 위치로 돌려놓습니다.
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = Mathf.Clamp01(scrollPosition);
        }

        /// <summary>유물, 시대, 공통 자산의 우선순위로 배경을 선택하고 머리글과 뒤로 버튼 그림을 갱신합니다.</summary>
        private void ApplyFrameAppearance()
        {
            Sprite background = _data.Appearance ? _data.Appearance.PageBackground : null;
            if (_data.Page != CollectionPage.Overview)
            {
                var era = _data.Catalog.FindEra(_data.SelectedEraId);
                if (era != null) background = FirstSprite(era.BackgroundIllustration, background);
                if (_data.Page == CollectionPage.Relic)
                {
                    var relic = _data.Catalog.FindRelic(_data.SelectedRelicId);
                    if (relic != null) background = FirstSprite(relic.BackgroundIllustration, background);
                }
            }
            ApplySurface(_pageBackground, background, Paper);
            ApplySurface(_headerRule, _data.Appearance ? _data.Appearance.Divider : null, new Color(.79f, .79f, .72f));
            ApplySurface((Image)BackButton.targetGraphic, _data.Appearance ? _data.Appearance.BackButtonBackground : null, Paper);
            Sprite icon = _data.Appearance ? _data.Appearance.BackIcon : null;
            _backLabel.gameObject.SetActive(!icon);
            _backIcon.gameObject.SetActive(icon);
            _backIcon.sprite = icon;
            _backIcon.color = Color.white;
        }

        /// <summary>시대별 수집 상태, 신규 표시와 전체 유물 및 건물 통계를 구성합니다.</summary>
        private void BuildOverview()
        {
            float summaryY = 145 + _data.Catalog.Eras.Count * 295;
            _content.sizeDelta = new Vector2(0, summaryY + 290);
            Label("Eyebrow", _content, "시대의 조각을 모으다", 32, Ink, 0, 0, ContentWidth, 54);
            Label("Introduction", _content, "복원을 마친 유물과 정령을 만나보세요.", 26, Muted, 0, 57, ContentWidth, 44);
            for (int i = 0; i < _data.Catalog.Eras.Count; i++)
            {
                var era = _data.Catalog.Eras[i];
                float y = 125 + i * 295;
                var card = MakeButton("Era_" + era.Id, _content, "", 0, y, ContentWidth, 270, Ink,
                    Color.Lerp(Paper, era.Accent, .12f), sprite: _data.Appearance ? _data.Appearance.EraCardBackground : null);
                // 화면상 이름·카드 번호 대신 고정 ID를 보냅니다. 카드 정렬과 디자인을 바꿔도 같은 시대를 가리킵니다.
                card.onClick.AddListener(() => RequestEra(era.Id));
                _eraButtons.Add(era.Id, card);
                Box("Accent", card.transform, era.Accent, 0, 0, 8, 270, _data.Appearance ? _data.Appearance.AccentStrip : null);
                Label("Index", card.transform, "0" + (i + 1), 25, era.Accent, 36, 23, 110, 40);
                Label("Era", card.transform, era.Name, 46, Ink, 36, 80, 530, 66);
                Label("Period", card.transform, era.Period, 25, Muted, 38, 145, 500, 44);
                bool complete = _data.IsBuildingUnlocked(era.Id);
                Sprite completionIcon = _data.Appearance ? _data.Appearance.CompletionIcon : null;
                string count = complete ? (completionIcon ? "수집 완료" : "수집 완료  ✓") :
                    $"{_data.RestoredInEra(era.Id)} / {_data.TotalInEra(era.Id)}";
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
                if (_data.HasUnreadInEra(era.Id)) NewBadge(card.transform, 545, 36);
            }
            Box("Stats Rule", _content, new Color(.79f, .79f, .72f), 0, summaryY, ContentWidth, 2, _data.Appearance ? _data.Appearance.Divider : null);
            Label("Stats Heading", _content, "나의 수집 기록", 30, Ink, 0, summaryY + 24, 440, 48);
            StatsText = Label("Statistics", _content,
                $"복원한 유물  {_data.RestoredCount} / {_data.TotalCount}\n해금한 건물  {_data.CompletedBuildingCount} / {_data.Catalog.Eras.Count}\n수집률  {Percent(_data.CollectionRate)}%",
                28, Ink, 450, summaryY + 22, 534, 154, TextAnchor.MiddleRight);
            ProgressBar(_content, 0, summaryY + 190, ContentWidth, _data.CollectionRate, Gold);
            Label("Sample Notice", _content, "미리보기 · 샘플 수집 기록", 22, Muted, 0, summaryY + 220, ContentWidth, 40);
        }

        /// <summary>선택 시대의 유물 카드, 복원 잠금, 건물 해금 상태와 시대 설명을 구성합니다.</summary>
        private void BuildEra()
        {
            var era = _data.Catalog.FindEra(_data.SelectedEraId);
            Label("Era Name", _content, era.Name, 58, Ink, 0, 0, ContentWidth, 80);
            Label("Era Period", _content, era.Period, 28, Muted, 0, 82, ContentWidth, 46);
            float rate = (float)_data.RestoredInEra(era.Id) / Mathf.Max(1, _data.TotalInEra(era.Id));
            Label("Era Rate", _content, $"수집률 {Percent(rate)}%   ·   {_data.RestoredInEra(era.Id)} / {_data.TotalInEra(era.Id)}", 30, era.Accent,
                0, 150, ContentWidth, 48);
            ProgressBar(_content, 0, 215, ContentWidth, rate, era.Accent);
            var relics = _data.Catalog.Relics.Where(relic => relic.EraId == era.Id).ToArray();
            float buildingY = 290 + Mathf.CeilToInt(relics.Length / 3f) * 380;
            float contextY = buildingY + 340;
            _content.sizeDelta = new Vector2(0, contextY + 670);
            for (int i = 0; i < relics.Length; i++)
            {
                var relic = relics[i];
                bool restored = _data.IsRestored(relic.Id);
                float x = i % 3 * 336;
                float y = 270 + i / 3 * 380;
                var card = MakeButton("Relic_" + relic.Id, _content, "", x, y, 312, 350, Ink,
                    restored ? Color.Lerp(Paper, era.Accent, .14f) : new Color(.89f, .89f, .85f),
                    sprite: _data.Appearance ? (restored ? _data.Appearance.RestoredRelicCardBackground : _data.Appearance.LockedRelicCardBackground) : null);
                // 잠긴 모양과 입력 비활성화는 View의 일입니다. 실제 상세 접근 허용 여부는 Presenter가 다시 검사합니다.
                card.interactable = restored;
                card.onClick.AddListener(() => RequestRelic(relic.Id));
                _relicButtons.Add(relic.Id, card);
                Artwork("Relic Emblem", card.transform, restored ? KindFor(relic) : CollectionArtworkKind.Lock,
                    restored ? era.Accent : Muted, 60, 44, 192, 158,
                    restored ? FirstSprite(relic.Thumbnail, relic.Illustration) : FirstSprite(relic.LockedIllustration, CommonLock));
                Label("Relic Name", card.transform, restored ? relic.Name : "???", 28, Ink, 12, 212, 288, 70, TextAnchor.MiddleCenter);
                Label("Spirit Name", card.transform, restored ? relic.SpiritName : "???", 23, Muted, 12, 284, 288, 50, TextAnchor.MiddleCenter);
                if (_data.IsUnread(relic.Id)) NewBadge(card.transform, 18, 16);
            }
            bool unlocked = _data.IsBuildingUnlocked(era.Id);
            var building = Box("Building Card", _content, Color.Lerp(Paper, era.Accent, .08f), 0, buildingY, ContentWidth, 275,
                _data.Appearance ? _data.Appearance.BuildingCardBackground : null);
            Artwork("Building Emblem", building.transform, unlocked ? CollectionArtworkKind.Building : CollectionArtworkKind.Lock,
                unlocked ? era.Accent : Muted, 38, 42, 205, 170,
                unlocked ? era.BuildingIllustration : FirstSprite(era.LockedBuildingIllustration, CommonLock));
            Label("Building Name", building.transform, unlocked ? era.BuildingName : "시대의 건물", 35, Ink, 278, 32, 675, 60);
            BuildingStatusText = Label("Building Status", building.transform, unlocked ? "해금 완료" : "복원 완료 후 해금", 27,
                era.Accent, 280, 99, 665, 45);
            Label("Building Hint", building.transform, unlocked ? "이 시대의 모든 유물을 복원했습니다." :
                $"유물 {_data.TotalInEra(era.Id)}종을 모두 복원하면\n건물이 해금됩니다.", 26, Muted, 280, 150, 665, 94);
            Label("Era Context Heading", _content, "시대를 들여다보다", 34, Ink, 0, contextY, ContentWidth, 60);
            Artwork("Context Emblem", _content, KindForEra(era.Id), era.Accent, 655, contextY + 100, 270, 220,
                FirstSprite(era.ContextIllustration, era.OverviewIllustration));
            Label("Era Context", _content, era.Description, 28, Muted, 0, contextY + 80, 605, 315, TextAnchor.UpperLeft);
            Label("Sample Notice", _content, "미리보기 · 정령·건물 이름과 설명, 그림은 임시 콘텐츠입니다.", 22, Muted, 0, contextY + 470, ContentWidth, 70);
        }

        /// <summary>선택 유물과 정령의 그림, 설명 및 발굴과 보존 기록을 구성합니다.</summary>
        private void BuildRelic()
        {
            var relic = _data.Catalog.FindRelic(_data.SelectedRelicId);
            var era = _data.Catalog.FindEra(relic.EraId);
            _content.sizeDelta = new Vector2(0, 2020);
            Label("Era Tag", _content, era.Name + "  /  복원 완료", 25, era.Accent, 0, 0, ContentWidth, 45);
            Label("Relic Title", _content, relic.Name, 56, Ink, 0, 52, ContentWidth, 92);
            Label("Spirit Title", _content, "함께 깨어난 정령 · " + relic.SpiritName, 30, Muted, 0, 146, ContentWidth, 60);
            var plate = Box("Illustration Plate", _content, Color.Lerp(Paper, era.Accent, .10f), 0, 248, ContentWidth, 555,
                _data.Appearance ? _data.Appearance.RelicPlateBackground : null);
            Label("Plate Index", plate.transform, "RESTORED  /  " + era.Name, 23, era.Accent, 32, 25, 650, 42);
            Artwork("Relic Illustration", plate.transform, KindFor(relic), era.Accent, 110, 104, 580, 350, relic.Illustration);
            var spiritPlate = Box("Spirit Plate", plate.transform, Paper, 699, 314, 240, 204,
                _data.Appearance ? _data.Appearance.SpiritPlateBackground : null);
            Artwork("Spirit Illustration", spiritPlate.transform, CollectionArtworkKind.Spirit, era.Accent, 38, 12, 165, 141, relic.SpiritIllustration);
            Label("Spirit Caption", spiritPlate.transform, relic.SpiritName, 23, Ink, 8, 150, 224, 45, TextAnchor.MiddleCenter);
            Label("Illustration Caption", plate.transform, relic.Illustration ? "유물과 정령의 기록" : "유물 · 정령 임시 일러스트", 22, Muted, 32, 481, 650, 42);
            Label("Description Heading", _content, "유물이 품은 이야기", 36, Ink, 0, 873, ContentWidth, 64);
            Label("Description", _content, relic.Description, 29, Ink, 0, 950, ContentWidth, 335, TextAnchor.UpperLeft);
            Box("Description Rule", _content, new Color(.79f, .79f, .72f), 0, 1310, ContentWidth, 2,
                _data.Appearance ? _data.Appearance.Divider : null);
            Label("Conservation Heading", _content, "발굴과 보존의 기록", 34, Ink, 0, 1360, ContentWidth, 62);
            Label("Conservation", _content, relic.ConservationNote, 28, Muted, 0, 1445, ContentWidth, 330, TextAnchor.UpperLeft);
            Label("Draft Notice", _content, "미리보기 · 정령·건물 이름과 설명, 그림은 임시 콘텐츠입니다.\n실제 유물 자료와 정령 아트는 추후 교체됩니다.", 22,
                Muted, 0, 1865, ContentWidth, 90);
        }

        /// <summary>부동소수점 경계 오차를 보정한 뒤 수집률의 소수 부분을 버려 정수 백분율로 표시합니다.</summary>
        /// <param name="rate">0에서 1 사이의 수집률입니다.</param>
        /// <returns>화면에 표시할 정수 백분율입니다.</returns>
        private static int Percent(float rate) => Mathf.FloorToInt(rate * 100 + .001f);
        /// <summary>개별 잠금 그림이 없을 때 사용할 공통 그림이며, 미지정이면 null입니다.</summary>
        private Sprite CommonLock => _data.Appearance ? _data.Appearance.LockIllustration : null;
        /// <summary>우선 그림이 유효하면 선택하고, 없으면 대체 그림을 선택합니다.</summary>
        /// <param name="preferred">우선 사용할 그림입니다.</param>
        /// <param name="fallback">우선 그림이 없을 때 사용할 그림입니다.</param>
        /// <returns>선택된 그림이며, 둘 다 없으면 null입니다.</returns>
        private static Sprite FirstSprite(Sprite preferred, Sprite fallback) => preferred ? preferred : fallback;

        /// <summary>표면 그림의 원래 색을 유지하고 테두리가 있으면 Sliced 표현을, 없으면 늘린 표현을 적용합니다.</summary>
        /// <param name="image">그림과 표현 방식을 적용할 UI 표면입니다.</param>
        /// <param name="sprite">표면 그림이며, null이면 단색 표면을 사용합니다.</param>
        /// <param name="fallbackTint">표면 그림이 없을 때 사용할 색상입니다.</param>
        private static void ApplySurface(Image image, Sprite sprite, Color fallbackTint)
        {
            image.sprite = sprite;
            image.color = sprite ? Color.white : fallbackTint;
            image.type = sprite && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
        }

        /// <summary>확정된 유물 ID에 해당하는 임시 벡터 기호를 선택합니다.</summary>
        /// <param name="relic">기호를 선택할 유물 정의입니다.</param>
        /// <returns>유물에 대응하는 기호이며, 알려지지 않은 ID는 그릇 기호를 사용합니다.</returns>
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

        /// <summary>카탈로그 정렬 순서와 관계없이 시대 ID로 대표 기호를 선택합니다.</summary>
        /// <param name="eraId">대표 기호를 선택할 시대 ID입니다.</param>
        /// <returns>시대 대표 기호이며, 알려지지 않은 ID는 건물 기호를 사용합니다.</returns>
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

        /// <summary>부모 좌측 상단을 기준으로 본문의 고정 크기 사각 영역을 생성합니다.</summary>
        /// <param name="name">생성할 오브젝트 이름입니다.</param>
        /// <param name="parent">영역을 포함할 부모입니다.</param>
        /// <param name="x">부모 왼쪽에서 오른쪽으로의 거리입니다.</param>
        /// <param name="y">부모 위쪽에서 아래쪽으로의 거리입니다.</param>
        /// <param name="width">영역의 너비입니다.</param>
        /// <param name="height">영역의 높이입니다.</param>
        /// <returns>생성한 UI 영역입니다.</returns>
        private RectTransform TopRect(string name, Transform parent, float x, float y, float width, float height)
        {
            return UIFactory.Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - height), new Vector2(x + width, -y));
        }

        /// <summary>입력을 가로채지 않는 배경이나 장식 표면을 그림 또는 단색으로 생성합니다.</summary>
        /// <param name="name">생성할 표면 이름입니다.</param>
        /// <param name="parent">표면을 포함할 부모입니다.</param>
        /// <param name="tint">그림이 없을 때 사용할 색상입니다.</param>
        /// <param name="x">부모 왼쪽에서의 거리입니다.</param>
        /// <param name="y">부모 위쪽에서 아래로의 거리입니다.</param>
        /// <param name="width">표면 너비입니다.</param>
        /// <param name="height">표면 높이입니다.</param>
        /// <param name="sprite">표면 그림이며, null이면 단색을 사용합니다.</param>
        /// <returns>생성한 장식 이미지입니다.</returns>
        private Image Box(string name, Transform parent, Color tint, float x, float y, float width, float height, Sprite sprite = null)
        {
            var image = TopRect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            ApplySurface(image, sprite, tint);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>포인터 입력을 받지 않는 한글 레이블을 도감 글꼴과 지정된 정렬로 생성합니다.</summary>
        /// <param name="name">생성할 레이블 이름입니다.</param>
        /// <param name="parent">레이블을 포함할 부모입니다.</param>
        /// <param name="text">표시할 문구입니다.</param>
        /// <param name="size">글꼴 크기입니다.</param>
        /// <param name="tint">글자 색상입니다.</param>
        /// <param name="x">부모 왼쪽에서의 거리입니다.</param>
        /// <param name="y">부모 위쪽에서 아래로의 거리입니다.</param>
        /// <param name="width">레이블 너비입니다.</param>
        /// <param name="height">레이블의 기준 높이입니다.</param>
        /// <param name="alignment">영역 안의 글자 정렬 방식입니다.</param>
        /// <returns>생성한 텍스트 컴포넌트입니다.</returns>
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

        /// <summary>장식 표면에 버튼 입력과 상태 색상을 연결하고 필요한 경우 글자 레이블을 추가합니다.</summary>
        /// <param name="name">생성할 버튼 이름입니다.</param>
        /// <param name="parent">버튼을 포함할 부모입니다.</param>
        /// <param name="text">버튼 문구이며, 비어 있으면 레이블을 만들지 않습니다.</param>
        /// <param name="x">부모 왼쪽에서의 거리입니다.</param>
        /// <param name="y">부모 위쪽에서 아래로의 거리입니다.</param>
        /// <param name="width">버튼 너비입니다.</param>
        /// <param name="height">버튼 높이입니다.</param>
        /// <param name="ink">문구 색상입니다.</param>
        /// <param name="fill">배경 그림이 없을 때 사용할 표면 색상입니다.</param>
        /// <param name="size">문구의 글꼴 크기입니다.</param>
        /// <param name="sprite">버튼 배경 그림이며, null이면 단색을 사용합니다.</param>
        /// <returns>클릭 이벤트를 연결할 버튼입니다.</returns>
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

        /// <summary>원본 색과 비율을 유지하는 그림을 표시하고 그림이 없으면 임시 벡터 기호를 생성합니다.</summary>
        /// <param name="name">생성할 그림 영역 이름입니다.</param>
        /// <param name="parent">그림을 포함할 부모입니다.</param>
        /// <param name="kind">그림이 없을 때 사용할 벡터 기호 종류입니다.</param>
        /// <param name="tint">벡터 기호 색상입니다.</param>
        /// <param name="x">부모 왼쪽에서의 거리입니다.</param>
        /// <param name="y">부모 위쪽에서 아래로의 거리입니다.</param>
        /// <param name="width">그림 영역 너비입니다.</param>
        /// <param name="height">그림 영역 높이입니다.</param>
        /// <param name="sprite">표시할 그림이며, null이면 벡터 기호를 사용합니다.</param>
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
            // 그림이 없을 때 쓰는 임시 기호입니다. 필요한 CanvasRenderer는 CollectionArtwork 자체가 보장합니다.
            else rect.gameObject.AddComponent<CollectionArtwork>().Configure(kind, tint);
        }

        /// <summary>미열람 기록의 신규 배지를 공통 그림 또는 N 글자로 생성합니다.</summary>
        /// <param name="parent">배지를 표시할 시대 또는 유물 카드입니다.</param>
        /// <param name="x">카드 왼쪽에서의 거리입니다.</param>
        /// <param name="y">카드 위쪽에서 아래로의 거리입니다.</param>
        private void NewBadge(Transform parent, float x, float y)
        {
            var badge = Box("New", parent, new Color(.63f, .29f, .20f), x, y, 45, 45,
                _data.Appearance ? _data.Appearance.NewBadgeBackground : null);
            Sprite icon = _data.Appearance ? _data.Appearance.NewBadgeIcon : null;
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

        /// <summary>전체 진행 막대와 0에서 1로 제한한 수집률에 해당하는 채움 영역을 생성합니다.</summary>
        /// <param name="parent">진행 막대를 포함할 부모입니다.</param>
        /// <param name="x">부모 왼쪽에서의 거리입니다.</param>
        /// <param name="y">부모 위쪽에서 아래로의 거리입니다.</param>
        /// <param name="width">전체 진행 막대 너비입니다.</param>
        /// <param name="rate">채움 비율이며, 0 이하이면 채움 영역을 만들지 않습니다.</param>
        /// <param name="tint">채움 그림이 없을 때 사용할 색상입니다.</param>
        private void ProgressBar(Transform parent, float x, float y, float width, float rate, Color tint)
        {
            Box("Progress Track", parent, new Color(.85f, .86f, .81f), x, y, width, 8,
                _data.Appearance ? _data.Appearance.ProgressTrack : null);
            if (rate > 0) Box("Progress Fill", parent, tint, x, y, width * Mathf.Clamp01(rate), 8,
                _data.Appearance ? _data.Appearance.ProgressFill : null);
        }
    }
}
