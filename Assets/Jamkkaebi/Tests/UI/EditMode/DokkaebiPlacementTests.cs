using System;
using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.UI.Exhibition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>도깨비 여러 마리 배치의 격자 규칙, 배회 정지·자리 옮기기, 고르는 목록, 보유 목록을 확인한다.</summary>
    public sealed class DokkaebiPlacementTests
    {
        private ExhibitionGrid _grid;
        private HousingLayout _layout;
        private readonly List<Object> _spawned = new List<Object>();

        [SetUp]
        public void SetUp()
        { _grid = ScriptableObject.CreateInstance<ExhibitionGrid>(); _layout = new HousingLayout(_grid); }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _spawned) if (obj) Object.DestroyImmediate(obj);
            _spawned.Clear();
            Object.DestroyImmediate(_grid);
        }

        private T Spawn<T>(string name) where T : Component
        {
            var obj = new GameObject(name);
            _spawned.Add(obj);
            return obj.AddComponent<T>();
        }

        private DokkaebiWanderer SpawnWanderer(Vector2Int cell, int relicId, out bool configured)
        {
            var surface = Spawn<ExhibitionSurface>("Surface " + relicId);
            typeof(ExhibitionSurface).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(surface, _grid);
            var wanderer = surface.gameObject.AddComponent<DokkaebiWanderer>();
            configured = wanderer.Configure(_layout, surface, cell, null, relicId);
            return wanderer;
        }

        private static void Step(DokkaebiWanderer wanderer) =>
            typeof(DokkaebiWanderer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wanderer, null);

        [Test]
        public void WalkersGetDistinctIdsInTheSameSpaceAsPlacements()
        {
            _layout.TryPlace("item", new Vector2Int(4, 4), Vector2Int.one, out var item);
            Assert.That(_layout.TryAddWalker(Vector2Int.zero, out var first), Is.True);
            Assert.That(_layout.TryAddWalker(new Vector2Int(2, 0), out var second), Is.True);
            Assert.That(new[] { item.Id, first.Id, second.Id }, Is.Unique);
            Assert.That(_layout.GetWalker(first.Id), Is.SameAs(first));
            Assert.That(_layout.GetWalker(item.Id), Is.Null);
            Assert.That(_layout.Walkers, Has.Count.EqualTo(2));
        }

        [Test]
        public void WalkerCannotStartOnPlacementAnotherAnchorOrOutside()
        {
            _layout.TryPlace("item", new Vector2Int(1, 1), Vector2Int.one, out _);
            _layout.TryAddWalker(Vector2Int.zero, out _);
            Assert.That(_layout.TryAddWalker(new Vector2Int(1, 1), out _), Is.False);
            Assert.That(_layout.TryAddWalker(Vector2Int.zero, out _), Is.False);
            Assert.That(_layout.TryAddWalker(new Vector2Int(-1, 0), out _), Is.False);
            Assert.That(_layout.Walkers, Has.Count.EqualTo(1));
        }

        [Test]
        public void WalkersCannotReserveEachOthersCells()
        {
            _layout.TryAddWalker(Vector2Int.zero, out var first);
            _layout.TryAddWalker(new Vector2Int(2, 0), out var second);
            Assert.That(_layout.TryReserveTraversal(first.Id, Vector2Int.zero, Vector2Int.right), Is.True);
            // 두 번째가 첫 번째의 목적지로 들어가지 못한다.
            Assert.That(_layout.TryReserveTraversal(second.Id, new Vector2Int(2, 0), Vector2Int.right), Is.False);
            Assert.That(second.To, Is.EqualTo(new Vector2Int(2, 0)));
            // 첫 번째가 도착해 떠난 칸은 다시 쓸 수 있다.
            Assert.That(_layout.TryReserveTraversal(first.Id, Vector2Int.right, Vector2Int.right), Is.True);
            Assert.That(_layout.TryReserveTraversal(second.Id, new Vector2Int(2, 0), new Vector2Int(2, 1)), Is.True);
            Assert.That(_layout.TryReserveTraversal(99, Vector2Int.zero, Vector2Int.zero), Is.False);
        }

        [Test]
        public void AnchorBlocksFixedPlacementEvenWhileWalkerIsAway()
        {
            _layout.TryAddWalker(new Vector2Int(3, 3), out var walker);
            _layout.TryReserveTraversal(walker.Id, new Vector2Int(3, 3), new Vector2Int(3, 4));
            _layout.TryReserveTraversal(walker.Id, new Vector2Int(3, 4), new Vector2Int(3, 4));
            Assert.That(walker.Anchor, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(_layout.CanPlace(new Vector2Int(3, 3), Vector2Int.one), Is.False);
            Assert.That(_layout.CanPlace(new Vector2Int(3, 4), Vector2Int.one), Is.False);
            Assert.That(_layout.CanPlace(new Vector2Int(2, 3), Vector2Int.one), Is.True);
        }

        [Test]
        public void MovingAnchorFreesOldCellsAndFailureChangesNothing()
        {
            _layout.TryPlace("item", new Vector2Int(5, 5), Vector2Int.one, out _);
            _layout.TryAddWalker(Vector2Int.zero, out var walker);
            _layout.TryAddWalker(new Vector2Int(6, 0), out var other);
            _layout.TryReserveTraversal(walker.Id, Vector2Int.zero, Vector2Int.right);

            Assert.That(_layout.TryMoveWalker(walker.Id, new Vector2Int(5, 5)), Is.False);
            Assert.That(_layout.TryMoveWalker(walker.Id, other.Anchor), Is.False);
            Assert.That(walker.Anchor, Is.EqualTo(Vector2Int.zero));
            Assert.That(walker.To, Is.EqualTo(Vector2Int.right));

            Assert.That(_layout.TryMoveWalker(walker.Id, new Vector2Int(3, 3)), Is.True);
            Assert.That(walker.Anchor, Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(walker.From, Is.EqualTo(walker.To));
            Assert.That(_layout.CanPlace(Vector2Int.zero, new Vector2Int(2, 1)), Is.True);
            // 자기 자리로 다시 옮기는 것은 겹침이 아니다.
            Assert.That(_layout.TryMoveWalker(walker.Id, new Vector2Int(3, 3)), Is.True);
        }

        [Test]
        public void WandererRegistersWithRelicAndRefusesTakenCell()
        {
            var first = SpawnWanderer(new Vector2Int(1, 1), 2, out bool firstConfigured);
            var second = SpawnWanderer(new Vector2Int(1, 1), 8, out bool secondConfigured);
            Assert.That(firstConfigured, Is.True);
            Assert.That(first.RelicId, Is.EqualTo(2));
            Assert.That(first.WalkerId, Is.Not.Zero);
            Assert.That(secondConfigured, Is.False);
            Assert.That(second.WalkerId, Is.Zero);
            Assert.That(_layout.Walkers, Has.Count.EqualTo(1));
        }

        [Test]
        public void PausedWandererStaysPutAndKeepsItsReservation()
        {
            var wanderer = SpawnWanderer(new Vector2Int(3, 3), 2, out _);
            Step(wanderer);
            Assert.That(wanderer.TargetCell, Is.Not.EqualTo(wanderer.CurrentCell), "첫 걸음을 예약해야 한다");
            Vector2Int target = wanderer.TargetCell;
            Vector3 position = wanderer.transform.position;

            wanderer.SetPaused(true);
            for (int i = 0; i < 3; i++) Step(wanderer);
            Assert.That(wanderer.Paused, Is.True);
            Assert.That(wanderer.transform.position, Is.EqualTo(position));
            Assert.That(wanderer.TargetCell, Is.EqualTo(target));
            Assert.That(_layout.CanPlace(target, Vector2Int.one), Is.False);
            Assert.That(wanderer.PlacementCell, Is.EqualTo(new Vector2Int(3, 3)));
        }

        [Test]
        public void WandererMovesAnchorAndReleasesOnDestroy()
        {
            var wanderer = SpawnWanderer(new Vector2Int(3, 3), 2, out _);
            _layout.TryPlace("item", new Vector2Int(5, 5), Vector2Int.one, out _);
            Step(wanderer);
            Assert.That(wanderer.TryMoveAnchor(new Vector2Int(5, 5)), Is.False);
            Assert.That(wanderer.PlacementCell, Is.EqualTo(new Vector2Int(3, 3)));

            Assert.That(wanderer.TryMoveAnchor(new Vector2Int(0, 6)), Is.True);
            Assert.That(wanderer.PlacementCell, Is.EqualTo(new Vector2Int(0, 6)));
            Assert.That(wanderer.CurrentCell, Is.EqualTo(new Vector2Int(0, 6)));
            Assert.That(wanderer.TargetCell, Is.EqualTo(new Vector2Int(0, 6)));
            Assert.That(_layout.CanPlace(new Vector2Int(3, 3), Vector2Int.one), Is.True);

            // 편집 모드에서는 Unity가 OnDestroy를 부르지 않아 직접 부른다.
            typeof(DokkaebiWanderer).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wanderer, null);
            Assert.That(_layout.Walkers, Is.Empty);
            Assert.That(_layout.CanPlace(new Vector2Int(0, 6), Vector2Int.one), Is.True);
        }

        [Test]
        public void PlaceableValidatesTypeAndIdAndSharesServerKeyFormat()
        {
            var spirit = new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, 2, "달빛");
            var building = new ExhibitionPlaceable(ExhibitionPlaceable.BuildingType, 2, "");
            Assert.That(spirit.Key, Is.EqualTo(new ExhibitionNpcData("local", 2, true, Vector2Int.zero).Key));
            Assert.That(building.Key, Is.EqualTo(new ExhibitionBuildingData("local", 2, true, Vector2Int.zero, Vector2Int.one).Key));
            Assert.That(spirit.IsSpirit, Is.True);
            Assert.That(building.IsSpirit, Is.False);
            Assert.That(building.DisplayName, Is.EqualTo(building.Key));
            Assert.Throws<ArgumentException>(() => new ExhibitionPlaceable("RELIC", 1, "x"));
            Assert.Throws<ArgumentException>(() => new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, 0, "x"));
        }

        [Test]
        public void InventoryReplacesItemsDropsDuplicatesAndNotifies()
        {
            var inventory = Spawn<ExhibitionInventory>("Inventory");
            int changes = 0; inventory.Changed += () => changes++;
            inventory.Set(new[]
            {
                new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, 2, "하나"),
                new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, 2, "겹침"),
                null,
                new ExhibitionPlaceable(ExhibitionPlaceable.BuildingType, 2, "건물")
            });
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(inventory.Items, Has.Count.EqualTo(2));
            Assert.That(inventory.Find("SPIRIT:2").DisplayName, Is.EqualTo("하나"));
            Assert.That(inventory.Find("SPIRIT:9"), Is.Null);
            // 자기 목록이나 그것을 걸러 낸 지연 열거를 넘겨도 내용을 잃지 않는다.
            inventory.Set(inventory.Items);
            Assert.That(inventory.Items, Has.Count.EqualTo(2));
            inventory.Set(System.Linq.Enumerable.Where(inventory.Items, item => item.IsSpirit));
            Assert.That(inventory.Items, Has.Count.EqualTo(1));
            Assert.That(inventory.Find("SPIRIT:2"), Is.Not.Null);
            changes = 1;
            inventory.Set(Array.Empty<ExhibitionPlaceable>());
            Assert.That(inventory.Items, Is.Empty);
            // 셸 없이 열었으면 조회하지 않고 상태도 바꾸지 않는다.
            inventory.Refresh();
            Assert.That(inventory.Loading, Is.False);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void PickerPagesOptionsAndReportsChosenKey()
        {
            var canvas = Spawn<Canvas>("Canvas");
            var picker = canvas.gameObject.AddComponent<HousingPlacementPicker>();
            picker.Configure((RectTransform)canvas.transform, null);
            Assert.That(picker.IsVisible, Is.False);

            var options = new List<HousingPlacementOption>();
            for (int i = 1; i <= 10; i++) options.Add(new HousingPlacementOption("SPIRIT:" + i, "도깨비 " + i));
            string chosen = null; bool cancelled = false;
            picker.Chosen += key => chosen = key; picker.Cancelled += () => cancelled = true;

            picker.Show(options, "안내");
            Assert.That(picker.IsVisible, Is.True);
            Assert.That(picker.VisibleOptionCount, Is.EqualTo(8));
            Click(canvas, "도깨비 3");
            Assert.That(chosen, Is.EqualTo("SPIRIT:3"));

            Click(canvas, "다음");
            Assert.That(picker.VisibleOptionCount, Is.EqualTo(2));
            Click(canvas, "도깨비 10");
            Assert.That(chosen, Is.EqualTo("SPIRIT:10"));

            Click(canvas, "취소");
            Assert.That(cancelled, Is.True);
            picker.Hide();
            Assert.That(picker.IsVisible, Is.False);
            // 선택지가 줄면 남은 쪽 안에서 다시 그린다.
            picker.Show(options.GetRange(0, 1));
            Assert.That(picker.VisibleOptionCount, Is.EqualTo(1));
        }

        [Test]
        public void PickerKeepsButtonsWhenOnlyTheMessageChanges()
        {
            var canvas = Spawn<Canvas>("Canvas");
            var picker = canvas.gameObject.AddComponent<HousingPlacementPicker>();
            picker.Configure((RectTransform)canvas.transform, null);
            var options = new List<HousingPlacementOption>
                { new HousingPlacementOption("SPIRIT:2", "하나"), new HousingPlacementOption("SPIRIT:8", "둘") };
            picker.Show(options, "도깨비 목록을 불러오는 중입니다.");
            Button pressed = Find(canvas, "하나");

            // 보유 목록을 다시 읽어 같은 선택지가 오면 누르고 있던 버튼이 그대로 남는다.
            picker.Show(new List<HousingPlacementOption>
                { new HousingPlacementOption("SPIRIT:2", "하나"), new HousingPlacementOption("SPIRIT:8", "둘") }, null);
            Assert.That(pressed != null, Is.True, "버튼이 다시 만들어지면 안 된다");
            Assert.That(Find(canvas, "하나"), Is.SameAs(pressed));
            Text message = null;
            foreach (var text in canvas.GetComponentsInChildren<Text>()) if (text.name == "Message") message = text;
            Assert.That(message.text, Is.Empty);

            string chosen = null; picker.Chosen += key => chosen = key;
            pressed.onClick.Invoke();
            Assert.That(chosen, Is.EqualTo("SPIRIT:2"));

            // 선택지가 달라지면 다시 그린다.
            picker.Show(options.GetRange(1, 1), null);
            Assert.That(pressed == null, Is.True);
            Assert.That(picker.VisibleOptionCount, Is.EqualTo(1));
        }

        private static Button Find(Canvas canvas, string label)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(false))
                if (button.name == label) return button;
            return null;
        }

        [Test]
        public void HoldMenuHidesRotateForWalkers()
        {
            var canvas = Spawn<Canvas>("Canvas");
            var menu = canvas.gameObject.AddComponent<HousingRadialMenu>();
            menu.Configure((RectTransform)canvas.transform, null, null);
            menu.Show(Vector2.zero);
            Assert.That(menu.VisibleActionCount, Is.EqualTo(3));
            menu.Show(Vector2.zero, false, false);
            Assert.That(menu.VisibleActionCount, Is.EqualTo(2));
            Assert.That(menu.TryChoose(new Vector2(200, -116), out _), Is.False, "숨긴 회전 방향은 고를 수 없다");
            menu.Show(Vector2.zero, true);
            Assert.That(menu.VisibleActionCount, Is.EqualTo(2));
            Assert.That(menu.IsFloorMenu, Is.True);
        }

        private static void Click(Canvas canvas, string label)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(false))
                if (button.name == label) { button.onClick.Invoke(); return; }
            Assert.Fail("버튼을 찾지 못했습니다: " + label);
        }
    }
}
