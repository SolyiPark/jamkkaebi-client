using Newtonsoft.Json.Linq;

namespace Jamkkaebi.Scripts.UI.Friends
{
    public sealed class ServerApiResult
    {
        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public JToken Data { get; }
        public long Status { get; }
        /// <summary>성공 여부·응답 코드·메시지·선택 데이터와 HTTP 상태를 보관합니다.</summary>
        public ServerApiResult(bool success, string code, string message, JToken data = null, long status = 0)
        { Success = success; Code = code; Message = message; Data = data; Status = status; }
        /// <summary>HTTP 상태와 ApiResponse 필수 필드를 검증해 선택적인 data를 포함한 결과를 만듭니다.</summary>
        public static ServerApiResult Parse(string json, long status)
        {
            var envelope = JObject.Parse(json);
            if (envelope["success"]?.Type != JTokenType.Boolean || envelope["code"]?.Type != JTokenType.String)
                throw new System.FormatException("서버 응답 형식이 올바르지 않아요.");
            bool success = (bool)envelope["success"];
            if (success && (status < 200 || status >= 300)) throw new System.FormatException("HTTP 상태와 응답이 일치하지 않아요.");
            return new ServerApiResult(success, (string)envelope["code"], (string)envelope["message"], envelope["data"], status);
        }
    }
}
