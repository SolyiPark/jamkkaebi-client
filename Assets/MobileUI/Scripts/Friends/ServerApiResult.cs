using Newtonsoft.Json.Linq;

namespace MobilePrototype.Friends
{
    public sealed class ServerApiResult
    {
        public bool Success { get; }
        public string Code { get; }
        public string Message { get; }
        public JToken Data { get; }
        public long Status { get; }
        public ServerApiResult(bool success, string code, string message, JToken data = null, long status = 0)
        { Success = success; Code = code; Message = message; Data = data; Status = status; }
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
