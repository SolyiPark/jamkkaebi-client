using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Jamkkaebi.Scripts.UI.Common;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 홈의 도깨비 배치를 서버와 맞춘다. 홈을 열 때 서버 배치를 불러오고, 그 뒤로는 3분에 한 번
    /// 인터넷 연결이 확인되고 배치가 달라졌을 때 전체 배치를 보낸다. 서버 주소가 있을 때만 하우징이 붙인다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>API 클라이언트는 셸의 것을 받아 쓴다. 따로 만들지 않는다.</item>
    /// <item>실패해도 모의 데이터로 바꾸지 않고, 그 자리에서 다시 보내지도 않는다. 다음 차례에 그때의 배치로 다시 판단한다.</item>
    /// <item>서버 배치를 불러오기 전에는 보내지 않는다.</item>
    /// <item>홈이 그리지 않는 서버의 건물 배치는 그대로 보존한다.</item>
    /// <item>꺼졌다 켜지면 앞선 요청의 늦은 응답을 버린다.</item>
    /// </list>
    /// </remarks>
    public sealed class ExhibitionServerSync : MonoBehaviour
    {
        private static readonly IReadOnlyDictionary<string, int> NoBuildingBindings = new Dictionary<string, int>();

        private ExhibitionHousing _housing;
        private JamkkaebiApiClient _client;
        private ExhibitionLayoutWrapper _wrapper;
        private ExhibitionSyncSchedule _schedule;
        private Func<bool> _reachable;
        private readonly HashSet<int> _ownedRelicIds = new HashSet<int>();
        private int _generation;
        private bool _busy;

        /// <summary>서버 배치를 불러와 홈에 반영했는지.</summary>
        public bool Loaded => _schedule != null && _schedule.Loaded;

        /// <summary>서버와 마지막으로 맞춘 배치의 요약. 맞춘 적이 없으면 null.</summary>
        public string SyncedSignature => _schedule?.SyncedSignature;

        /// <summary>마지막 불러오기·보내기가 실패했으면 그 사유. 성공했으면 null.</summary>
        public string LastError { get; private set; }

        /// <param name="reachable">인터넷 연결 확인. 생략하면 기기의 네트워크 상태를 본다.</param>
        /// <param name="intervalSeconds">판단 간격. 게임에서는 기본값(3분)을 쓰고, 테스트만 줄인다.</param>
        /// <exception cref="ArgumentNullException">housing이나 client가 null인 경우.</exception>
        public void Configure(ExhibitionHousing housing, JamkkaebiApiClient client, Func<bool> reachable = null,
            float intervalSeconds = ExhibitionSyncSchedule.DefaultIntervalSeconds)
        {
            _housing = housing ? housing : throw new ArgumentNullException(nameof(housing));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _reachable = reachable ?? (() => Application.internetReachability != NetworkReachability.NotReachable);
            _wrapper = new ExhibitionLayoutWrapper(_client);
            _schedule = new ExhibitionSyncSchedule(Time.unscaledTime, intervalSeconds);
        }

        /// <summary>놓인 도깨비의 정령과 기준 자리를 정령 순으로 적은 요약. 배치가 같으면 같은 글이 나온다.</summary>
        public static string SignatureOf(IEnumerable<DokkaebiWanderer> wanderers)
        {
            if (wanderers == null) throw new ArgumentNullException(nameof(wanderers));
            var rows = new List<(int relicId, Vector2Int cell)>();
            foreach (var wanderer in wanderers)
                if (wanderer) rows.Add((wanderer.RelicId, wanderer.PlacementCell));
            return Signature(rows);
        }

        /// <summary>
        /// 서버가 알려 준 도깨비 자리(relicId → 칸)의 요약. 홈의 배치 요약과 같은 형식이라 그대로 비교한다.
        /// 서버와 맞춘 기준은 홈에 세운 결과가 아니라 이 값으로 잡는다. 자리를 보정했거나 세우지 못한 도깨비가 있으면
        /// 홈과 서버가 다르고, 그 차이를 다음 차례에 보내야 하기 때문이다.
        /// </summary>
        /// <exception cref="ArgumentNullException">cells가 null인 경우.</exception>
        public static string SignatureOf(IReadOnlyDictionary<int, Vector2Int> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            var rows = new List<(int relicId, Vector2Int cell)>();
            foreach (var pair in cells) rows.Add((pair.Key, pair.Value));
            return Signature(rows);
        }

        private static string Signature(List<(int relicId, Vector2Int cell)> rows)
        {
            rows.Sort((a, b) => a.relicId.CompareTo(b.relicId));
            var text = new StringBuilder();
            foreach (var row in rows) text.Append(row.relicId).Append('@').Append(row.cell.x).Append(',').Append(row.cell.y).Append(';');
            return text.ToString();
        }

        private void Update()
        {
            if (_schedule == null || _busy || !_housing || _housing.Layout == null) return;
            // 차례가 아닐 때는 배치 요약을 만들지 않는다(매 프레임 불린다).
            if (!_schedule.IsDue(Time.unscaledTime)) return;
            switch (_schedule.Tick(Time.unscaledTime, _reachable(), SignatureOf(_housing.Wanderers)))
            {
                case ExhibitionSyncSchedule.Step.Load: StartCoroutine(Load()); break;
                case ExhibitionSyncSchedule.Step.Send: StartCoroutine(Send()); break;
            }
        }

        /// <summary>서버 배치를 불러와 홈에 세운다. Update가 차례에 맞춰 돌린다(테스트는 직접 돌린다).</summary>
        internal IEnumerator Load()
        {
            _busy = true;
            int generation = _generation;
            ServerApiResult result = null;
            yield return _client.Send("GET", "/api/exhibition", null, value => result = value);
            if (generation != _generation) yield break;
            _busy = false;
            if (result == null || !result.Success) { Fail("전시관 배치를 불러오지 못했습니다.", result); yield break; }
            if (!TryParse(result, out var snapshot)) yield break;

            Apply(snapshot);
            LastError = null;
            // 기준은 서버의 좌표다. 홈에 세우며 보정한 자리는 서버와 다르므로 다음 차례에 보내진다.
            _schedule.MarkLoaded(SignatureOf(snapshot.SpiritCells));
        }

        /// <summary>지금 배치를 서버에 저장한다. Update가 차례에 맞춰 돌린다(테스트는 직접 돌린다).</summary>
        internal IEnumerator Send()
        {
            _busy = true;
            int generation = _generation;
            string signature = null;
            ServerApiResult result = null;
            yield return _wrapper.Save(exhibition =>
            {
                // 저장용 조회가 알려 준 최신 건물 자리를 먼저 홈에 반영한다. 다른 기기에서 건물이 옮겨져 도깨비와 겹치게 됐으면
                // 도깨비를 빈칸으로 옮긴 뒤의 배치를 보내야 한다. 옛 건물 자리로 묶으면 거부되고, 배치가 그대로면 매 차례 되풀이된다.
                Remember(ExhibitionServerSnapshot.Parse(exhibition, _housing.Surface.Grid.Size));
                signature = SignatureOf(_housing.Wanderers);
                // 서버가 아는 정령 가운데 놓여 있지 않은 것은 회수한 것으로 보낸다.
                return ExhibitionLayoutCapture.Capture(_housing, NoBuildingBindings, _ownedRelicIds);
            }, value => result = value);
            if (generation != _generation) yield break;
            _busy = false;
            if (result == null || !result.Success) { Fail("전시관 배치를 저장하지 못했습니다.", result); yield break; }

            LastError = null;
            // 저장 응답은 저장 뒤의 전시관이다. 읽히면 서버가 실제로 가진 자리를 기준으로 삼고, 읽지 못하면 보낸 배치를 기준으로 둔다.
            // 보유 목록과 건물 자리도 새로 읽는다. 걷고 있는 도깨비의 자리는 건드리지 않는다.
            if (TryParse(result, out var snapshot, report: false))
            {
                _schedule.MarkSent(SignatureOf(snapshot.SpiritCells));
                Remember(snapshot);
            }
            else _schedule.MarkSent(signature);
        }

        /// <param name="report">false면 읽지 못해도 실패로 기록하지 않는다. 저장은 이미 성공했고 응답만 못 읽은 경우에 쓴다.</param>
        private bool TryParse(ServerApiResult result, out ExhibitionServerSnapshot snapshot, bool report = true)
        {
            snapshot = null;
            try
            {
                snapshot = ExhibitionServerSnapshot.Parse(result.Data, _housing.Surface.Grid.Size);
                return true;
            }
            catch (Exception error) when (error is FormatException || error is InvalidOperationException)
            {
                if (!report) return false;
                LastError = error.Message;
                Debug.LogWarning($"전시관 응답을 읽지 못했습니다. {error.Message}", this);
                return false;
            }
        }

        private void Remember(ExhibitionServerSnapshot snapshot)
        {
            _ownedRelicIds.Clear();
            foreach (var spirit in snapshot.Spirits) _ownedRelicIds.Add(spirit.ServerId);
            _housing.Layout.SetBlockedCells(snapshot.BuildingCells);
            // 조회가 끝나기 전에 놓은 도깨비가 건물 자리에 서 있으면 그 칸에서 걸어 나오지 못한다. 빈칸으로 옮긴다.
            _housing.MoveSpiritsOffBlockedCells();
        }

        private void Apply(ExhibitionServerSnapshot snapshot)
        {
            Remember(snapshot);
            foreach (var spirit in snapshot.Spirits)
            {
                if (!snapshot.SpiritCells.TryGetValue(spirit.ServerId, out var cell) || _housing.IsSpiritPlaced(spirit.ServerId)) continue;
                // 그 칸을 쓸 수 없으면(이미 놓은 도깨비, 막힌 칸) 가까운 칸에 세운다. 세우지 못하면 다음 송신에서 회수로 나간다.
                if (!_housing.TryAddSpiritNear(spirit, cell))
                    Debug.LogWarning($"도깨비 {spirit.ServerId}을(를) 세울 빈칸이 없습니다.", this);
            }
        }

        private void Fail(string summary, ServerApiResult result)
        {
            LastError = result == null ? summary : $"{summary} {result.Code} {result.Message}";
            Debug.LogWarning(LastError, this);
        }

        private void OnDisable()
        {
            // 오브젝트가 꺼지면 코루틴이 멈추지만 이 컴포넌트만 끄면 계속 돈다. 조회를 기다리던 저장이 꺼진 뒤에
            // 서버 저장까지 나가지 않도록 직접 멈춘다. 늦은 응답은 세대 검사로 버리고, 멈춘 저장이 남긴 진행 중 표시도 새로 만든다.
            StopAllCoroutines();
            _generation++;
            _busy = false;
            if (_client != null) _wrapper = new ExhibitionLayoutWrapper(_client);
        }
    }
}
