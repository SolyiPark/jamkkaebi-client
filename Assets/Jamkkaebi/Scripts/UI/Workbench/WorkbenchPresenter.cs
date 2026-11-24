using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    public class WorkbenchPresenter : MonoBehaviour
    {
        public MinigameSessionAdapter adapter;
        public Camera sceneCamera;
        public Text summary;
        public Text feedback;
        public Button[] toolButtons;
        public Button restartButton;

        [Tooltip("켜면 예전처럼 씬 안의 미니게임을 바로 보여 준다. 끄면(기본) 작업대 화면을 띄운다. 미니게임 연결은 4단계에서 Minigame 씬으로 옮긴다.")]
        [SerializeField] private bool _showLegacyMinigame;

        private void Awake()
        {
            if (_showLegacyMinigame) return;
            if (!adapter || !summary)
            {
                Debug.LogError("작업대 화면에는 미니게임 어댑터와 기존 HUD 글자(글꼴) 참조가 필요합니다.", this);
                return;
            }

            // 3단계: 기존 미니게임 보드와 HUD는 숨기고, 실행 중에 작업대 화면을 만든다(씬 파일은 바꾸지 않는다).
            adapter.gameObject.SetActive(false);
            summary.canvas.rootCanvas.gameObject.SetActive(false);

            WorkbenchScreenController screen = GetComponent<WorkbenchScreenController>();
            if (!screen) screen = gameObject.AddComponent<WorkbenchScreenController>();
            SharedRecords shared = SharedRecords.Current;
            screen.Initialize(GetComponent<MirrorSceneRoot>(), summary.font, shared?.Workbench, shared?.Clock, shared?.Recorder);
        }

        private void OnEnable()
        {
            adapter.FeedbackChanged += ShowFeedback;
        }

        private void Start()
        {
            for (int i = 0; i < toolButtons.Length; i++)
            {
                int index = i;
                toolButtons[i].onClick.AddListener(() => adapter.SelectTool((ToolMode)index));
            }
            restartButton.onClick.AddListener(() =>
            {
                if (adapter.CanAcceptInput) adapter.BeginSession();
            });
        }

        private void OnDisable() => adapter.FeedbackChanged -= ShowFeedback;
        private void ShowFeedback(string message) => feedback.text = message;

        private void LateUpdate()
        {
            var session = adapter.Session;
            if (session == null) return;
            float width = session.Grid.Width * adapter.TileSize;
            float height = session.Grid.Height * adapter.TileSize;
            // Reserve the top 20% and bottom 20% for the workbench controls.
            sceneCamera.orthographicSize = Mathf.Max(height / 1.12f,
                width / (1.76f * Mathf.Max(.1f, sceneCamera.aspect)));
            string state = session.State == SessionState.InProgress ? "발굴 중" :
                session.State == SessionState.Succeeded ? "발굴 성공" : "발굴 종료";
            summary.text = $"{state}\n" +
                $"도구 {session.RemainingDestructiveToolUses} · 정찰 {session.RemainingScoutToolUses} · 손상 {session.DamageGauge:P0} · 정성 {session.RevealedBonusTileCount}";
            for (int i = 0; i < toolButtons.Length; i++)
            {
                toolButtons[i].interactable = adapter.CanAcceptInput && session.State == SessionState.InProgress;
                toolButtons[i].targetGraphic.color = i == (int)adapter.SelectedTool
                    ? new Color(.70f, .43f, .26f) : new Color(.48f, .52f, .50f);
            }
            restartButton.interactable = adapter.CanAcceptInput;
        }
    }
}
