using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.UI.Collection;
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
        private const float RelicPanelHeight = 760;
        private const float ResultPanelHeight = 620;

        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private Text _status;

        public IReadOnlyDictionary<string, Button> Buttons => _buttons;

        /// <summary>
        /// 상세·결과 패널의 세로 스크롤. 세로가 짧은 화면에서도 버튼과 안내 문구까지 내려 볼 수 있다.
        /// 가로 드래그는 CollectionScrollRect가 공통 탭 전환에 넘긴다. 패널이 없는 화면에서는 null.
        /// </summary>
        public CollectionScrollRect Scroll { get; private set; }

        public event Action BackClicked;
        public event Action PurifyClicked;
        public event Action ConfirmClicked;
        public event Action RetryClicked;
        public event Action<MinigameDifficulty> DifficultySelected;
        public event Action RenameClicked;
        public event Action<string> RenameSubmitted;
        public event Action RenameCancelled;
        public event Action Rebuilt;

        /// <summary>이름 바꾸기 입력칸. 열려 있을 때만 값이 있다.</summary>
        public InputField RenameInput { get; private set; }

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

            RectTransform panel = Panel(PanelTop, RelicPanelHeight);
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

        /// <summary>최초 정화를 마친 정령의 상세(시안 3번, 성장 대신 친밀도).</summary>
        /// <param name="selected">지금 고른 난이도. 고를 수 없는 계획이면 무시한다.</param>
        public void RenderSpirit(SpiritDetailData data, MinigameDifficulty selected)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Begin();

            RectTransform top = UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one).rectTransform;
            BackButton(top);
            Image artwork = WorkbenchUI.Box("Artwork", top, WorkbenchUI.NamedTone, new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -120), new Vector2(380, 420));
            WorkbenchUI.Fill("Clarity", artwork.transform, _font, data.ClarityName, 30, Color.white);

            float buttonY = data.DifficultySelectable ? -652 : -540;
            RectTransform panel = Panel(.6f, -(buttonY - 118 - 44) + 30);
            WorkbenchUI.Label("Title", panel, _font, data.SpiritName, 40, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -34), new Vector2(700, 60)).fontStyle = FontStyle.Bold;
            Image rename = WorkbenchUI.Box("Rename", panel, WorkbenchUI.Card, new Vector2(.5f, 1), new Vector2(0, 1),
                new Vector2(300, -36), new Vector2(150, 56));
            Register("Rename", WorkbenchUI.Button(rename, () => RenameClicked?.Invoke()));
            WorkbenchUI.Fill("Caption", rename.transform, _font, "이름 바꾸기", 22, WorkbenchUI.Ink, TextAnchor.MiddleCenter, 2);
            WorkbenchUI.Label("RelicName", panel, _font, data.RelicName, 26, WorkbenchUI.Muted, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -96), new Vector2(900, 40));
            EraChip(panel, data.EraName, -142);

            WorkbenchUI.Label("IntimacyLabel", panel, _font, IntimacyText(data), 28, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -204), new Vector2(900, 44));
            Image bar = WorkbenchUI.Box("IntimacyBar", panel, WorkbenchUI.Card, new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -252), new Vector2(620, 26));
            UIFactory.Image("Fill", bar.transform, new Color32(255, 150, 40, 255), Vector2.zero,
                new Vector2(Mathf.Clamp01(data.Intimacy.Fill), 1));

            Steps(panel, data.RestoreSteps, -302, 110, 120);
            WorkbenchUI.Label("Days", panel, _font, data.DaysSincePurified, 28, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -480), new Vector2(900, 44));

            if (data.DifficultySelectable) DifficultyRow(panel, data, selected, -540);
            ActionButton(panel, "Purify", data.PurifyButton, buttonY, () => PurifyClicked?.Invoke());
            _status = WorkbenchUI.Label("Status", panel, _font, "", 26, WorkbenchUI.Muted, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, buttonY - 118), new Vector2(940, 44));
            End();
        }

        /// <summary>이름 바꾸기 입력창을 지금 화면 위에 띄운다. 비워 두면 기본 이름으로 되돌린다.</summary>
        /// <param name="current">지금 이름. 입력칸의 처음 값.</param>
        /// <param name="defaultName">기본 이름. 입력칸을 비우면 흐리게 보여 "비우고 저장하면 이 이름"임을 알린다.</param>
        public void ShowRenameSheet(string current, string defaultName)
        {
            RectTransform shade = UIFactory.Image("Rename Shade", _root, new Color(0, 0, 0, .4f), Vector2.zero, Vector2.one).rectTransform;
            shade.GetComponent<Image>().raycastTarget = true;
            Image sheet = WorkbenchUI.Box("Rename Sheet", shade, WorkbenchUI.Panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 120), new Vector2(940, 520));
            WorkbenchUI.Label("Title", sheet.transform, _font, "정령 이름 바꾸기", 36, WorkbenchUI.Ink, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -36), new Vector2(880, 56)).fontStyle = FontStyle.Bold;
            WorkbenchUI.Label("Hint", sheet.transform, _font,
                $"{SpiritNameRule.MinLength}~{SpiritNameRule.MaxLength}자. 비워 두면 기본 이름으로 돌아가요.", 24,
                WorkbenchUI.Muted, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -100), new Vector2(880, 40));

            Image field = WorkbenchUI.Box("Name Input", sheet.transform, WorkbenchUI.Backdrop, new Vector2(.5f, 1),
                new Vector2(.5f, 1), new Vector2(0, -160), new Vector2(800, 96));
            field.raycastTarget = true;
            RenameInput = field.gameObject.AddComponent<InputField>();
            Text inputText = WorkbenchUI.Fill("Input Text", field.transform, _font, "", 34, WorkbenchUI.Ink, TextAnchor.MiddleLeft, 20);
            inputText.supportRichText = false;
            RenameInput.textComponent = inputText;
            RenameInput.placeholder = WorkbenchUI.Fill("Placeholder", field.transform, _font, defaultName, 34, WorkbenchUI.Muted,
                TextAnchor.MiddleLeft, 20);
            RenameInput.lineType = InputField.LineType.SingleLine;
            // 이모지는 UTF-16 두 칸이라 넉넉히 받고, 실제 길이 검사는 SpiritNameRule이 코드포인트로 한다.
            RenameInput.characterLimit = SpiritNameRule.MaxLength * 2;
            RenameInput.text = current;

            Text error = WorkbenchUI.Label("Error", sheet.transform, _font, "", 24, new Color32(200, 60, 60, 255),
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -268), new Vector2(880, 40));
            RenameInput.onValueChanged.AddListener(value =>
            {
                error.text = SpiritNameRule.TryNormalize(value, out _, out string message) ? "" : message;
                if (_buttons.TryGetValue("RenameSave", out Button save)) save.interactable = message == null;
            });

            Image cancel = WorkbenchUI.Box("RenameCancel", sheet.transform, WorkbenchUI.Card, new Vector2(.5f, 0),
                new Vector2(1, 0), new Vector2(-12, 36), new Vector2(300, 96));
            Register("RenameCancel", WorkbenchUI.Button(cancel, () => RenameCancelled?.Invoke()));
            WorkbenchUI.Fill("Caption", cancel.transform, _font, "취소", 30, WorkbenchUI.Ink);
            Image save = WorkbenchUI.Box("RenameSave", sheet.transform, WorkbenchUI.Ink, new Vector2(.5f, 0),
                new Vector2(0, 0), new Vector2(12, 36), new Vector2(300, 96));
            Register("RenameSave", WorkbenchUI.Button(save, () => RenameSubmitted?.Invoke(RenameInput.text)));
            WorkbenchUI.Fill("Caption", save.transform, _font, "저장", 30, Color.white);
            _status = error;
            End();
        }

        public void HideRenameSheet()
        {
            Transform shade = _root.Find("Rename Shade");
            if (shade)
            {
                shade.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(shade.gameObject);
                else UnityEngine.Object.DestroyImmediate(shade.gameObject);
            }
            _buttons.Remove("RenameSave");
            _buttons.Remove("RenameCancel");
            RenameInput = null;
        }

        /// <summary>정화 중. 미니게임이 셸을 가리는 동안에는 보이지 않고, 미니게임이 내려간 뒤 보고 응답을 기다리는 동안 보인다.</summary>
        public void RenderPlaying(string title, string detail)
        {
            Begin();
            UIFactory.Image("Backdrop", _root, WorkbenchUI.Backdrop, Vector2.zero, Vector2.one);
            WorkbenchUI.Label("Title", _root, _font, title, 40, WorkbenchUI.Ink, new Vector2(.5f, .72f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(940, 70)).fontStyle = FontStyle.Bold;
            WorkbenchUI.Label("Detail", _root, _font, detail, 28, WorkbenchUI.Muted, new Vector2(.5f, .72f),
                new Vector2(.5f, .5f), new Vector2(0, -80), new Vector2(940, 100));
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
            RectTransform panel = Panel(PanelTop, ResultPanelHeight);
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
            RenameInput = null;
            Scroll = null;
        }

        private void End() => Rebuilt?.Invoke();

        /// <summary>
        /// 아래쪽 흰 패널과 그 안의 세로 스크롤을 만들고, 내용을 놓을 Content를 돌려준다.
        /// 내용은 Content 위쪽 기준 좌표로 놓으며, 높이가 패널보다 크면 스크롤된다.
        /// </summary>
        private RectTransform Panel(float top, float contentHeight)
        {
            RectTransform panel = UIFactory.Image("Panel", _root, WorkbenchUI.Panel, Vector2.zero, new Vector2(1, top),
                new Vector2(30, 0), new Vector2(-30, 0)).rectTransform;

            RectTransform viewport = UIFactory.Rect("Panel Scroll", panel, Vector2.zero, Vector2.one);
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
            content.sizeDelta = new Vector2(0, contentHeight);
            Scroll.viewport = viewport;
            Scroll.content = content;
            return content;
        }

        private static string IntimacyText(SpiritDetailData data)
        {
            string progress = data.Intimacy.IsMax ? "최고 단계" : $"{data.Intimacy.Current}/{data.Intimacy.Max}";
            return $"친밀도 Lv.{data.Intimacy.Level} · {progress}";
        }

        /// <summary>관리 플레이 난이도 선택. 고른 난이도는 진하게 칠한다.</summary>
        private void DifficultyRow(RectTransform parent, SpiritDetailData data, MinigameDifficulty selected, float y)
        {
            const float width = 260;
            const float gap = 20;
            int count = data.Difficulties.Count;
            float x = -(count * width + (count - 1) * gap) / 2 + width / 2;
            foreach (DifficultyOption option in data.Difficulties)
            {
                bool chosen = option.Difficulty == selected;
                Image background = WorkbenchUI.Box("Difficulty_" + option.Difficulty, parent,
                    chosen ? WorkbenchUI.Ink : WorkbenchUI.Card, new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(x, y), new Vector2(width, 96));
                MinigameDifficulty difficulty = option.Difficulty;
                Register("Difficulty_" + option.Difficulty, WorkbenchUI.Button(background, () => DifficultySelected?.Invoke(difficulty)));
                WorkbenchUI.Fill("Caption", background.transform, _font, option.Name + "\n" + option.EffectText, 24,
                    chosen ? Color.white : WorkbenchUI.Ink, TextAnchor.MiddleCenter, 4);
                x += width + gap;
            }
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
        private void Steps(RectTransform parent, IReadOnlyList<PhaseStep> steps, float y, float size = 150, float gap = 110)
        {
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
