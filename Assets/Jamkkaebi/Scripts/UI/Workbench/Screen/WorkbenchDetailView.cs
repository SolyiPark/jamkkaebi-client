using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 유물 상세, 정화 중(임시 결과 버튼), 정산 결과, 요청 실패 화면을 그린다.
    /// 홈과 같은 캔버스를 쓰며, 그릴 때마다 이전 화면을 지운다. 버튼은 이벤트로 알린다.
    /// </summary>
    public sealed class WorkbenchDetailView
    {
        private const float PanelTop = .47f;

        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private Text _status;

        public IReadOnlyDictionary<string, Button> Buttons => _buttons;

        public event Action BackClicked;
        public event Action PurifyClicked;
        public event Action<bool> ReportClicked;   // true면 성공으로 보고
        public event Action QuitPlayClicked;
        public event Action ConfirmClicked;
        public event Action RetryClicked;
        public event Action Rebuilt;

        public WorkbenchDetailView(RectTransform root, Font font)
        {
            _root = root ? root : throw new ArgumentNullException(nameof(root));
            _font = font ? font : throw new ArgumentNullException(nameof(font));
        }

        public void RenderLoading()
        {
            Begin();
            UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one);
            _status = WorkbenchUI.Fill("Message", _root, _font, "불러오는 중…", 34, WorkbenchUI.Ink);
            End();
        }

        /// <summary>최초 정화 중인 유물 상세(시안 2번).</summary>
        public void RenderRelic(RelicDetailData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Begin();

            RectTransform top = UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one).rectTransform;
            BackButton(top);
            WorkbenchUI.Box("Artwork", top, ToneOf(data.RevealLevel), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -140), new Vector2(430, 500));

            RectTransform panel = Panel();
            WorkbenchUI.Label("Title", panel, _font, data.Title, 40, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(900, 64)).fontStyle = FontStyle.Bold;
            EraChip(panel, data.EraName, -118);
            WorkbenchUI.Label("Notice", panel, _font, data.Notice, 28, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -186), new Vector2(940, 50));
            Steps(panel, data.Steps, -260);
            ActionButton(panel, "Purify", data.PurifyButton, -560, () => PurifyClicked?.Invoke());
            _status = WorkbenchUI.Label("Status", panel, _font, "", 26, WorkbenchUI.Muted, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -680), new Vector2(940, 50));
            End();
        }

        /// <summary>정화 중. 4단계에서 미니게임으로 바뀔 자리라 결과를 고르는 임시 버튼만 둔다.</summary>
        public void RenderPlaying(string title, string detail)
        {
            Begin();
            UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one);
            WorkbenchUI.Label("Title", _root, _font, title, 40, WorkbenchUI.Ink, new Vector2(.5f, .72f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(940, 70)).fontStyle = FontStyle.Bold;
            WorkbenchUI.Label("Detail", _root, _font, detail, 28, WorkbenchUI.Muted, new Vector2(.5f, .72f),
                new Vector2(.5f, .5f), new Vector2(0, -80), new Vector2(940, 100));
            WorkbenchUI.Label("Placeholder", _root, _font, "[임시] 미니게임은 4단계에서 연결합니다.\n결과를 골라 보고하세요.", 26,
                WorkbenchUI.Muted, new Vector2(.5f, .55f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(940, 100));
            ActionButton(_root, "ReportSuccess", "성공으로 보고", new Vector2(.5f, .42f), () => ReportClicked?.Invoke(true));
            ActionButton(_root, "ReportFailure", "실패로 보고", new Vector2(.5f, .34f), () => ReportClicked?.Invoke(false));
            ActionButton(_root, "QuitPlay", "그만두기", new Vector2(.5f, .26f), () => QuitPlayClicked?.Invoke());
            _status = WorkbenchUI.Label("Status", _root, _font, "", 26, WorkbenchUI.Muted, new Vector2(.5f, .19f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(940, 60));
            End();
        }

        /// <summary>정산 결과. 확인을 누르면 컨트롤러가 다음 화면을 정한다.</summary>
        public void RenderResult(SettlementData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Begin();
            UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one);
            RectTransform panel = Panel();
            WorkbenchUI.Label("Title", panel, _font, data.Title, 44, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -60), new Vector2(900, 70)).fontStyle = FontStyle.Bold;
            WorkbenchUI.Label("Lines", panel, _font, string.Join("\n", data.Lines), 30, WorkbenchUI.Ink,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(940, 260));
            ActionButton(panel, "Confirm", "확인", -470, () => ConfirmClicked?.Invoke());
            End();
        }

        /// <summary>보고처럼 다시 보내도 되는 요청이 실패했을 때. 같은 내용으로 다시 보내거나 홈으로 간다.</summary>
        public void RenderRetry(string message)
        {
            Begin();
            UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one);
            WorkbenchUI.Label("Message", _root, _font, message, 32, WorkbenchUI.Ink, new Vector2(.5f, .62f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(940, 200));
            ActionButton(_root, "Retry", "다시 보내기", new Vector2(.5f, .45f), () => RetryClicked?.Invoke());
            ActionButton(_root, "Home", "홈으로", new Vector2(.5f, .37f), () => BackClicked?.Invoke());
            End();
        }

        public void ShowStatus(string message)
        {
            if (_status) _status.text = message;
        }

        private void Begin()
        {
            WorkbenchUI.ClearChildren(_root);
            _buttons.Clear();
            _status = null;
        }

        private void End() => Rebuilt?.Invoke();

        private RectTransform Panel()
        {
            return UIFactory.Image("Panel", _root, WorkbenchUI.Panel, Vector2.zero, new Vector2(1, PanelTop),
                new Vector2(30, 0), new Vector2(-30, 0)).rectTransform;
        }

        private void BackButton(RectTransform parent)
        {
            Image back = WorkbenchUI.Box("Back", parent, Color.clear, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(24, -24), new Vector2(110, 110));
            Register("Back", WorkbenchUI.Button(back, () => BackClicked?.Invoke()));
            WorkbenchUI.Fill("Arrow", back.transform, _font, "←", 56, WorkbenchUI.Ink);
        }

        private void EraChip(RectTransform parent, string era, float y)
        {
            Image chip = WorkbenchUI.Box("Era", parent, new Color32(210, 225, 250, 255), new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, y), new Vector2(130, 46));
            WorkbenchUI.Fill("Label", chip.transform, _font, era, 24, WorkbenchUI.Ink, TextAnchor.MiddleCenter, 2);
        }

        /// <summary>3단계 진행 표시. 완료·현재·잠김을 색과 글자로 구분한다.</summary>
        private void Steps(RectTransform parent, IReadOnlyList<PhaseStep> steps, float y)
        {
            const float size = 150;
            const float gap = 110;
            float x = -(steps.Count * size + (steps.Count - 1) * gap) / 2 + size / 2;
            for (int i = 0; i < steps.Count; i++)
            {
                PhaseStep step = steps[i];
                if (i > 0)
                {
                    WorkbenchUI.Box("Line_" + i, parent, WorkbenchUI.Ink, new Vector2(.5f, 1), new Vector2(.5f, .5f),
                        new Vector2(x - (size + gap) / 2, y - size / 2), new Vector2(gap, 3));
                }
                Image circle = WorkbenchUI.Box("Step_" + step.Phase, parent,
                    step.State == PhaseStepState.Current ? WorkbenchUI.Card : new Color32(200, 200, 200, 255),
                    new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(x, y), new Vector2(size, size));
                string mark = step.State switch
                {
                    PhaseStepState.Done => "완료",
                    PhaseStepState.Locked => "잠김",
                    _ => "진행"
                };
                WorkbenchUI.Fill("State", circle.transform, _font, mark, 28,
                    step.State == PhaseStepState.Locked ? WorkbenchUI.Muted : WorkbenchUI.Ink);
                WorkbenchUI.Label("Name", parent, _font, step.Name, 28, WorkbenchUI.Ink, new Vector2(.5f, 1),
                    new Vector2(.5f, 1), new Vector2(x, y - size - 14), new Vector2(size + gap, 44));
                x += size + gap;
            }
        }

        private void ActionButton(RectTransform parent, string id, string caption, float y, Action click)
        {
            Image background = WorkbenchUI.Box(id, parent, WorkbenchUI.Card, new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, y), new Vector2(560, 110));
            Register(id, WorkbenchUI.Button(background, click));
            WorkbenchUI.Fill("Caption", background.transform, _font, caption, 32, WorkbenchUI.Ink);
        }

        private void ActionButton(RectTransform parent, string id, string caption, Vector2 anchor, Action click)
        {
            Image background = WorkbenchUI.Box(id, parent, WorkbenchUI.Card, anchor, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(560, 100));
            Register(id, WorkbenchUI.Button(background, click));
            WorkbenchUI.Fill("Caption", background.transform, _font, caption, 32, WorkbenchUI.Ink);
        }

        private void Register(string id, Button button) => _buttons[id] = button;

        private static Color ToneOf(RevealLevel level) => level switch
        {
            RevealLevel.Form => WorkbenchUI.FormTone,
            RevealLevel.Named => WorkbenchUI.NamedTone,
            _ => WorkbenchUI.Silhouette
        };
    }
}
