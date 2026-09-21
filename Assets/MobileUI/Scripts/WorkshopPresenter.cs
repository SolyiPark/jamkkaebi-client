using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype
{
    public class WorkshopPresenter : MonoBehaviour
    {
        public MinigameSessionAdapter adapter;
        public Camera sceneCamera;
        public Text summary;
        public Text feedback;
        public Button[] toolButtons;
        public Button restartButton;

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
            // Reserve the top 20% and bottom 20% for the workshop controls.
            sceneCamera.orthographicSize = Mathf.Max(height / 1.12f,
                width / (1.76f * Mathf.Max(.1f, sceneCamera.aspect)));
            string state = session.State == SessionState.InProgress ? "발굴 중" :
                session.State == SessionState.Succeeded ? "발굴 성공" : "발굴 종료";
            summary.text = $"{state} · {Mathf.CeilToInt(session.RemainingSeconds)}초\n" +
                $"도구 {session.RemainingDestructiveToolUses} · 정찰 {session.RemainingScoutToolUses} · 손상 {session.DamageGauge:P0}";
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
