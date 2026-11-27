using System;
using Jamkkaebi.Scripts.UI.Exhibition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    public sealed class ExhibitionLayoutTransmissionTests
    {
        /// <summary>숨긴 NPC와 표시 중인 건물을 포함한 서버 보유 목록 검증 fixture를 만듭니다.</summary>
        private static JObject Baseline() => JObject.Parse(@"{
            'grid':{'width':7,'height':7},
            'spirits':[{'relicId':2,'displayed':false,'hidden':false},
                {'relicId':8,'displayed':true,'hidden':true,'position':{'x':4,'y':4}}],
            'buildings':[{'buildingId':1,'displayed':true,'position':{'x':2,'y':2}}]}");

        /// <summary>일괄 요청의 허용 필드와 관리하지 않는 숨김 NPC의 배치 보존을 검증합니다.</summary>
        [Test]
        public void AggregateUsesOnlyServerFieldsAndPreservesUnmanagedHiddenSpirit()
        {
            var package = ExhibitionLayoutPackage.Build(new ExhibitionLayoutDraft(new Vector2Int(7,7),
                new ExhibitionObjectData[] { new ExhibitionNpcData("npc",2,true,new Vector2Int(1,1)),
                    new ExhibitionBuildingData("local/91",1,true,new Vector2Int(3,3),Vector2Int.one) }), Baseline());
            Assert.AreEqual(1, package.Count); Assert.AreEqual(3, ((JArray)package["items"]).Count);
            foreach (JObject item in package["items"]) Assert.AreEqual(4, item.Count);
            Assert.AreEqual(1, (int)package["items"][1]["id"]);
            Assert.AreEqual(8, (int)package["items"][2]["id"]);
        }

        /// <summary>회수한 관리 대상만 PUT 목록에서 제외되고 다른 서버 객체는 남는지 검증합니다.</summary>
        [Test]
        public void RecalledManagedObjectIsOmittedWhileUnmanagedObjectRemains()
        {
            var result = ExhibitionLayoutPackage.Build(new ExhibitionLayoutDraft(new Vector2Int(7,7),
                new[] { new ExhibitionBuildingData("building",1,false,Vector2Int.zero,Vector2Int.one) }), Baseline());
            Assert.AreEqual(1, ((JArray)result["items"]).Count);
            Assert.AreEqual("SPIRIT", (string)result["items"][0]["type"]);
        }

        /// <summary>미보유·미바인딩·회전·다칸·범위·충돌 배치가 저장 전에 거부되는지 검증합니다.</summary>
        [TestCase(0, 0, 1, 2, 2)] // 미바인딩
        [TestCase(99, 0, 1, 2, 2)] // 미보유
        [TestCase(1, 1, 1, 2, 2)] // 서버 미지원 회전
        [TestCase(1, 0, 2, 2, 2)] // 서버 미지원 다칸
        [TestCase(1, 0, 1, 7, 0)] // 범위 초과
        [TestCase(1, 0, 1, 4, 4)] // 보존 대상과 충돌
        public void UnsupportedOrDestructiveDraftIsRejected(int id, int turns, int size, int x, int y)
        {
            var draft = new ExhibitionLayoutDraft(new Vector2Int(7,7), new[] {
                new ExhibitionBuildingData("building",id,true,new Vector2Int(x,y),new Vector2Int(size,size),turns) });
            Assert.Throws<InvalidOperationException>(() => ExhibitionLayoutPackage.Build(draft, Baseline()));
        }

        /// <summary>동일 서버 ID 중복과 서로 다른 격자 규격의 저장 거부를 검증합니다.</summary>
        [Test]
        public void DuplicateServerIdentityAndGridMismatchAreRejected()
        {
            var item = new ExhibitionBuildingData("building",1,true,Vector2Int.zero,Vector2Int.one);
            Assert.Throws<InvalidOperationException>(() => ExhibitionLayoutPackage.Build(
                new ExhibitionLayoutDraft(new Vector2Int(7,7), new[] { item, item }), Baseline()));
            Assert.Throws<InvalidOperationException>(() => ExhibitionLayoutPackage.Build(
                new ExhibitionLayoutDraft(new Vector2Int(20,20), new[] { item }), Baseline()));
        }

        /// <summary>잘못된 보유 목록이 전체 배치를 지우는 빈 요청으로 변환되지 않는지 검증합니다.</summary>
        [Test]
        public void MalformedBaselineCannotBecomeEmptyFullReplacement()
        {
            var baseline = Baseline(); baseline.Remove("spirits");
            Assert.Throws<FormatException>(() => ExhibitionLayoutPackage.Build(
                new ExhibitionLayoutDraft(new Vector2Int(7,7), Array.Empty<ExhibitionObjectData>()), baseline));
        }
    }
}
