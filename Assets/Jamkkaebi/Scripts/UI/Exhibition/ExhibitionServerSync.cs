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

        /// <summary>마지막 불러오기·보내기가 실패했으면 그 사유. 성공했으면 null.</summary>
        public string LastError { get; private set; }

        /// <param name="reachable">인터넷 연결 확인. 생략하면 기기의 네트워크 상태를 본다.</param>
        /// <exception cref="ArgumentNullException">housing이나 client가 null인 경우.</exception>
        public void Configure(ExhibitionHousing housing, JamkkaebiApiClient client, Func<bool> reachable = null)
        {
            _housing = housing ? housing : throw new ArgumentNullException(nameof(housing));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _reachable = reachable ?? (() => Application.internetReachability != NetworkReachability.NotReachable);
            _wrapper = new ExhibitionLayoutWrapper(_client);
            _schedule = new ExhibitionSyncSchedule(Time.unscaledTime);
        }

        /// <summary>놓인 도깨비의 정령과 기준 자리를 정령 순으로 적은 요약. 배치가 같으면 같은 글이 나온다.</summary>
        public static string SignatureOf(IEnumerable<DokkaebiWanderer> wanderers)
        {
            if (wanderers == null) throw new ArgumentNullException(nameof(wanderers));
            var rows = new List<(int relicId, Vector2Int cell)>();
            foreach (var wanderer in wanderers)
                if (wanderer) rows.Add((wanderer.RelicId, wanderer.PlacementCell));
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

        private IEnumerator Load()
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
            _schedule.MarkLoaded(SignatureOf(_housing.Wanderers));
        }

        private IEnumerator Send()
        {
            _busy = true;
            int generation = _generation;
            string signature = SignatureOf(_housing.Wanderers);
            // 서버가 아는 정령 가운데 놓여 있지 않은 것은 회수한 것으로 보낸다.
            var draft = ExhibitionLayoutCapture.Capture(_housing, NoBuildingBindings, _ownedRelicIds);
            ServerApiResult result = null;
            yield return _wrapper.Save(draft, value => result = value);
            if (generation != _generation) yield break;
            _busy = false;
            if (result == null || !result.Success) { Fail("전시관 배치를 저장하지 못했습니다.", result); yield break; }

            LastError = null;
            _schedule.MarkSent(signature);
            // 저장 응답은 저장 뒤의 전시관이다. 보유 목록과 건물 자리만 새로 읽고, 걷고 있는 도깨비의 자리는 건드리지 않는다.
            if (TryParse(result, out var snapshot)) Remember(snapshot);
        }

        private bool TryParse(ServerApiResult result, out ExhibitionServerSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                snapshot = ExhibitionServerSnapshot.Parse(result.Data, _housing.Surface.Grid.Size);
                return true;
            }
            catch (Exception error) when (error is FormatException || error is InvalidOperationException)
            {
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
            // 비활성화되면 코루틴이 멈춘다. 끝나지 못한 요청의 늦은 응답을 버리고, 멈춘 저장이 남긴 진행 중 표시도 새로 만든다.
            _generation++;
            _busy = false;
            if (_client != null) _wrapper = new ExhibitionLayoutWrapper(_client);
        }
    }
}
