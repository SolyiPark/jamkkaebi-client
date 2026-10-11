using System;
using System.Collections;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace MobilePrototype.Friends
{
    /// <summary>서버의 ApiResponse·Bearer 인증·회전 토큰 계약. 토큰을 자산이나 PlayerPrefs에 저장하지 않습니다.</summary>
    public sealed class JamkkaebiApiClient
    {
        private string _accessToken;
        private string _refreshToken;
        private bool _refreshing;
        private ServerApiResult _refreshResult;
        public string BaseUrl { get; }
        public bool Authenticated => !string.IsNullOrEmpty(_accessToken);
        /// <summary>Creates a client with an HTTP(S) base URL and tokens held in memory.</summary>
        /// <exception cref="ArgumentException">The URL is not absolute HTTP(S), or contains user information, a query, or a fragment.</exception>
        public JamkkaebiApiClient(string baseUrl, string accessToken = null, string refreshToken = null)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("서버 기본 주소가 올바르지 않습니다.");
            BaseUrl = baseUrl.TrimEnd('/'); SetTokens(accessToken, refreshToken);
        }
        /// <summary>Replaces the in-memory tokens; null values clear them without contacting the server.</summary>
        public void SetTokens(string accessToken, string refreshToken)
        { _accessToken = accessToken; _refreshToken = refreshToken; }
        /// <summary>Returns a normalized, escaped friend code suitable for an API path segment.</summary>
        /// <exception cref="ArgumentException">The normalized code is not eight characters from the server alphabet.</exception>
        public static string CodePath(string code)
        {
            if (!FriendsPreviewState.IsValidCode(code)) throw new ArgumentException("친구 코드 형식이 올바르지 않습니다.");
            return Uri.EscapeDataString(FriendsPreviewState.NormalizeCode(code));
        }
        /// <summary>Sends an API request; authenticated A004 responses share a refresh when a refresh token is available, then retry once on refresh success.</summary>
        /// <param name="path">Path beginning with /api/, optionally including a query string.</param>
        /// <param name="authenticated">Whether to require and send an access token and handle authentication failures.</param>
        /// <param name="completed">Receives server results or local AUTH_REQUIRED, NETWORK_ERROR, INVALID_RESPONSE, or refresh failure results.</param>
        /// <returns>A coroutine that waits for the request and any shared refresh; A003, A004, and A005 final results clear tokens for authenticated requests.</returns>
        /// <exception cref="ArgumentException">The path lacks the /api/ prefix or contains :// when the coroutine advances.</exception>
        /// <remarks>Request setup and callback exceptions propagate. Response parsing failures are converted to INVALID_RESPONSE.</remarks>
        public IEnumerator Send(string method, string path, JObject body, Action<ServerApiResult> completed, bool authenticated = true)
        {
            if (!path.StartsWith("/api/", StringComparison.Ordinal) || path.Contains("://")) throw new ArgumentException("API 경로가 올바르지 않습니다.");
            if (authenticated && !Authenticated)
            { completed(new ServerApiResult(false, "AUTH_REQUIRED", "로그인이 필요해요.")); yield break; }
            ServerApiResult result = null;
            yield return SendOnce(method, path, body, authenticated, value => result = value);
            if (authenticated && result.Code == "A004" && !string.IsNullOrEmpty(_refreshToken))
            {
                if (_refreshing) { while (_refreshing) yield return null; }
                else
                {
                    _refreshing = true;
                    // 소유 코루틴이 취소되어도 대기 요청이 null 또는 이전 성공 응답을 사용하지 않는다.
                    _refreshResult = new ServerApiResult(false, "AUTH_REFRESH_INTERRUPTED", "인증 갱신이 중단됐어요. 다시 로그인해 주세요.");
                    try
                    {
                        yield return SendOnce("POST", "/api/auth/reissue", new JObject { ["refreshToken"] = _refreshToken, ["deviceLabel"] = "Unity client" }, false, value => _refreshResult = value);
                        if (_refreshResult.Success) _refreshResult = ApplyTokenResponse(_refreshResult);
                        else if (_refreshResult.Code == "A005") SetTokens(null, null);
                    }
                    finally { _refreshing = false; }
                }
                if (_refreshResult.Success) yield return SendOnce(method, path, body, true, value => result = value);
                else result = _refreshResult;
            }
            if (authenticated && (result.Code == "A003" || result.Code == "A004" || result.Code == "A005")) SetTokens(null, null);
            completed(result);
        }
        /// <summary>Sends one JSON request with a 15-second timeout; connection and response-parsing failures are delivered to the callback as failure results.</summary>
        private IEnumerator SendOnce(string method, string path, JObject body, bool authenticated, Action<ServerApiResult> completed)
        {
            using (var request = new UnityWebRequest(BaseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 15;
                request.SetRequestHeader("Accept", "application/json");
                if (authenticated) request.SetRequestHeader("Authorization", "Bearer " + _accessToken);
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                var operation = request.SendWebRequest();
                while (!operation.isDone) yield return null;
                ServerApiResult result;
                if (request.result == UnityWebRequest.Result.ConnectionError)
                    result = new ServerApiResult(false, "NETWORK_ERROR", "서버에 연결할 수 없어요.", status: request.responseCode);
                else
                {
                    try { result = ServerApiResult.Parse(request.downloadHandler.text, request.responseCode); }
                    catch (Exception error) { result = new ServerApiResult(false, "INVALID_RESPONSE", "서버 응답을 읽을 수 없어요. (" + error.GetType().Name + ")", status: request.responseCode); }
                }
                completed(result);
            }
        }
        /// <summary>Replaces both tokens only after reading nonempty access and refresh tokens from the response data.</summary>
        /// <exception cref="FormatException">Either token is absent or empty.</exception>
        /// <remarks>JSON access or token conversion errors propagate.</remarks>
        public void ApplyTokens(JToken data)
        {
            string access = (string)data?["accessToken"], refresh = (string)data?["refreshToken"];
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) throw new FormatException("로그인 응답에 토큰이 없습니다.");
            SetTokens(access, refresh);
        }
        /// <summary>Applies response tokens, clearing both tokens and returning INVALID_RESPONSE if token application throws FormatException.</summary>
        private ServerApiResult ApplyTokenResponse(ServerApiResult result)
        {
            try { ApplyTokens(result.Data); return result; }
            catch (FormatException) { SetTokens(null, null); return new ServerApiResult(false, "INVALID_RESPONSE", "로그인 응답을 읽을 수 없어요.", status: result.Status); }
        }
        /// <summary>Returns 32 cryptographically random bytes encoded as a standard Base64 login verifier.</summary>
        public static string CreateVerifier()
        {
            var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        /// <summary>Starts login without authentication using the Base64 SHA-256 hash of the UTF-8 verifier.</summary>
        /// <param name="verifier">Original verifier to retain for the later handoff exchange.</param>
        /// <returns>The login-start request coroutine; the callback receives the response containing the authorization URL on success.</returns>
        /// <exception cref="ArgumentException"><paramref name="provider"/> is neither GOOGLE nor KAKAO.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="verifier"/> is null.</exception>
        public IEnumerator StartLogin(string provider, string verifier, Action<ServerApiResult> completed)
        {
            if (provider != "GOOGLE" && provider != "KAKAO") throw new ArgumentException("지원하지 않는 로그인 제공자입니다.");
            string hash; using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(verifier)));
            return Send("POST", "/api/auth/login/start", new JObject { ["provider"] = provider, ["verifierHash"] = hash }, completed, false);
        }
        /// <summary>Exchanges a handoff and its original verifier without authentication, applying returned tokens before reporting success.</summary>
        public IEnumerator Exchange(string handoff, string verifier, Action<ServerApiResult> completed)
        {
            return Send("POST", "/api/auth/exchange", new JObject { ["handoff"] = handoff, ["verifier"] = verifier, ["deviceLabel"] = "Unity client" }, result =>
            { completed(result.Success ? ApplyTokenResponse(result) : result); }, false);
        }
        /// <summary>Requests logout and clears local tokens on success; authentication failures follow Send token handling.</summary>
        public IEnumerator Logout(Action<ServerApiResult> completed) => Send("POST", "/api/auth/logout", null, result =>
        { if (result.Success) SetTokens(null, null); completed(result); });
    }
}
