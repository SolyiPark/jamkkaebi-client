using System;
using System.Collections;
using MobilePrototype.Friends;
using Newtonsoft.Json.Linq;

namespace MobilePrototype.Exhibition
{
    /// <summary>객체마다 요청하지 않고 캡처한 편집 결과를 한 패키지로 저장한다.</summary>
    public sealed class ExhibitionLayoutWrapper
    {
        private readonly JamkkaebiApiClient _client;
        private JObject _lastPackage;
        public bool Busy { get; private set; }
        public JObject LastPackage => _lastPackage == null ? null : (JObject)_lastPackage.DeepClone();
        /// <summary>Uses the supplied API client for layout reads and saves.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
        public ExhibitionLayoutWrapper(JamkkaebiApiClient client)
        { _client = client ?? throw new ArgumentNullException(nameof(client)); }

        /// <summary>Reads the server exhibition, validates a replacement package, and submits it only if validation succeeds.</summary>
        /// <param name="completed">Receives the API result, SAVE_BUSY for an overlapping save, or LAYOUT_REJECTED for caught argument, format, or invalid-operation errors.</param>
        /// <returns>A coroutine that keeps Busy set during the save and exposes the built payload through LastPackage, even if the PUT fails.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="completed"/> is null when the coroutine advances.</exception>
        /// <remarks>Other build errors, including integer overflow, propagate; Busy is cleared when the save exits.</remarks>
        public IEnumerator Save(ExhibitionLayoutDraft draft, Action<ServerApiResult> completed)
        {
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
                    try { _lastPackage = ExhibitionLayoutPackage.Build(draft, result.Data); }
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
