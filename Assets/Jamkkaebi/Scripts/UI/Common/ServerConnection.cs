using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// 서버 주소·토큰을 찾아 공용 API 클라이언트를 만든다. 주소가 없으면 null이고, 부르는 쪽은 모의 모드로 동작한다.
    /// </summary>
    /// <remarks>
    /// 로그인 화면이 아직 없어 토큰은 프로세스 환경 변수로만 받는다(친구 탭과 같은 방식).
    /// 에디터에서는 환경 변수가 없으면 로컬 검증 서버 스크립트(Tools/serve-workbench-contract.sh)가 만든
    /// <see cref="DevSessionPath"/>를 읽는다. 이 파일은 Temp 아래에 있어 커밋되지 않고 스크립트가 끝나면 지운다.
    /// 토큰은 메모리에만 두고 기록하지 않는다.
    /// </remarks>
    public static class ServerConnection
    {
        public const string UrlVariable = "JAMKKAEBI_SERVER_URL";
        public const string AccessTokenVariable = "JAMKKAEBI_ACCESS_TOKEN";
        public const string RefreshTokenVariable = "JAMKKAEBI_REFRESH_TOKEN";

        /// <summary>로컬 검증 서버의 개발용 세션 파일(에디터 전용).</summary>
        public static string DevSessionPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "ServerContract", "workbench-session.json"));

        /// <summary>
        /// Inspector 주소, 없으면 환경 변수 주소로 클라이언트를 만든다. 둘 다 없으면 에디터에서만 개발용 세션 파일을 본다.
        /// </summary>
        /// <param name="inspectorBaseUrl">Inspector에 넣은 주소. 비우면 환경 변수를 쓴다.</param>
        /// <returns>클라이언트. 서버 주소가 없거나 올바르지 않으면 null(모의 모드).</returns>
        public static JamkkaebiApiClient Create(string inspectorBaseUrl)
        {
            string baseUrl = string.IsNullOrWhiteSpace(inspectorBaseUrl)
                ? Environment.GetEnvironmentVariable(UrlVariable)
                : inspectorBaseUrl;
            string access = Environment.GetEnvironmentVariable(AccessTokenVariable);
            string refresh = Environment.GetEnvironmentVariable(RefreshTokenVariable);

#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(baseUrl) && TryReadDevSession(out string devUrl, out string devAccess, out string devRefresh))
            {
                baseUrl = devUrl;
                access = devAccess;
                refresh = devRefresh;
                Debug.Log("[Server] 로컬 검증 서버의 개발용 세션으로 연결합니다.");
            }
#endif

            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            try
            {
                return new JamkkaebiApiClient(baseUrl.Trim(), access, refresh);
            }
            catch (ArgumentException)
            {
                // 실행 중 실패가 아니라 설정 오류다. 주소를 바로잡을 때까지 모의 모드로 둔다.
                Debug.LogError("서버 기본 주소가 올바르지 않아 서버에 연결하지 않습니다.");
                return null;
            }
        }

#if UNITY_EDITOR
        private static bool TryReadDevSession(out string baseUrl, out string access, out string refresh)
        {
            baseUrl = access = refresh = null;
            string path = DevSessionPath;
            if (!File.Exists(path)) return false;
            try
            {
                JObject session = JObject.Parse(File.ReadAllText(path));
                baseUrl = (string)session["baseUrl"];
                access = (string)session["playAccessToken"];
                refresh = (string)session["playRefreshToken"];
                return !string.IsNullOrWhiteSpace(baseUrl);
            }
            catch (Exception)
            {
                Debug.LogWarning("개발용 세션 파일을 읽지 못했습니다. 모의 모드로 둡니다.");
                return false;
            }
        }
#endif
    }
}
