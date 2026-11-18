using System;
using System.Collections;
using System.Text;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using Jamkkaebi.Scripts.UI.Workbench;
using UnityEditor;
using UnityEngine;

namespace Jamkkaebi.Editor
{
    /// <summary>
    /// MockWorkbenchBackend를 손으로 돌려 보는 개발용 창. 상태는 창이 열려 있는 동안 메모리에만 있다.
    /// 지연 0으로 만들어 코루틴을 그 자리에서 끝까지 돌리므로 Play 모드가 필요 없다.
    /// </summary>
    public sealed class WorkbenchMockConsole : EditorWindow
    {
        private const int TotalRelics = 10;

        private MockWorkbenchBackend _backend;
        private DateTime _startTime;
        private double _offsetDays;           // 시계를 앞으로 돌린 일수
        private int _seed = 1;

        private string _lastRequestId;        // "같은 번호로 다시 보내기" 용
        private BoxSource _lastSource;        // 다시 보낼 때 원래 경로를 그대로 쓴다
        private int _selectedRelicId = 1;
        private MinigameDifficulty _difficulty = MinigameDifficulty.Normal;
        private bool _difficultyNull = true;  // 최초 정화처럼 난이도를 비워 보내기
        private string _lastToken;
        private bool _autoRefresh = true;     // 동작 뒤 작업대를 자동으로 조회

        private readonly StringBuilder _log = new StringBuilder();
        private Vector2 _scroll;

        [MenuItem("Jamkkaebi/Workbench/Mock Backend Console")]
        private static void Open() => GetWindow<WorkbenchMockConsole>("작업대 모의 백엔드");

        // 창을 열거나 스크립트를 다시 컴파일하면 모의 상태는 처음부터 시작한다.
        private void OnEnable() => ResetBackend();

        private DateTime Now => _startTime + TimeSpan.FromDays(_offsetDays);

        private void ResetBackend()
        {
            _startTime = DateTime.UtcNow;
            _offsetDays = 0;
            _lastRequestId = null;
            _lastToken = null;
            _log.Clear();

            RelicInventory inventory = new RelicInventory(ClarityPolicy.Default,
                RelicInventory.DefaultWorkbenchCapacity, Array.Empty<OwnedRelic>());
            PlayPlanner planner = new PlayPlanner(ClarityPolicy.Default, RestorePolicy.Default,
                MinigameDifficulty.Normal);
            _backend = new MockWorkbenchBackend(inventory, planner, TotalRelics,
                new System.Random(_seed), () => Now, 0f);

            Log($"새 모의 백엔드 (전체 유물 {TotalRelics}개, 용량 {RelicInventory.DefaultWorkbenchCapacity}, 시드 {_seed})");
        }

        private void OnGUI()
        {
            // 1) 준비
            EditorGUILayout.LabelField("준비", EditorStyles.boldLabel);
            _seed = EditorGUILayout.IntField("난수 시드", _seed);
            _autoRefresh = EditorGUILayout.Toggle("동작 뒤 작업대 자동 조회", _autoRefresh);
            if (GUILayout.Button("처음부터 다시")) ResetBackend();

            // 2) 시계
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("시계", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("지금 (UTC)", $"{Now:yyyy-MM-dd HH:mm}  (+{_offsetDays:0.##}일)");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1시간")) Advance(1.0 / 24);
                if (GUILayout.Button("+1일")) Advance(1);
                if (GUILayout.Button("+7일")) Advance(7);
            }

            // 3) 작업대
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("작업대", EditorStyles.boldLabel);
            if (GUILayout.Button("작업대 조회")) ShowWorkbench();

            // 4) 상자
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("상자", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("무료로 열기")) OpenBox(BoxSource.Free, Guid.NewGuid().ToString());
                if (GUILayout.Button("선물로 열기")) OpenBox(BoxSource.Gift, Guid.NewGuid().ToString());
                if (GUILayout.Button("구매로 열기")) OpenBox(BoxSource.Purchase, Guid.NewGuid().ToString());
            }
            using (new EditorGUI.DisabledScope(_lastRequestId == null))
            {
                // 응답을 놓친 상황을 흉내 낸다. 성공 뒤에 눌러도 같은 결과가 와야 한다.
                if (GUILayout.Button("같은 번호로 다시 보내기 (응답 유실 흉내)"))
                    OpenBox(_lastSource, _lastRequestId);
            }

