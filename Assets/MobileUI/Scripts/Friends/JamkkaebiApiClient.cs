using System;
using System.Collections;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Jamkkaebi.Scripts.UI.Friends
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
        /// <summary>HTTP 기본 주소를 검증하고 메모리 토큰으로 API 클라이언트를 초기화합니다.</summary>
        public JamkkaebiApiClient(string baseUrl, string accessToken = null, string refreshToken = null)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("서버 기본 주소가 올바르지 않습니다.");
            BaseUrl = baseUrl.TrimEnd('/'); SetTokens(accessToken, refreshToken);
        }
        /// <summary>인증 토큰을 메모리에만 교체하며 null을 전달하면 해당 토큰을 제거합니다.</summary>
        public void SetTokens(string accessToken, string refreshToken)
        { _accessToken = accessToken; _refreshToken = refreshToken; }
        /// <summary>유효한 친구 코드만 정규화하고 API 경로에 넣을 수 있도록 인코딩합니다.</summary>
        public static string CodePath(string code)
        {
            if (!FriendsPreviewState.IsValidCode(code)) throw new ArgumentException("친구 코드 형식이 올바르지 않습니다.");
            return Uri.EscapeDataString(FriendsPreviewState.NormalizeCode(code));
        }
        /// <summary>인증 HTTP 요청을 보내고 만료 시 공유 갱신을 거쳐 한 번 재요청한 결과를 전달합니다.</summary>
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
        /// <summary>UnityWebRequest로 한 번 요청하고 네트워크 오류와 서버 응답 오류를 구분합니다.</summary>
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
        /// <summary>로그인 응답에 두 토큰이 모두 있을 때만 메모리 인증 상태를 교체합니다.</summary>
        public void ApplyTokens(JToken data)
        {
            string access = (string)data?["accessToken"], refresh = (string)data?["refreshToken"];
            if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh)) throw new FormatException("로그인 응답에 토큰이 없습니다.");
            SetTokens(access, refresh);
        }
        /// <summary>토큰 응답을 검증하고 형식 오류가 있으면 인증 상태를 제거한 실패 결과를 반환합니다.</summary>
        private ServerApiResult ApplyTokenResponse(ServerApiResult result)
        {
            try { ApplyTokens(result.Data); return result; }
            catch (FormatException) { SetTokens(null, null); return new ServerApiResult(false, "INVALID_RESPONSE", "로그인 응답을 읽을 수 없어요.", status: result.Status); }
        }
        /// <summary>로그인 handoff 검증에 사용할 암호학적 난수 verifier를 생성합니다.</summary>
        public static string CreateVerifier()
        {
            var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
        /// <summary>지원 제공자와 verifier의 SHA-256 해시로 OAuth 시작 주소를 요청합니다.</summary>
        public IEnumerator StartLogin(string provider, string verifier, Action<ServerApiResult> completed)
        {
            if (provider != "GOOGLE" && provider != "KAKAO") throw new ArgumentException("지원하지 않는 로그인 제공자입니다.");
            string hash; using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(verifier)));
            return Send("POST", "/api/auth/login/start", new JObject { ["provider"] = provider, ["verifierHash"] = hash }, completed, false);
        }
        /// <summary>handoff와 verifier를 토큰으로 교환하고 성공한 토큰을 메모리에 반영합니다.</summary>
        public IEnumerator Exchange(string handoff, string verifier, Action<ServerApiResult> completed)
        {
            return Send("POST", "/api/auth/exchange", new JObject { ["handoff"] = handoff, ["verifier"] = verifier, ["deviceLabel"] = "Unity client" }, result =>
            { completed(result.Success ? ApplyTokenResponse(result) : result); }, false);
        }
        /// <summary>서버 로그아웃에 성공한 경우 클라이언트의 메모리 토큰을 제거합니다.</summary>
        public IEnumerator Logout(Action<ServerApiResult> completed) => Send("POST", "/api/auth/logout", null, result =>
        { if (result.Success) SetTokens(null, null); completed(result); });
    }
}
