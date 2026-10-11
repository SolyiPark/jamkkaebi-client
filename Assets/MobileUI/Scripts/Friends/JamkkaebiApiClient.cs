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
        public JamkkaebiApiClient(string baseUrl, string accessToken = null, string refreshToken = null)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("서버 기본 주소가 올바르지 않습니다.");
            BaseUrl = baseUrl.TrimEnd('/'); SetTokens(accessToken, refreshToken);
        }
        public void SetTokens(string accessToken, string refreshToken)
        { _accessToken = accessToken; _refreshToken = refreshToken; }
        public static string CodePath(string code)
        {
            if (!FriendsPreviewState.IsValidCode(code)) throw new ArgumentException("친구 코드 형식이 올바르지 않습니다.");
            return Uri.EscapeDataString(FriendsPreviewState.NormalizeCode(code));
        }
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
        public void ApplyTokens(JToken data)
        {
            string access = (string)data?["accessToken"], refresh = (string)data?["refreshToken"];
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) throw new FormatException("로그인 응답에 토큰이 없습니다.");
            SetTokens(access, refresh);
        }
        private ServerApiResult ApplyTokenResponse(ServerApiResult result)
        {
            try { ApplyTokens(result.Data); return result; }
            catch (FormatException) { SetTokens(null, null); return new ServerApiResult(false, "INVALID_RESPONSE", "로그인 응답을 읽을 수 없어요.", status: result.Status); }
        }
        public static string CreateVerifier()
        {
            var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        public IEnumerator StartLogin(string provider, string verifier, Action<ServerApiResult> completed)
        {
            if (provider != "GOOGLE" && provider != "KAKAO") throw new ArgumentException("지원하지 않는 로그인 제공자입니다.");
            string hash; using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(verifier)));
            return Send("POST", "/api/auth/login/start", new JObject { ["provider"] = provider, ["verifierHash"] = hash }, completed, false);
        }
        public IEnumerator Exchange(string handoff, string verifier, Action<ServerApiResult> completed)
        {
            return Send("POST", "/api/auth/exchange", new JObject { ["handoff"] = handoff, ["verifier"] = verifier, ["deviceLabel"] = "Unity client" }, result =>
            { completed(result.Success ? ApplyTokenResponse(result) : result); }, false);
        }
        public IEnumerator Logout(Action<ServerApiResult> completed) => Send("POST", "/api/auth/logout", null, result =>
        { if (result.Success) SetTokens(null, null); completed(result); });
    }
}
