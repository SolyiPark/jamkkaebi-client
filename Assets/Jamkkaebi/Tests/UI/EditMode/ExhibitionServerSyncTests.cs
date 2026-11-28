using System;
using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Shell;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Step = Jamkkaebi.Scripts.UI.Exhibition.ExhibitionSyncSchedule.Step;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 홈 배치를 서버와 맞추는 판단과 변환을 확인한다: 응답 읽기, 보낼 때 정하기, 막힌 칸, 보낼 묶음.
    /// 실제 HTTP 왕복은 여기서 다루지 않는다.
    /// </summary>
    public sealed class ExhibitionServerSyncTests
    {
        private static readonly Vector2Int Grid = new Vector2Int(7, 7);

        private static JObject Response() => JObject.Parse(@"{
            'grid':{'width':7,'height':7},
            'spirits':[
                {'relicId':2,'spiritName':'달빛','displayed':true,'hidden':false,'position':{'x':1,'y':1}},
                {'relicId':8,'displayed':true,'hidden':true,'position':{'x':4,'y':4}},
                {'relicId':9,'spiritName':'새벽','displayed':false,'hidden':false}],
            'buildings':[
                {'buildingId':1,'displayed':true,'position':{'x':5,'y':0}},
                {'buildingId':2,'displayed':false}],
            'today':'2026-11-28','now':'2026-11-28T21:00:00','nickname':'발굴자','recentVisitDates':['2026-11-28']}");

        // ---------- 응답 읽기 ----------

        [Test]
        public void SnapshotReadsOwnedSpiritsPlacedCellsAndBuildingFootprints()
        {
            var snapshot = ExhibitionServerSnapshot.Parse(Response(), Grid);

            Assert.That(snapshot.Spirits, Has.Count.EqualTo(3));
            Assert.That(snapshot.Spirits[0].DisplayName, Is.EqualTo("달빛"));
            Assert.That(snapshot.Spirits[1].DisplayName, Is.EqualTo("SPIRIT:8"), "이름이 없으면 키로 대신한다");
            Assert.That(snapshot.SpiritCells, Has.Count.EqualTo(2));
            Assert.That(snapshot.SpiritCells[2], Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(snapshot.SpiritCells[8], Is.EqualTo(new Vector2Int(4, 4)), "숨긴 도깨비도 주인의 홈에는 놓여 있다");
            Assert.That(snapshot.SpiritCells.ContainsKey(9), Is.False);
            Assert.That(snapshot.BuildingCells, Is.EquivalentTo(new[]
                { new Vector2Int(5, 0), new Vector2Int(6, 0), new Vector2Int(5, 1), new Vector2Int(6, 1) }),
                "건물은 왼쪽 위 칸에서 2×2를 차지한다");
        }

        [Test]
        public void SnapshotRejectsAnotherGridInsteadOfShrinkingIt()
        {
            var response = Response(); response["grid"]["width"] = 20; response["grid"]["height"] = 20;
            Assert.Throws<InvalidOperationException>(() => ExhibitionServerSnapshot.Parse(response, Grid));
        }

        [TestCase("{}")]
        [TestCase("{'grid':{'width':7,'height':7},'spirits':[]}")]
        [TestCase("{'grid':{'width':7,'height':7},'spirits':[{'relicId':2}],'buildings':[]}")]
        [TestCase("{'grid':{'width':7,'height':7},'spirits':[{'relicId':2,'displayed':true}],'buildings':[]}")]
        [TestCase("{'grid':{'width':7,'height':7},'spirits':[{'relicId':2,'displayed':true,'position':{'x':7,'y':0}}],'buildings':[]}")]
        [TestCase("{'grid':{'width':7,'height':7},'spirits':[{'relicId':2,'displayed':false},{'relicId':2,'displayed':false}],'buildings':[]}")]
        [TestCase("{'grid':{'width':7,'height':7},'spirits':[],'buildings':[{'buildingId':1,'displayed':true,'position':{'x':6,'y':0}}]}")]
        public void SnapshotRejectsMalformedResponses(string json)
        {
            Assert.Throws<FormatException>(() => ExhibitionServerSnapshot.Parse(JObject.Parse(json), Grid));
        }

        [Test]
        public void SnapshotRejectsMissingData()
        {
            Assert.Throws<FormatException>(() => ExhibitionServerSnapshot.Parse(null, Grid));
        }

        // ---------- 보낼 때 정하기 ----------

        [Test]
        public void ScheduleLoadsFirstThenWaitsAFullIntervalBetweenDecisions()
        {
            var schedule = new ExhibitionSyncSchedule(10, 180);
            Assert.That(schedule.Tick(9, true, "a"), Is.EqualTo(Step.Wait));
            Assert.That(schedule.Tick(10, true, "a"), Is.EqualTo(Step.Load), "첫 차례에는 서버 배치부터 불러온다");
            Assert.That(schedule.Tick(11, true, "a"), Is.EqualTo(Step.Wait));
            Assert.That(schedule.Tick(189.9f, true, "a"), Is.EqualTo(Step.Wait));
            Assert.That(schedule.Tick(190, true, "a"), Is.EqualTo(Step.Load), "불러오지 못했으면 다음 차례에 다시 불러온다");
        }

        [Test]
        public void ScheduleNeverSendsBeforeTheServerLayoutIsLoaded()
        {
            var schedule = new ExhibitionSyncSchedule(0, 180);
            for (int i = 0; i < 5; i++)
                Assert.That(schedule.Tick(i * 180, true, "changed-" + i), Is.EqualTo(Step.Load));
            Assert.That(schedule.Loaded, Is.False);
        }

        [Test]
        public void ScheduleSendsOnlyWhenTheLayoutDiffersFromTheLastSyncedOne()
        {
            var schedule = new ExhibitionSyncSchedule(0, 180);
            schedule.Tick(0, true, "a");
            schedule.MarkLoaded("a");
            Assert.That(schedule.Tick(180, true, "a"), Is.EqualTo(Step.Skip), "불러온 그대로면 보내지 않는다");
            Assert.That(schedule.Tick(360, true, "b"), Is.EqualTo(Step.Send));
            // 저장에 실패하면 맞춘 것으로 치지 않는다. 다음 차례에 그때의 배치를 보낸다.
            Assert.That(schedule.Tick(361, true, "b"), Is.EqualTo(Step.Wait), "그 자리에서 다시 보내지 않는다");
            Assert.That(schedule.Tick(540, true, "c"), Is.EqualTo(Step.Send));
            schedule.MarkSent("c");
            Assert.That(schedule.Tick(720, true, "c"), Is.EqualTo(Step.Skip));
            Assert.That(schedule.Tick(900, true, "a"), Is.EqualTo(Step.Send), "예전 배치로 돌아가도 마지막에 맞춘 것과 다르면 보낸다");
        }

        [Test]
        public void ScheduleSkipsTheTurnWithoutInternet()
        {
            var schedule = new ExhibitionSyncSchedule(0, 180);
            Assert.That(schedule.Tick(0, false, "a"), Is.EqualTo(Step.Skip));
            Assert.That(schedule.Tick(1, true, "a"), Is.EqualTo(Step.Wait), "연결이 돌아와도 다음 차례까지 기다린다");
            Assert.That(schedule.Tick(180, true, "a"), Is.EqualTo(Step.Load));
            schedule.MarkLoaded("a");
            Assert.That(schedule.Tick(360, false, "b"), Is.EqualTo(Step.Skip));
            Assert.That(schedule.Tick(540, true, "b"), Is.EqualTo(Step.Send));
        }

        [Test]
        public void ScheduleDefaultsToThreeMinutesAndRejectsBadIntervals()
        {
            Assert.That(ExhibitionSyncSchedule.DefaultIntervalSeconds, Is.EqualTo(180f));
            var schedule = new ExhibitionSyncSchedule(0);
            schedule.Tick(0, true, "a");
            Assert.That(schedule.Tick(179, true, "a"), Is.EqualTo(Step.Wait));
            Assert.That(schedule.Tick(180, true, "a"), Is.EqualTo(Step.Load));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExhibitionSyncSchedule(0, 0));
        }

        // ---------- 막힌 칸 ----------

        [Test]
        public void BlockedCellsRefusePlacementAndWalking()
        {
            var grid = ScriptableObject.CreateInstance<ExhibitionGrid>();
            try
            {
                var layout = new HousingLayout(grid);
                layout.TryAddWalker(new Vector2Int(4, 0), out var walker);
                layout.SetBlockedCells(new[] { new Vector2Int(5, 0), new Vector2Int(5, 1) });

                Assert.That(layout.IsBlocked(new Vector2Int(5, 0)), Is.True);
                Assert.That(layout.IsWalkable(new Vector2Int(5, 0)), Is.False);
                Assert.That(layout.CanPlace(new Vector2Int(5, 0), Vector2Int.one), Is.False);
                Assert.That(layout.CanPlace(new Vector2Int(4, 1), new Vector2Int(2, 1)), Is.False, "발자국 일부만 걸쳐도 놓을 수 없다");
                Assert.That(layout.CanAddWalker(new Vector2Int(5, 1)), Is.False);
                Assert.That(layout.TryReserveTraversal(walker.Id, new Vector2Int(4, 0), new Vector2Int(5, 0)), Is.False);
                Assert.That(layout.TryMoveWalker(walker.Id, new Vector2Int(5, 1)), Is.False);

                // 통째로 바꾼다. 자기 목록에서 걸러 넘겨도 된다.
                layout.SetBlockedCells(new[] { new Vector2Int(0, 0) });
                Assert.That(layout.IsWalkable(new Vector2Int(5, 0)), Is.True);
                Assert.That(layout.IsBlocked(new Vector2Int(0, 0)), Is.True);
                layout.SetBlockedCells(Array.Empty<Vector2Int>());
                Assert.That(layout.IsBlocked(new Vector2Int(0, 0)), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(grid); }
        }

        // ---------- 하우징을 세워 확인하는 것 ----------

        private GameObject _root;
        private ExhibitionGrid _grid;
        private ExhibitionHousing _housing;

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private void BuildHousing()
        {
            _grid = ScriptableObject.CreateInstance<ExhibitionGrid>();
            _root = new GameObject("Home");
            var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(_root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            _root.AddComponent<MirrorSceneRoot>().sceneCamera = camera;
            var surfaceObject = new GameObject("Surface"); surfaceObject.transform.SetParent(_root.transform, false);
            var surface = surfaceObject.AddComponent<ExhibitionSurface>();
            SetField(surface, "_grid", _grid);
            var content = new GameObject("Content"); content.transform.SetParent(surfaceObject.transform, false);
            _housing = _root.AddComponent<ExhibitionHousing>();
            SetField(_housing, "_surface", surface);
            SetField(_housing, "_content", content.transform);
            typeof(ExhibitionHousing).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_housing, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root) UnityEngine.Object.DestroyImmediate(_root);
            if (_grid) UnityEngine.Object.DestroyImmediate(_grid);
        }

        private static ExhibitionPlaceable Spirit(int relicId) =>
            new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, relicId, "도깨비 " + relicId);

        [Test]
        public void HomeStartsEmptyAndOffersOnlyUnplacedOwnedSpirits()
        {
            BuildHousing();
            Assert.That(_housing.Layout.Placements, Is.Empty, "임시 전시대 없이 시작한다");
            Assert.That(_housing.Wanderers, Is.Empty);
            Assert.That(_housing.PlacementOptions(), Is.Empty, "보유 도깨비가 없으면 놓을 것이 없다");

            _housing.Inventory.Set(new[] { Spirit(2), Spirit(8) });
            _housing.TryAddSpirit(Spirit(2), new Vector2Int(1, 1));
            var options = _housing.PlacementOptions();
            Assert.That(options, Has.Count.EqualTo(1));
            Assert.That(options[0].Key, Is.EqualTo("SPIRIT:8"), "아직 놓지 않은 도깨비만 나온다");
        }

        [Test]
        public void HousingWithoutAServerAddressDoesNotSync()
        {
            BuildHousing();
            Assert.That(_housing.ServerSync, Is.Null, "셸의 서버 클라이언트가 없으면(모의 모드) 붙이지 않는다");
        }

        [Test]
        public void SpiritIsPlacedOnTheNearestFreeCellWhenItsSavedCellIsTaken()
        {
            BuildHousing();
            _housing.Layout.TryPlace("stand", new Vector2Int(2, 2), Vector2Int.one, out _);
            _housing.Layout.SetBlockedCells(new[] { new Vector2Int(2, 1), new Vector2Int(1, 2) });

            Assert.That(_housing.TryAddSpiritNear(Spirit(2), new Vector2Int(2, 2)), Is.True);
            var cell = _housing.FindWanderer(2).PlacementCell;
            Assert.That(cell, Is.Not.EqualTo(new Vector2Int(2, 2)));
            Assert.That(Mathf.Abs(cell.x - 2) + Mathf.Abs(cell.y - 2), Is.EqualTo(1), "한 칸 떨어진 빈칸에 선다");
            Assert.That(_housing.Layout.IsBlocked(cell), Is.False);

            Assert.That(_housing.TryAddSpiritNear(Spirit(2), new Vector2Int(6, 6)), Is.False, "같은 정령은 다시 놓이지 않는다");
            Assert.That(_housing.TryAddSpiritNear(Spirit(8), new Vector2Int(6, 6)), Is.True);
            Assert.That(_housing.FindWanderer(8).PlacementCell, Is.EqualTo(new Vector2Int(6, 6)), "빈칸이면 그 칸에 선다");
        }

        [Test]
        public void SignatureIsStableForTheSameLayoutAndChangesWhenASpiritMoves()
        {
            BuildHousing();
            _housing.TryAddSpirit(Spirit(8), new Vector2Int(4, 4));
            _housing.TryAddSpirit(Spirit(2), new Vector2Int(1, 1));

            string signature = ExhibitionServerSync.SignatureOf(_housing.Wanderers);
            Assert.That(signature, Is.EqualTo("2@1,1;8@4,4;"), "놓은 순서와 무관하게 정령 순으로 적는다");
            Assert.That(ExhibitionServerSync.SignatureOf(_housing.Wanderers), Is.EqualTo(signature));

            _housing.FindWanderer(2).TryMoveAnchor(new Vector2Int(0, 3));
            Assert.That(ExhibitionServerSync.SignatureOf(_housing.Wanderers), Is.EqualTo("2@0,3;8@4,4;"));
            Assert.That(ExhibitionServerSync.SignatureOf(Array.Empty<DokkaebiWanderer>()), Is.Empty);
        }

        [Test]
        public void SentPackageCarriesPlacedSpiritsDropsRecalledOnesAndKeepsServerBuildings()
        {
            BuildHousing();
            _housing.TryAddSpirit(Spirit(2), new Vector2Int(0, 3));
            _housing.TryAddSpirit(Spirit(9), new Vector2Int(3, 5));
            var owned = new[] { 2, 8, 9 };
            var noBindings = new Dictionary<string, int>();

            var draft = ExhibitionLayoutCapture.Capture(_housing, noBindings, owned);
            var items = (JArray)ExhibitionLayoutPackage.Build(draft, Response())["items"];

            var sent = new List<string>();
            foreach (var item in items) sent.Add($"{item["type"]}:{item["id"]}@{item["x"]},{item["y"]}");
            Assert.That(sent, Is.EquivalentTo(new[] { "SPIRIT:2@0,3", "SPIRIT:9@3,5", "BUILDING:1@5,0" }),
                "놓인 도깨비는 지금 자리로, 서버의 건물은 그대로, 홈에 없는 8번은 회수로 빠진다");

            // 서버 ID와 묶이지 않은 고정 배치물이 있으면 송신 전에 거부된다(기존 계약). 홈은 이제 그런 배치물 없이 시작한다.
            _housing.Layout.TryPlace("unbound", new Vector2Int(2, 2), Vector2Int.one, out _);
            var withUnbound = ExhibitionLayoutCapture.Capture(_housing, noBindings, owned);
            Assert.Throws<InvalidOperationException>(() => ExhibitionLayoutPackage.Build(withUnbound, Response()));
        }

        [Test]
        public void LoadedSnapshotCellsAreWhereSpiritsStandAndBuildingsBlock()
        {
            BuildHousing();
            var snapshot = ExhibitionServerSnapshot.Parse(Response(), Grid);
            _housing.Layout.SetBlockedCells(snapshot.BuildingCells);
            foreach (var spirit in snapshot.Spirits)
                if (snapshot.SpiritCells.TryGetValue(spirit.ServerId, out var cell)) _housing.TryAddSpiritNear(spirit, cell);

            Assert.That(_housing.FindWanderer(2).PlacementCell, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(_housing.FindWanderer(8).PlacementCell, Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(_housing.FindWanderer(9), Is.Null, "놓이지 않은 정령은 세우지 않는다");
            Assert.That(_housing.TryAddSpirit(Spirit(9), new Vector2Int(5, 0)), Is.False, "서버의 건물 자리에는 놓을 수 없다");
            // 불러온 그대로를 다시 묶으면 서버 배치와 같다.
            var draft = ExhibitionLayoutCapture.Capture(_housing, new Dictionary<string, int>(), new[] { 2, 8, 9 });
            Assert.That(((JArray)ExhibitionLayoutPackage.Build(draft, Response())["items"]), Has.Count.EqualTo(3));
        }
    }
}
