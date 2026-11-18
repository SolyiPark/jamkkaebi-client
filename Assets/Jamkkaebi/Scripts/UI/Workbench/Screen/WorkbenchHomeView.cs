using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.UI.Collection;
using Jamkkaebi.Scripts.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 작업대 홈 화면. 위쪽은 진행 중 유물 슬롯과 등록 버튼, 아래쪽은 정령·건물 탭과 카드 그리드.
    /// 그리기만 하고, 사용자의 선택은 이벤트로 알린다(백엔드 호출은 컨트롤러가 한다).
    /// </summary>
    public sealed class WorkbenchHomeView
    {
        public enum ListTab
        {
            Spirits = 0,
            Buildings = 1
        }

        private const float SlotWidth = 290;
        private const float SlotHeight = 340;
        private const float SlotGap = 26;
        private const float SidePadding = 54;
        private const int Columns = 3;
        private const float CardHeight = 330;
        private const float CardGap = 26;

        private readonly RectTransform _root;
        private readonly Font _font;
        private WorkbenchHomeData _data;
        private Text _status;

        public ListTab Tab { get; private set; } = ListTab.Spirits;
        public CollectionScrollRect Scroll { get; private set; }

        /// <summary>진행 중 유물 슬롯을 눌렀다(relicId).</summary>
        public event Action<int> SlotSelected;

        /// <summary>정령 카드를 눌렀다(relicId).</summary>
        public event Action<int> SpiritSelected;

        /// <summary>"신규 미확인 유물 등록"을 눌렀다.</summary>
        public event Action RegisterClicked;

        /// <summary>화면을 새로 만들었다. 새 오브젝트에 콘텐츠 레이어를 입히는 데 쓴다.</summary>
        public event Action Rebuilt;

        public WorkbenchHomeView(RectTransform root, Font font)
        {
            _root = root ? root : throw new ArgumentNullException(nameof(root));
            _font = font ? font : throw new ArgumentNullException(nameof(font));
        }

        /// <summary>불러오는 중이거나 실패했을 때 안내 문구만 보인다.</summary>
        public void RenderMessage(string message)
        {
            WorkbenchUI.ClearChildren(_root);
            _data = null;
            Scroll = null;
            UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one);
            _status = WorkbenchUI.Fill("Message", _root, _font, message, 34, WorkbenchUI.Ink);
            Rebuilt?.Invoke();
        }

        public void Render(WorkbenchHomeData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            WorkbenchUI.ClearChildren(_root);

            RectTransform top = UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop,
                new Vector2(0, .42f), Vector2.one).rectTransform;
            BuildSlots(top, data);

            RectTransform bottom = UIFactory.Image("Panel", _root, WorkbenchUI.Panel,
                Vector2.zero, new Vector2(1, .42f)).rectTransform;
            BuildList(bottom, data);
            Rebuilt?.Invoke();
        }

        /// <summary>요청 결과처럼 잠깐 알릴 문장을 안내 문구 자리에 보인다. 다시 그리면 원래 안내로 돌아간다.</summary>
        public void ShowStatus(string message)
        {
            if (_status) _status.text = message;
        }

        public void SelectTab(ListTab tab)
        {
            Tab = tab;
            if (_data != null) Render(_data);
        }

        private void BuildSlots(RectTransform top, WorkbenchHomeData data)
        {
            int count = data.Slots.Count + (data.CanRegister ? 1 : 0);
            float total = count * SlotWidth + Math.Max(0, count - 1) * SlotGap;
            float x = -total / 2 + SlotWidth / 2;
            Vector2 anchor = new Vector2(.5f, .58f);

            foreach (SlotCardData slot in data.Slots)
            {
                Image card = WorkbenchUI.Box("Slot_" + slot.RelicId, top, ToneOf(slot.RevealLevel), anchor,
                    new Vector2(.5f, .5f), new Vector2(x, 0), new Vector2(SlotWidth, SlotHeight));
                int relicId = slot.RelicId;
                WorkbenchUI.Button(card, () => SlotSelected?.Invoke(relicId));
                WorkbenchUI.Fill("Title", card.transform, _font, slot.Title, 28, Color.white, TextAnchor.MiddleCenter, 18);
                WorkbenchUI.Label("Era", card.transform, _font, slot.EraName, 22, new Color(1, 1, 1, .75f),
                    new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -12), new Vector2(SlotWidth, 36));
                WorkbenchUI.Label("Next", card.transform, _font, "다음 · " + WorkbenchText.PhaseName(slot.NextPhase), 22,
                    new Color(1, 1, 1, .75f), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 12),
                    new Vector2(SlotWidth, 36));
                x += SlotWidth + SlotGap;
            }

            if (data.CanRegister)
            {
                Image register = WorkbenchUI.Box("Register", top, WorkbenchUI.Panel, anchor, new Vector2(.5f, .5f),
                    new Vector2(x, 0), new Vector2(SlotWidth * .82f, 150));
                WorkbenchUI.Button(register, () => RegisterClicked?.Invoke());
                WorkbenchUI.Fill("Caption", register.transform, _font, WorkbenchText.RegisterButton, 30, WorkbenchUI.Ink);
            }

            _status = WorkbenchUI.Label("Guide", top, _font, data.Guide, 32, WorkbenchUI.Ink,
                new Vector2(.5f, .58f), new Vector2(.5f, 1), new Vector2(0, -SlotHeight / 2 - 50), new Vector2(980, 90));
        }

        private void BuildList(RectTransform bottom, WorkbenchHomeData data)
        {
            float tabWidth = (1080 - SidePadding * 2 - 20) / 2;
            BuildTab(bottom, "Tab_Spirits", "정령", ListTab.Spirits, SidePadding, tabWidth);
            BuildTab(bottom, "Tab_Buildings", "건물", ListTab.Buildings, SidePadding + tabWidth + 20, tabWidth);

            if (Tab == ListTab.Buildings)
            {
                Scroll = null;
                WorkbenchUI.Label("ComingSoon", bottom, _font, WorkbenchText.BuildingsComingSoon, 30, WorkbenchUI.Muted,
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -40), new Vector2(900, 80));
                return;
            }

            RectTransform content = MakeScroll(bottom, top: 130);
            if (data.Spirits.Count == 0)
            {
                WorkbenchUI.Label("Empty", content, _font, "정화를 모두 마친 정령이 여기에 모여요.", 28, WorkbenchUI.Muted,
                    new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(900, 80));
                return;
            }

            float width = 1080 - SidePadding * 2;
            float cardWidth = (width - CardGap * (Columns - 1)) / Columns;
            for (int i = 0; i < data.Spirits.Count; i++)
            {
                SpiritCardData spirit = data.Spirits[i];
                int row = i / Columns;
                int column = i % Columns;
                Image card = WorkbenchUI.Box("Spirit_" + spirit.RelicId, content, WorkbenchUI.Card, new Vector2(0, 1),
                    new Vector2(0, 1), new Vector2(column * (cardWidth + CardGap), -row * (CardHeight + CardGap)),
                    new Vector2(cardWidth, CardHeight));
                int relicId = spirit.RelicId;
                WorkbenchUI.Button(card, () => SpiritSelected?.Invoke(relicId));

                Image chip = WorkbenchUI.Box("Clarity", card.transform, ClarityTone(spirit.Clarity), new Vector2(0, 1),
                    new Vector2(0, 1), new Vector2(14, -14), new Vector2(104, 42));
                WorkbenchUI.Fill("Label", chip.transform, _font, spirit.ClarityName, 22, WorkbenchUI.Ink, TextAnchor.MiddleCenter, 2);

                WorkbenchUI.Label("Names", card.transform, _font, spirit.SpiritName + "\n" + spirit.RelicName, 28,
                    WorkbenchUI.Ink, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24),
                    new Vector2(cardWidth - 16, 110));
            }

            int rows = (data.Spirits.Count + Columns - 1) / Columns;
            content.sizeDelta = new Vector2(0, rows * CardHeight + (rows - 1) * CardGap + 40);
        }

        private void BuildTab(RectTransform bottom, string name, string caption, ListTab tab, float x, float width)
        {
            bool selected = Tab == tab;
            Image background = WorkbenchUI.Box(name, bottom, selected ? WorkbenchUI.Ink : WorkbenchUI.Card,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -26), new Vector2(width, 82));
            WorkbenchUI.Button(background, () => SelectTab(tab));
            WorkbenchUI.Fill("Caption", background.transform, _font, caption, 30, selected ? Color.white : WorkbenchUI.Ink);
        }

        /// <summary>세로 스크롤. 가로 드래그는 CollectionScrollRect가 공통 탭 전환에 넘긴다.</summary>
        private RectTransform MakeScroll(RectTransform parent, float top)
        {
            RectTransform viewport = UIFactory.Rect("Spirit Scroll", parent, Vector2.zero, Vector2.one,
                new Vector2(SidePadding, 0), new Vector2(-SidePadding, -top));
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            Scroll = viewport.gameObject.AddComponent<CollectionScrollRect>();
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = 35;
            Scroll.decelerationRate = .08f;

            RectTransform content = UIFactory.Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 400);
            Scroll.viewport = viewport;
            Scroll.content = content;
            return content;
        }

        private static Color ToneOf(RevealLevel level) => level switch
        {
            RevealLevel.Form => WorkbenchUI.FormTone,
            RevealLevel.Named => WorkbenchUI.NamedTone,
            _ => WorkbenchUI.Silhouette
        };

        private static Color ClarityTone(Clarity clarity) => clarity switch
        {
            Clarity.Faded => WorkbenchUI.Faded,
            Clarity.Resealed => WorkbenchUI.Resealed,
            _ => WorkbenchUI.Clear
        };
    }
}
