using System;
using System.Collections;
using Jamkkaebi.Scripts.UI.Common;
using Newtonsoft.Json.Linq;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>객체마다 요청하지 않고 캡처한 편집 결과를 한 패키지로 저장한다.</summary>
    public sealed class ExhibitionLayoutWrapper
    {
        private readonly JamkkaebiApiClient _client;
        private JObject _lastPackage;
        public bool Busy { get; private set; }
        public JObject LastPackage => _lastPackage == null ? null : (JObject)_lastPackage.DeepClone();
        /// <summary>보유 목록 조회와 배치 저장에 사용할 인증 API 클라이언트를 지정합니다.</summary>
        public ExhibitionLayoutWrapper(JamkkaebiApiClient client)
        { _client = client ?? throw new ArgumentNullException(nameof(client)); }

        /// <summary>보유 목록을 조회해 관리 대상과 기존 배치를 병합한 뒤 하나의 PUT으로 저장합니다.</summary>
        public IEnumerator Save(ExhibitionLayoutDraft draft, Action<ServerApiResult> completed) =>
            Save(_ => draft, completed);

        /// <summary>
        /// 보유 목록을 조회한 뒤에 초안을 만들어 저장합니다. 조회 응답(전시관 data)을 보고 홈을 먼저 고친 다음 캡처해야 할 때 씁니다.
        /// 예: 다른 기기에서 건물이 옮겨져 도깨비와 겹치게 됐으면, 도깨비를 옮긴 뒤의 배치를 보내야 거부되지 않습니다.
        /// </summary>
        /// <param name="createDraft">
        /// 조회에 성공했을 때 한 번 불립니다. FormatException·InvalidOperationException·ArgumentException을 던지면
        /// 저장하지 않고 LAYOUT_REJECTED로 끝납니다.
        /// </param>
        /// <exception cref="ArgumentNullException">createDraft나 completed가 null인 경우.</exception>
        public IEnumerator Save(Func<JToken, ExhibitionLayoutDraft> createDraft, Action<ServerApiResult> completed)
        {
            if (createDraft == null) throw new ArgumentNullException(nameof(createDraft));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (Busy) { completed(new ServerApiResult(false, "SAVE_BUSY", "배치를 저장 중입니다.")); yield break; }
            Busy = true;
            ServerApiResult result = null;
            try
            {
                _lastPackage = null;
                yield return _client.Send("GET", "/api/exhibition", null, value => result = value);
                if (result.Success)
                {
                    try { _lastPackage = ExhibitionLayoutPackage.Build(createDraft(result.Data), result.Data); }
                    catch (Exception error) when (error is InvalidOperationException || error is FormatException || error is ArgumentException)
                    { result = new ServerApiResult(false, "LAYOUT_REJECTED", error.Message); }
                    if (result.Success)
                        yield return _client.Send("PUT", "/api/exhibition/layout", _lastPackage, value => result = value);
                }
            }
            finally { Busy = false; }
            completed(result);
        }
    }
}
