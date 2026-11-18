using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>유물 상세·정화 중·결과·재전송 화면이 버튼을 만들고 선택을 이벤트로 알리는지 검사합니다.</summary>
    public sealed class WorkbenchDetailViewTests
    {
        private GameObject _canvas;
        private Font _font;
        private WorkbenchDetailView _view;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Test Canvas", typeof(RectTransform), typeof(Canvas));
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            _view = new WorkbenchDetailView((RectTransform)_canvas.transform, _font);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_font);
        }

        private Transform Find(string name) =>
            _canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static RelicDetailData Relic() => new RelicDetailData(2, RevealLevel.Form, "백제, 향이 스민 산맥", "삼국",
            WorkbenchViewDataBuilder.FirstRouteSteps(Phase.Recall), WorkbenchText.PurifyButton);

        /// <summary>유물 상세는 제목·시대·3단계 진행 표시·정화 버튼·뒤로가기를 만드는지 검사합니다.</summary>
        [Test]
        public void RenderRelic_BuildsParts()
        {
            _view.RenderRelic(Relic());

            Assert.AreEqual("백제, 향이 스민 산맥", Find("Title").GetComponent<Text>().text);
            Assert.IsNotNull(Find("Step_Unseal"));
            Assert.IsNotNull(Find("Step_Recall"));
            Assert.IsNotNull(Find("Step_Guard"));
            Assert.AreEqual("완료", Find("Step_Unseal").Find("State").GetComponent<Text>().text);
            Assert.AreEqual("진행", Find("Step_Recall").Find("State").GetComponent<Text>().text);
            Assert.AreEqual("잠김", Find("Step_Guard").Find("State").GetComponent<Text>().text);
            Assert.IsTrue(_view.Buttons.ContainsKey("Purify"));
            Assert.IsTrue(_view.Buttons.ContainsKey("Back"));
        }

        /// <summary>상세의 정화·뒤로가기 버튼이 이벤트를 알리는지 검사합니다.</summary>
        [Test]
        public void RenderRelic_ButtonsRaiseEvents()
        {
            int purify = 0, back = 0;
            _view.PurifyClicked += () => purify++;
            _view.BackClicked += () => back++;
            _view.RenderRelic(Relic());

            _view.Buttons["Purify"].onClick.Invoke();
            _view.Buttons["Back"].onClick.Invoke();

            Assert.AreEqual(1, purify);
            Assert.AreEqual(1, back);
        }

        /// <summary>정화 중 화면의 성공·실패·그만두기 버튼이 이벤트를 알리는지 검사합니다.</summary>
        [Test]
        public void RenderPlaying_ReportButtons()
        {
            bool? reported = null;
            int quit = 0;
            _view.ReportClicked += success => reported = success;
            _view.QuitPlayClicked += () => quit++;
            _view.RenderPlaying("기억 회복", "백제, 향이 스민 산맥");

            _view.Buttons["ReportFailure"].onClick.Invoke();
            Assert.AreEqual(false, reported);
            _view.Buttons["ReportSuccess"].onClick.Invoke();
            Assert.AreEqual(true, reported);
            _view.Buttons["QuitPlay"].onClick.Invoke();
            Assert.AreEqual(1, quit);
        }

        /// <summary>결과 화면은 제목과 문장을 보이고 확인을 알리는지 검사합니다.</summary>
        [Test]
        public void RenderResult_ShowsLinesAndConfirm()
        {
            int confirm = 0;
            _view.ConfirmClicked += () => confirm++;
            _view.RenderResult(new SettlementData(2, true, "정화 성공", new[] { "가", "나" }, false));

            Assert.AreEqual("정화 성공", Find("Title").GetComponent<Text>().text);
            Assert.AreEqual("가\n나", Find("Lines").GetComponent<Text>().text);
            _view.Buttons["Confirm"].onClick.Invoke();
            Assert.AreEqual(1, confirm);
        }

        /// <summary>재전송 화면은 다시 보내기와 홈으로 버튼을 만드는지 검사합니다.</summary>
        [Test]
        public void RenderRetry_Buttons()
        {
            int retry = 0, back = 0;
            _view.RetryClicked += () => retry++;
            _view.BackClicked += () => back++;
            _view.RenderRetry("실패");

            _view.Buttons["Retry"].onClick.Invoke();
            _view.Buttons["Home"].onClick.Invoke();

            Assert.AreEqual(1, retry);
            Assert.AreEqual(1, back);
        }

        /// <summary>다른 화면을 그리면 이전 버튼이 사라지는지 검사합니다.</summary>
        [Test]
        public void Render_ReplacesPreviousPage()
        {
            _view.RenderRelic(Relic());
            _view.RenderPlaying("기억 회복", "");

            Assert.IsFalse(_view.Buttons.ContainsKey("Purify"));
            Assert.IsNull(Find("Step_Unseal"));
            Assert.IsTrue(_view.Buttons.ContainsKey("ReportSuccess"));
        }
    }
}
