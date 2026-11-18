using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>작업대 홈 화면이 데이터대로 슬롯·등록 버튼·탭·정령 카드를 만들고 선택을 알리는지 검사합니다.</summary>
    public sealed class WorkbenchHomeViewTests
    {
        private GameObject _canvas;
        private Font _font;
        private WorkbenchHomeView _view;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            _view = new WorkbenchHomeView((RectTransform)_canvas.transform, _font);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_font);
        }

        private static WorkbenchHomeData Data(bool canRegister, params int[] spiritIds)
        {
            SlotCardData[] slots =
            {
                new SlotCardData(2, RevealLevel.Silhouette, "백제, 향이 스민 산맥", "삼국", Phase.Unseal, true),
                new SlotCardData(5, RevealLevel.Form, "고려, 살결에 새긴 구름", "고려", Phase.Recall, true)
            };
            SpiritCardData[] spirits = spiritIds
                .Select(id => new SpiritCardData(id, "정령" + id, "유물" + id, Clarity.Clear)).ToArray();
            return new WorkbenchHomeData(slots, spirits, canRegister, WorkbenchText.ChooseRelic, 3);
        }

        private Transform Find(string name) =>
            _canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /// <summary>슬롯마다 카드를, 빈칸이 있으면 등록 버튼을 만드는지 검사합니다.</summary>
        [Test]
        public void Render_SlotsAndRegister()
        {
            _view.Render(Data(true));

            Assert.IsNotNull(Find("Slot_2"));
            Assert.IsNotNull(Find("Slot_5"));
            Assert.IsNotNull(Find("Register"));
            Assert.AreEqual(WorkbenchText.ChooseRelic, Find("Guide").GetComponent<Text>().text);
        }

        /// <summary>가득 차면 등록 버튼을 만들지 않는지 검사합니다.</summary>
        [Test]
        public void Render_FullHidesRegister()
        {
            _view.Render(Data(false));

            Assert.IsNull(Find("Register"));
        }

        /// <summary>정령 카드를 3열로 놓고, 행 수에 맞게 스크롤 내용 높이를 잡는지 검사합니다.</summary>
        [Test]
        public void Render_SpiritGrid()
        {
            _view.Render(Data(true, 1, 2, 3, 4));

            RectTransform first = (RectTransform)Find("Spirit_1");
            RectTransform fourth = (RectTransform)Find("Spirit_4");
            Assert.AreEqual(first.anchoredPosition.x, fourth.anchoredPosition.x, 0.01f);
            Assert.Less(fourth.anchoredPosition.y, first.anchoredPosition.y);
            Assert.IsNotNull(_view.Scroll);
            Assert.Greater(_view.Scroll.content.sizeDelta.y, 2 * 300);
        }

        /// <summary>건물 탭을 고르면 정령 카드 대신 준비 중 안내를 보이는지 검사합니다.</summary>
        [Test]
        public void SelectTab_Buildings_ShowsComingSoon()
        {
            _view.Render(Data(true, 1));

            _view.SelectTab(WorkbenchHomeView.ListTab.Buildings);

            Assert.IsNull(Find("Spirit_1"));
            Assert.IsNotNull(Find("ComingSoon"));
            Assert.IsNull(_view.Scroll);
        }

        /// <summary>슬롯·정령 카드·등록 버튼을 누르면 해당 이벤트가 relicId와 함께 오는지 검사합니다.</summary>
        [Test]
        public void Clicks_RaiseEvents()
        {
            int slot = 0, spirit = 0, register = 0;
            _view.SlotSelected += id => slot = id;
            _view.SpiritSelected += id => spirit = id;
            _view.RegisterClicked += () => register++;
            _view.Render(Data(true, 7));

            Find("Slot_5").GetComponent<Button>().onClick.Invoke();
            Find("Spirit_7").GetComponent<Button>().onClick.Invoke();
            Find("Register").GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(5, slot);
            Assert.AreEqual(7, spirit);
            Assert.AreEqual(1, register);
        }

        /// <summary>다시 그리면 이전 카드가 남지 않고, 그릴 때마다 Rebuilt를 알리는지 검사합니다.</summary>
        [Test]
        public void Render_Again_ReplacesChildren()
        {
            int rebuilt = 0;
            _view.Rebuilt += () => rebuilt++;
            _view.Render(Data(true, 1, 2));

            _view.Render(Data(true));

            Assert.IsNull(Find("Spirit_1"));
            Assert.AreEqual(1, _canvas.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Slot_2"));
            Assert.AreEqual(2, rebuilt);
        }

        /// <summary>메시지 화면은 안내 문구만 보이고, 상태 문구를 바꿀 수 있는지 검사합니다.</summary>
        [Test]
        public void RenderMessage_ThenStatus()
        {
            _view.RenderMessage("불러오는 중");
            Assert.AreEqual("불러오는 중", Find("Message").GetComponent<Text>().text);

            _view.ShowStatus("실패");
            Assert.AreEqual("실패", Find("Message").GetComponent<Text>().text);
        }
    }
}