            // 5) 정화
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("정화", EditorStyles.boldLabel);
            _selectedRelicId = EditorGUILayout.IntField("relicId", _selectedRelicId);
            _difficultyNull = EditorGUILayout.Toggle("난이도 비워 보내기", _difficultyNull);
            using (new EditorGUI.DisabledScope(_difficultyNull))
                _difficulty = (MinigameDifficulty)EditorGUILayout.EnumPopup("난이도", _difficulty);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("상세 보기")) ShowDetail(_selectedRelicId);
                if (GUILayout.Button("정화 시작")) StartPurification();
            }
            EditorGUILayout.LabelField("마지막 토큰", _lastToken ?? "(없음)");
            using (new EditorGUI.DisabledScope(_lastToken == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("성공 보고")) Report(PlayResult.Success);
                if (GUILayout.Button("실패 보고")) Report(PlayResult.Failure);
            }

            // 6) 로그
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("로그", EditorStyles.boldLabel);
            if (GUILayout.Button("로그 지우기")) _log.Clear();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // 시계는 앞으로만 돌린다. 뒤로 돌리면 마지막 정화 시각이 미래가 되어 선명도가 어긋난다.
        private void Advance(double days)
        {
            _offsetDays += days;
            Log($"시계 +{days * 24:0.#}시간");
            if (_autoRefresh) ShowWorkbench();
        }

        private void ShowWorkbench()
        {
            Call<WorkbenchSnapshot>("작업대", (c, f) => _backend.GetWorkbench(c, f), snapshot =>
            {
                Log($"  슬롯 {snapshot.Slots.Count}/{snapshot.Capacity}{(snapshot.IsFull ? " (가득 참)" : "")} · 미획득 {snapshot.LockedCount}");
                foreach (WorkbenchSlot slot in snapshot.Slots)
                    Log($"    슬롯 #{slot.RelicId} · {slot.RevealLevel} · 다음 {slot.CurrentPhase}");
                foreach (WorkbenchSpirit spirit in snapshot.Spirits)
                    Log($"    정령 #{spirit.RelicId} · {spirit.Clarity} · 돌봄 {spirit.CareCount}회");
            });
        }

        private void ShowDetail(int relicId)
        {
            Call<RelicDetail>($"상세 #{relicId}", (c, f) => _backend.GetRelicDetail(relicId, c, f), detail =>
            {
                string clarity = detail.Clarity.HasValue ? detail.Clarity.Value.ToString() : "-";
                string resealAt = detail.ResealAt.HasValue ? $"{detail.ResealAt.Value:MM-dd HH:mm}" : "-";
                Log($"  {detail.Status} · 선명도 {clarity} · 재봉인 {resealAt} · 돌봄 {detail.CareCount}회");
                Log($"  다음: {Describe(detail.NextPlay)}");
            });
        }

        private void OpenBox(BoxSource source, string requestId)
        {
            _lastRequestId = requestId;
            _lastSource = source;
            Call<BoxOpenOutcome>($"상자 {source} {Short(requestId)}",
                (c, f) => _backend.OpenBox(source, requestId, c, f), outcome =>
                {
                    Log($"  #{outcome.RelicId} {outcome.Result} · 정성 +{outcome.JeongseongGained} · 결정 +{outcome.CrystalGained} · 정성 -{outcome.JeongseongSpent}");
                    _selectedRelicId = outcome.RelicId;
                });
            if (_autoRefresh) ShowWorkbench();
        }

        private void StartPurification()
        {
            int relicId = _selectedRelicId;
            MinigameDifficulty? difficulty = _difficultyNull ? (MinigameDifficulty?)null : _difficulty;
            Call<PurificationSession>($"정화 시작 #{relicId} {(difficulty.HasValue ? difficulty.Value.ToString() : "난이도 없음")}",
                (c, f) => _backend.StartPurification(relicId, difficulty, c, f), session =>
                {
                    _lastToken = session.SessionToken;
                    Log($"  토큰 {Short(session.SessionToken)} · {session.Route} · {session.Phase} · {session.Difficulty}");
                });
        }

        private void Report(PlayResult result)
        {
            string token = _lastToken;
            Call<SettlementResult>($"보고 {result} {Short(token)}",
                (c, f) => _backend.ReportPurification(token, result, c, f), settlement =>
                {
                    string revealed = settlement.Revealed.HasValue ? $" · 공개 {settlement.Revealed.Value}" : "";
                    string completed = settlement.CompletedNow ? " · 최초 정화 완료" : "";
                    Log($"  {settlement.Next}{revealed}{completed}");
                    if (settlement.NextPlan != null) Log($"  다음: {Describe(settlement.NextPlan)}");
                });
            if (_autoRefresh) ShowWorkbench();
        }

        private static string Describe(PlayPlan plan)
        {
            string clarity = plan.Clarity.HasValue ? $" · {plan.Clarity.Value}" : "";
            return $"{plan.Route}{clarity} · 남은 [{string.Join(", ", plan.RemainingPhases)}] · 난이도 [{string.Join(", ", plan.AllowedDifficulties)}]";
        }

        private static string Short(string id) => string.IsNullOrEmpty(id) ? "(없음)" : id.Substring(0, Math.Min(8, id.Length));

        /// <summary>코루틴을 끝까지 돌리고 성공·실패를 로그에 남긴다. 지연 0이라 그 자리에서 끝난다.</summary>
        private void Call<T>(string label, Func<Action<T>, Action<string>, IEnumerator> call, Action<T> onSuccess)
        {
            try
            {
                Drain(call(value =>
                {
                    Log($"[{label}] 성공");
                    onSuccess(value);
                }, error => Log($"[{label}] 실패: {error}")));
            }
            catch (Exception e)
            {
                // 모의 백엔드가 failed 대신 예외를 던지면 버그다. 콘솔에도 남긴다.
                Log($"[{label}] 예외: {e.GetType().Name}: {e.Message}");
                Debug.LogException(e);
            }
            Repaint();
        }

        private static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator inner) Drain(inner);
            }
        }

        private void Log(string line) => _log.AppendLine($"{Now:MM-dd HH:mm} {line}");
    }
}
