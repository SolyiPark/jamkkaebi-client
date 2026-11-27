using System.IO;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Friends;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    /// <summary>
    /// 로컬 서버(jamkkaebi-server main 677c295, 2026-11-20)가 실제로 돌려준 친구·요청·추천·차단 응답을 그대로 저장해 두고,
    /// 친구 탭 변환 코드가 서버 시각을 정해진 형식으로 표시하는지 검증합니다. 서버 없이 돕니다.
    /// 응답 원본은 LocalServerFixtures 폴더의 json 파일입니다.
    /// </summary>
    public sealed class LocalServerSocialResponseTests
    {
        /// <summary>저장해 둔 서버 응답을 읽어 성공 응답의 data 부분을 반환합니다.</summary>
        private static JToken Data(string name)
        {
            string path = Path.Combine(Application.dataPath, "Jamkkaebi/Tests/UI/EditMode/LocalServerFixtures", name + ".json");
            var result = ServerApiResult.Parse(File.ReadAllText(path), 200);
            Assert.IsTrue(result.Success, name);
            return result.Data;
        }

        /// <summary>요청 만료 시각과 차단 시각은 기기 언어 설정과 무관하게 yyyy-MM-dd HH:mm 형식으로 표시합니다.</summary>
        [Test]
        public void RequestExpiryAndBlockTimesUseFixedFormat()
        {
            var state = FriendsServerMapper.Create(Data("friends"), Data("received"), Data("sent"), Data("recommendations"), Data("blocks"));

            StringAssert.Contains("2026-12-04 16:27", state.Received[0].LastSeen);
            StringAssert.Contains("2026-12-04 16:27", state.Sent[0].LastSeen);
            StringAssert.Contains("2026-11-20 16:27", state.Blocked[0].LastSeen);
        }
    }
}
