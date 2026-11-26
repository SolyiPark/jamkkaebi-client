using System;
using System.Collections;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.UI.Common;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 작업대 백엔드의 HTTP 구현. 요청·응답 변환은 <see cref="WorkbenchServerMapper"/>가 맡는다.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>변경 요청(상자 열기·정화 시작·결과 보고·이름 변경)은 한 번만 보낸다. 네트워크 실패를 이유로 스스로 다시 보내지 않고
    /// 실패 콜백으로 알린다. 다시 보내기는 사용자가 고른다(상자는 같은 requestId, 보고는 같은 토큰·같은 결과라 서버가 한 번만 처리한다).</item>
    /// <item>서버 실패를 모의 백엔드로 바꿔 처리하지 않는다.</item>
    /// <item>HTTP 코루틴은 <c>host</c>(셸의 SharedRecords)에서 돈다. 작업대는 숨기면 꺼지는 탭이라 거기서 돌리면
    /// 공유 클라이언트의 토큰 재발급이 중간에 끊길 수 있다. 호출한 쪽 코루틴은 끝날 때까지 기다리기만 하고,
    /// 늦은 응답을 버릴지는 호출한 쪽(작업대 화면의 수명 검사)이 정한다.</item>
    /// </list>
    /// </remarks>
    public sealed class ServerWorkbenchBackend : IWorkbenchBackend
    {
        private readonly JamkkaebiApiClient _client;
        private readonly MonoBehaviour _host;
        private readonly Action<string> _log;

        // 결과 응답에 없는 진입 경로·난이도를 채우려고 시작한 판을 기억한다.
        // 서버는 새 판을 시작하면 보고하지 않은 이전 판을 대체하므로 마지막 판 하나만 둔다.
        private PurificationSession _current;

        /// <param name="client">인증 API 클라이언트. 셸이 하나만 만들어 친구 탭과 함께 쓴다.</param>
        /// <param name="host">HTTP 코루틴을 돌릴 오브젝트. null이면 호출한 코루틴 안에서 바로 돌린다(테스트).</param>
        public ServerWorkbenchBackend(JamkkaebiApiClient client, MonoBehaviour host = null)
            : this(client, host, Debug.LogWarning)
        {
        }

        /// <summary>검증용. 경고 기록을 바꿔 끼운다(Unity 밖에서 돌리거나 기록 내용을 확인할 때).</summary>
        internal ServerWorkbenchBackend(JamkkaebiApiClient client, MonoBehaviour host, Action<string> log)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _host = host;
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IEnumerator GetWorkbench(Action<WorkbenchSnapshot> completed, Action<string> failed)
        {
            return Call("GET", "/api/workbench", null, WorkbenchServerMapper.Snapshot, completed, failed);
        }

        public IEnumerator GetRelicDetail(int relicId, Action<RelicDetail> completed, Action<string> failed)
        {
            return Call("GET", RelicPath(relicId), null, WorkbenchServerMapper.Detail, completed, failed);
        }

        public IEnumerator OpenBox(BoxSource source, string requestId, Action<BoxOpenOutcome> completed,
            Action<string> failed)
        {
            return Call("POST", "/api/box/open", WorkbenchServerMapper.BoxOpenBody(source, requestId),
                WorkbenchServerMapper.BoxOutcome, completed, failed);
        }

        public IEnumerator StartPurification(int relicId, MinigameDifficulty? difficulty,
            Action<PurificationSession> completed, Action<string> failed)
        {
            return Call("POST", "/api/purifications", WorkbenchServerMapper.StartBody(relicId, difficulty),
                data => WorkbenchServerMapper.Session(data, relicId),
                session =>
                {
                    _current = session;
                    completed(session);
                }, failed);
        }

        public IEnumerator ReportPurification(string sessionToken, PlayReport report,
            Action<SettlementResult> completed, Action<string> failed)
        {
            if (string.IsNullOrEmpty(sessionToken)) throw new ArgumentException("sessionToken이 비어 있습니다.", nameof(sessionToken));
            if (report == null) throw new ArgumentNullException(nameof(report));
            return Report(sessionToken, report, completed, failed);
        }

        public IEnumerator RenameSpirit(int relicId, string name, Action<string> completed, Action<string> failed)
        {
            // 길이 규칙은 보내기 전에 거른다(금칙어는 서버만 검사한다).
            if (!SpiritNameRule.TryNormalize(name, out string normalized, out string error))
                return Fail(error, failed);
            return Call("PATCH", RelicPath(relicId) + "/spirit-name", WorkbenchServerMapper.RenameBody(normalized),
                WorkbenchServerMapper.SpiritName, completed, failed);
        }

        private IEnumerator Report(string sessionToken, PlayReport report, Action<SettlementResult> completed,
            Action<string> failed)
        {
            PurificationSession session = _current;
            if (session == null || session.SessionToken != sessionToken)
            {
                failed("시작하지 않았거나 이미 대체된 정화 판이에요.");
                yield break;
            }

            yield return Call("POST", "/api/purifications/" + Uri.EscapeDataString(sessionToken) + "/result",
                WorkbenchServerMapper.ReportBody(report),
                data => WorkbenchServerMapper.Settlement(data, session),
                settlement =>
                {
                    if (_current == session) _current = null;
                    completed(settlement);
                }, failed);
        }

        private static string RelicPath(int relicId)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            return "/api/relics/" + relicId;
        }

        private static IEnumerator Fail(string error, Action<string> failed)
        {
            failed(error);
            yield break;
        }

        private IEnumerator Call<T>(string method, string path, JObject body, Func<JToken, T> map,
            Action<T> completed, Action<string> failed)
        {
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (failed == null) throw new ArgumentNullException(nameof(failed));

            ServerApiResult result = null;
            if (_host == null)
            {
                yield return _client.Send(method, path, body, value => result = value);
            }
            else
            {
                bool done = false;
                _host.StartCoroutine(_client.Send(method, path, body, value =>
                {
                    result = value;
                    done = true;
                }));
                while (!done) yield return null;
            }

            // 결과 보고 경로의 세션 토큰은 기록하지 않는다.
            string logPath = path.StartsWith("/api/purifications/", StringComparison.Ordinal)
                ? "/api/purifications/{sessionToken}/result" : path;
            if (!result.Success)
            {
                // 코드만 남긴다. 토큰이나 요청 본문은 기록하지 않는다.
                _log($"[Workbench] {method} {logPath} 실패: {result.Code} (HTTP {result.Status})");
                failed(string.IsNullOrEmpty(result.Message) ? result.Code : result.Message);
                yield break;
            }

            T value;
            try
            {
                value = map(result.Data);
            }
            catch (FormatException error)
            {
                _log($"[Workbench] {method} {logPath} 응답 형식 오류: {error.Message}");
                failed("서버 응답을 읽을 수 없어요.");
                yield break;
            }
            completed(value);
        }
    }
}
