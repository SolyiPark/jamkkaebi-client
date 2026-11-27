using System.IO;
using System.Linq;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Friends;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    /// <summary>
    /// 로컬 서버(jamkkaebi-server main 677c295, 2026-11-20)가 실제로 돌려준 친구 방문 응답을 그대로 저장해 두고,
    /// 전시 스냅샷 변환 코드가 그 응답을 빠짐없이 읽는지 검증합니다. 서버 없이 돕니다.
    /// 응답 원본은 LocalServerFixtures 폴더의 json 파일입니다.
    /// </summary>
    public sealed class LocalServerVisitResponseTests
    {
        /// <summary>저장해 둔 서버 응답을 읽어 성공 응답의 data 부분을 반환합니다.</summary>
        private static JToken Data(string name)
        {
            string path = Path.Combine(Application.dataPath, "Jamkkaebi/Tests/UI/EditMode/LocalServerFixtures", name + ".json");
            var result = ServerApiResult.Parse(File.ReadAllText(path), 200);
            Assert.IsTrue(result.Success, name);
            return result.Data;
        }

        /// <summary>현재 서버는 각성 단계를 보내지 않습니다. 그래도 배치한 도깨비 3마리를 모두 읽어야 합니다.</summary>
        [Test]
        public void VisitSnapshotKeepsEverySpiritWithoutAwakeningStage()
        {
            var snapshot = ServerExhibitionBackend.ParseSnapshot(Data("visit_a"));

            Assert.AreEqual(0, snapshot.ParseWarnings.Count);
            Assert.AreEqual(3, snapshot.Placements.Count);
            CollectionAssert.AreEquivalent(new[] { 2, 4, 6 }, snapshot.Placements.Select(row => row.Id).ToArray());
            var incense = snapshot.Placements.Single(row => row.Id == 2);
            Assert.AreEqual("향산이", incense.Name);
            Assert.AreEqual(new Vector2Int(3, 3), incense.Anchor);
            Assert.IsFalse(incense.IsBuilding);
            Assert.IsNull(incense.Stage);

            var scene = new ExhibitionVisitScene(snapshot);
            Assert.AreEqual(3, scene.Objects.Count);
            Assert.AreEqual(0, scene.Warnings.Count);
        }
    }
}
