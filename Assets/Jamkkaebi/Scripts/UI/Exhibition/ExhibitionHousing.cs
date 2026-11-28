using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Friends;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    public sealed class ExhibitionHousing : MonoBehaviour
    {
        private const string ItemOptionPrefix = "ITEM:";

        [SerializeField] private ExhibitionSurface _surface;
        [SerializeField] private Transform _content;
        [SerializeField] private Font _font;
        [SerializeField] private Material _material;
        [SerializeField] private HousingItemDefinition _defaultItem;
        [SerializeField] private Vector2Int _initialCell = new Vector2Int(2, 2);
        [SerializeField] private bool _spawnInitialItem = true;
        [SerializeField] private ExhibitionBehaviorPolicy _policy;
        [SerializeField] private ExhibitionSelectionController _selectionController;
        [Tooltip("relicId별 도깨비 표현. 친구 방문과 같은 자산을 쓴다. 비었거나 프리팹이 없는 정령은 더미 그림으로 그린다.")]
        [SerializeField] private ExhibitionVisualCatalog _visualCatalog;
        private MirrorSceneRoot _root;
        private ExhibitionDummyVisuals _visuals;
        private readonly Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, HousingItemDefinition> _definitions = new Dictionary<int, HousingItemDefinition>();
        // 놓인 도깨비. 키는 하우징 배치 인스턴스 ID이고 전시물의 배치 ID와 겹치지 않는다.
        private readonly Dictionary<int, DokkaebiWanderer> _wanderers = new Dictionary<int, DokkaebiWanderer>();
        private HousingItemDefinition _pendingItem;
        private ExhibitionPlaceable _pendingPlaceable;
        private int _selectedId;
        private bool _placing;
        private readonly Dictionary<SpriteRenderer, float> _editingAlphas = new Dictionary<SpriteRenderer, float>();
        public ExhibitionSurface Surface => _surface;
        public Camera SceneCamera => _root.sceneCamera;
        public HousingLayout Layout { get; private set; }

        /// <summary>놓인 도깨비 전부. 순서는 정하지 않는다.</summary>
        public IReadOnlyCollection<DokkaebiWanderer> Wanderers => _wanderers.Values;

        public HousingRadialMenu Menu { get; private set; }

        /// <summary>놓을 수 있는 보유 객체 목록.</summary>
        public ExhibitionInventory Inventory { get; private set; }

        /// <summary>놓을 대상을 고르는 목록.</summary>
        public HousingPlacementPicker Picker { get; private set; }

        public bool IsPlacing => _placing;
        public bool IsEditingCell => _placing || _selectedId != 0;
        public bool IsPicking => Picker && Picker.IsVisible;
        public bool CanEdit => !_policy || _policy.AllowLayoutEditing;
        public ExhibitionBehaviorPolicy BehaviorPolicy => _policy;
        public ExhibitionSelectionController SelectionController => _selectionController;
        public HousingItemDefinition DefaultItem => _defaultItem;
        // A catalog/inventory bridge chooses an item and then calls BeginPlacement(definition).
        public event Action PlacementRequested;
        public event Action<HousingPlacement> PlacementAdded;
        public event Action<HousingPlacement> PlacementRemoved;

        /// <summary>도깨비를 놓았을 때.</summary>
        public event Action<DokkaebiWanderer> SpiritPlaced;

        /// <summary>도깨비를 회수했을 때. 인자는 relicId.</summary>
        public event Action<int> SpiritRecalled;

        public void ShowHoldMenu(Vector2 texturePoint, int itemId)
        {
            if (!CanEdit) return;
            ClosePicker();
            if (_selectionController) _selectionController.Clear();
            bool floorMenu = itemId == 0;
            Menu.SetMessage(floorMenu ? "배치·취소 방향으로 밀어주세요." : "원하는 버튼 방향으로 밀어 선택하세요.");
            // 도깨비는 회전이 없어 이동·회수만 보인다.
            Menu.Show(texturePoint, floorMenu, !_wanderers.ContainsKey(itemId));
        }

        public void HideHoldMenu()
        {
            if (Menu) Menu.Hide();
        }

        private void Start()
        {
            _root = GetComponent<MirrorSceneRoot>();
            if (!_selectionController) _selectionController = GetComponent<ExhibitionSelectionController>();
            if (!_selectionController) _selectionController = gameObject.AddComponent<ExhibitionSelectionController>();
            _selectionController.Configure(_policy, GetComponent<ExhibitionCameraController>());
            Layout = new HousingLayout(_surface.Grid);
            _visuals = new ExhibitionDummyVisuals(_material);
            // Keep the collider off the artwork renderer: its texture is intentionally non-readable.
            var floorInput = new GameObject("Housing Floor Input");
            _root.RegisterSpawnedObject(floorInput);
            floorInput.transform.SetParent(_surface.transform, false);
            var collider = floorInput.AddComponent<PolygonCollider2D>();
            var grid = _surface.Grid;
            collider.points = new[] { grid.ToLocal(Vector2.zero), grid.ToLocal(new Vector2(grid.Size.x, 0)),
                grid.ToLocal((Vector2)grid.Size), grid.ToLocal(new Vector2(0, grid.Size.y)) };
            // 빈 배경 클릭도 같은 루트 핸들러로 전달하며 스와이프 소유권은 셸에 남깁니다.
            var backgroundInput = new GameObject("Exhibition Selection Input", typeof(BoxCollider2D));
            _root.RegisterSpawnedObject(backgroundInput);
            backgroundInput.transform.SetParent(transform, false);
            backgroundInput.GetComponent<BoxCollider2D>().size = Vector2.one * 40;
            MirrorSceneRoot.SetLayer(backgroundInput.transform, _root.RenderLayer);
            gameObject.AddComponent<HousingSurfaceInput>().Configure(this);
            Inventory = gameObject.AddComponent<ExhibitionInventory>();
            Inventory.Changed += OnInventoryChanged;
            CreateControls();
            if (_spawnInitialItem) TryAdd(_defaultItem, _initialCell);
            ResetSelection();
            // 도깨비는 처음부터 놓여 있지 않다. 플레이어가 목록에서 골라 놓는다.
            Inventory.Refresh();
        }

        private GameObject ContentObject(string name)
        {
            var obj = new GameObject(name);
            _root.RegisterSpawnedObject(obj);
            obj.transform.SetParent(_content, false);
            obj.AddComponent<ExhibitionDepth>().Configure(_surface, obj.transform);
            return obj;
        }

        private bool TryAdd(HousingItemDefinition definition, Vector2Int cell)
        {
            if (!definition || !definition.IsValid || !Layout.TryPlace(definition.ItemId, cell, definition.Footprint, out var placement)) return false;
            var obj = ContentObject(definition.DisplayName + " " + placement.Id);
            if (definition.VisualPrefab) Instantiate(definition.VisualPrefab, obj.transform, false);
            else _visuals.ExhibitionStand(obj.transform);
            var hitArea = obj.AddComponent<BoxCollider2D>();
            hitArea.size = definition.HitSize; hitArea.offset = definition.HitOffset;
            MirrorSceneRoot.SetLayer(obj.transform, _root.RenderLayer);
            obj.transform.position = _surface.CellWorldPosition(cell);
            _objects.Add(placement.Id, obj);
            _definitions.Add(placement.Id, definition);
            UpdateFacing(placement);
            PlacementAdded?.Invoke(placement);
            return true;
        }

        /// <summary>이 정령의 도깨비가 전시관에 놓여 있는지.</summary>
        public bool IsSpiritPlaced(int relicId) => FindWanderer(relicId);

        /// <summary>이 정령의 놓인 도깨비. 없으면 null.</summary>
        public DokkaebiWanderer FindWanderer(int relicId)
        {
            foreach (var wanderer in _wanderers.Values)
                if (wanderer && wanderer.RelicId == relicId) return wanderer;
            return null;
        }

        /// <summary>
        /// 도깨비 한 마리를 기준 자리에 놓는다. 같은 정령은 한 마리만 놓을 수 있다.
        /// </summary>
        /// <returns>이미 놓였거나 그 칸을 쓸 수 없으면 false이며 아무것도 만들지 않는다.</returns>
        /// <exception cref="ArgumentNullException">spirit이 null인 경우.</exception>
        /// <exception cref="ArgumentException">spirit이 도깨비가 아닌 경우.</exception>
        public bool TryAddSpirit(ExhibitionPlaceable spirit, Vector2Int cell)
        {
            if (spirit == null) throw new ArgumentNullException(nameof(spirit));
            if (!spirit.IsSpirit) throw new ArgumentException("도깨비만 놓을 수 있습니다: " + spirit.Key, nameof(spirit));
            if (!CanEdit || IsSpiritPlaced(spirit.ServerId) || !Layout.CanAddWalker(cell)) return false;

            var obj = ContentObject("Dokkaebi " + spirit.ServerId);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(obj.transform, false);
            var touch = new Rect(-.12f, 0, .24f, .45f);
            if (_visualCatalog && _visualCatalog.TryGet(false, spirit.ServerId, out var definition) && definition.Prefab)
            {
                var instance = Instantiate(definition.Prefab, visual.transform, false);
                instance.transform.localPosition = definition.FootOffset;
                instance.transform.localScale = definition.Scale;
                foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>()) renderer.sharedMaterial = _material;
                touch = definition.TouchRect;
            }
            else _visuals.Dokkaebi(visual.transform);
            var hitArea = obj.AddComponent<BoxCollider2D>();
            hitArea.size = touch.size; hitArea.offset = touch.center;
            MirrorSceneRoot.SetLayer(obj.transform, _root.RenderLayer);

            var wanderer = obj.AddComponent<DokkaebiWanderer>();
            if (!wanderer.Configure(Layout, _surface, cell, _policy, spirit.ServerId))
            {
                Destroy(obj);
                return false;
            }
            _wanderers.Add(wanderer.WalkerId, wanderer);
            SpiritPlaced?.Invoke(wanderer);
            return true;
        }

        private void UpdateFacing(HousingPlacement placement)
        {
            var obj = _objects[placement.Id];
            if (!_definitions[placement.Id].VisualPrefab) _visuals.SetFacing(obj.transform, placement.QuarterTurns);
            else foreach (var component in obj.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IHousingRotationView view) view.SetFacing(placement.QuarterTurns);
        }

        /// <summary>RT 좌표에서 가장 앞에 보이는 전시물 또는 도깨비의 배치 인스턴스 ID. 없으면 0.</summary>
        public int ItemAtTexturePoint(Vector2 texturePoint)
        {
            var ray = SceneCamera.ScreenPointToRay(texturePoint);
            int id = 0;
            int frontOrder = int.MinValue;
            foreach (var pair in _objects)
                if (Hits(ray, pair.Value, ref frontOrder)) id = pair.Key;
            // 같은 깊이면 도깨비가 앞이다.
            foreach (var pair in _wanderers)
                if (pair.Value && Hits(ray, pair.Value.gameObject, ref frontOrder)) id = pair.Key;
            return id;
        }

        private bool Hits(Ray ray, GameObject obj, ref int frontOrder)
        {
            if (!obj.activeInHierarchy) return false;
            var plane = new Plane(Vector3.forward, obj.transform.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            var hitArea = obj.GetComponent<BoxCollider2D>();
            Vector2 local = obj.transform.InverseTransformPoint(ray.GetPoint(distance));
            var delta = local - hitArea.offset;
            if (Mathf.Abs(delta.x) > hitArea.size.x * .5f || Mathf.Abs(delta.y) > hitArea.size.y * .5f) return false;
            int order = DepthOrder(obj);
            if (order < frontOrder) return false;
            frontOrder = order;
            return true;
        }

        public void ExecuteAction(int id, HousingAction action)
        {
            if (action == HousingAction.Cancel) { ResetSelection(); return; }
            if (!CanEdit) return;
            if (_selectionController) _selectionController.Clear();
            if (action == HousingAction.Place) { RequestPlacement(); return; }
            if (_wanderers.TryGetValue(id, out var wanderer)) { ExecuteSpiritAction(id, wanderer, action); return; }
            if (!_objects.ContainsKey(id)) return;
            _selectedId = id; _placing = false;
            switch (action)
            {
                case HousingAction.Move:
                    RefreshSelection("손을 떼고 이동할 빈칸을 터치하세요.");
                    break;
                case HousingAction.Recall:
                    RemoveSelected();
                    break;
                case HousingAction.Rotate:
                    if (!Layout.TryRotate(id)) { ResetSelection(); Menu.SetMessage("이곳에서는 회전할 수 없습니다."); break; }
                    var placement = Layout.GetPlacement(id);
                    UpdateFacing(placement);
                    ResetSelection();
                    Menu.SetMessage($"전시물 방향: {placement.QuarterTurns * 90}°");
                    break;
            }
        }

        private void ExecuteSpiritAction(int id, DokkaebiWanderer wanderer, HousingAction action)
        {
            switch (action)
            {
                case HousingAction.Move:
                    ResetSelection();
                    _selectedId = id;
                    // 옮길 자리를 고르는 동안 제자리에 세워 둔다.
                    wanderer.SetPaused(true);
                    RefreshSelection("손을 떼고 옮길 빈칸을 터치하세요.");
                    break;
                case HousingAction.Recall:
                    _selectedId = id;
                    RemoveSelected();
                    break;
            }
        }

        public void RequestPlacement()
        {
            if (!CanEdit) return;
            if (_selectionController) _selectionController.Clear();
            ResetSelection();
            if (PlacementRequested != null) PlacementRequested.Invoke();
            else if (Picker) OpenPicker();
            else BeginPlacement(_defaultItem);
        }

        public bool BeginPlacement(HousingItemDefinition definition)
        {
            if (!CanEdit || !definition || !definition.IsValid) return false;
            if (_selectionController) _selectionController.Clear();
            ClosePicker();
            ResetSelection();
            _pendingItem = definition;
            _placing = true;
            RefreshSelection("빈칸을 터치해 전시물을 배치하세요.");
            return true;
        }

        /// <summary>보유 객체를 놓을 빈칸 터치를 기다린다. 지금은 도깨비만 받는다(건물은 도깨비 작업 뒤에 연결한다).</summary>
        /// <returns>편집할 수 없거나, 도깨비가 아니거나, 이미 놓인 정령이면 false.</returns>
        public bool BeginPlacement(ExhibitionPlaceable placeable)
        {
            if (!CanEdit || placeable == null || !placeable.IsSpirit || IsSpiritPlaced(placeable.ServerId)) return false;
            if (_selectionController) _selectionController.Clear();
            ClosePicker();
            ResetSelection();
            _pendingPlaceable = placeable;
            _placing = true;
            RefreshSelection($"빈칸을 터치해 {placeable.DisplayName} 도깨비를 놓으세요.");
            return true;
        }

        /// <summary>놓을 대상을 고르는 목록을 띄우고 보유 목록을 다시 읽는다.</summary>
        private void OpenPicker()
        {
            Picker.Show(PlacementOptions(), PickerMessage());
            Inventory.Refresh();
        }

        private void ClosePicker()
        {
            if (Picker) Picker.Hide();
        }

        /// <summary>지금 놓을 수 있는 대상. 아직 놓지 않은 보유 도깨비와 임시 전시물이다.</summary>
        public List<HousingPlacementOption> PlacementOptions()
        {
            var options = new List<HousingPlacementOption>();
            if (Inventory)
                foreach (var item in Inventory.Items)
                    if (item.IsSpirit && !IsSpiritPlaced(item.ServerId))
                        options.Add(new HousingPlacementOption(item.Key, item.DisplayName));
            if (_defaultItem && _defaultItem.IsValid)
                options.Add(new HousingPlacementOption(ItemOptionPrefix + _defaultItem.ItemId, _defaultItem.DisplayName));
            return options;
        }

        private string PickerMessage()
        {
            if (!Inventory) return null;
            if (Inventory.Loading) return "도깨비 목록을 불러오는 중입니다.";
            if (Inventory.Error != null) return "도깨비 목록을 불러오지 못했습니다.";
            foreach (var item in Inventory.Items)
                if (item.IsSpirit && !IsSpiritPlaced(item.ServerId)) return null;
            return "놓을 수 있는 도깨비가 없습니다.";
        }

        private void OnInventoryChanged()
        {
            if (IsPicking) Picker.Show(PlacementOptions(), PickerMessage());
        }

        private void OnPickerChosen(string key)
        {
            if (key.StartsWith(ItemOptionPrefix, StringComparison.Ordinal)) { BeginPlacement(_defaultItem); return; }
            var placeable = Inventory ? Inventory.Find(key) : null;
            if (!BeginPlacement(placeable)) OnInventoryChanged();
        }

        public void SelectCell(Vector2Int cell)
        {
            if (!CanEdit || !_surface.Grid.Contains(cell)) return;
            if (_placing)
            {
                bool added = _pendingPlaceable != null ? TryAddSpirit(_pendingPlaceable, cell) : TryAdd(_pendingItem, cell);
                if (added) ResetSelection();
                else RefreshSelection("다른 빈칸을 선택하세요.");
                return;
            }
            if (_wanderers.TryGetValue(_selectedId, out var wanderer))
            {
                if (wanderer.TryMoveAnchor(cell)) ResetSelection();
                else RefreshSelection("이곳에는 옮길 수 없습니다.");
                return;
            }
            var item = Layout.At(cell);
            if (item != null)
            {
                RefreshSelection("전시물을 꾹 눌러 편집하세요.");
            }
            else if (_selectedId != 0)
            {
                if (Layout.TryMove(_selectedId, cell))
                {
                    _objects[_selectedId].transform.position = _surface.CellWorldPosition(cell);
                    ResetSelection();
                }
                else RefreshSelection("이곳에는 이동할 수 없습니다.");
            }
        }

        public void RemoveSelected()
        {
            if (!CanEdit) return;
            if (_selectionController) _selectionController.Clear();
            if (_wanderers.TryGetValue(_selectedId, out var wanderer))
            {
                int relicId = wanderer.RelicId;
                Layout.RemoveWalker(_selectedId);
                _wanderers.Remove(_selectedId);
                Destroy(wanderer.gameObject);
                ResetSelection();
                SpiritRecalled?.Invoke(relicId);
                return;
            }
            var removed = Layout.GetPlacement(_selectedId);
            if (!Layout.Remove(_selectedId)) return;
            Destroy(_objects[_selectedId]);
            _objects.Remove(_selectedId);
            _definitions.Remove(_selectedId);
            ResetSelection();
            PlacementRemoved?.Invoke(removed);
        }

        public void ResetSelection()
        {
            // 옮길 자리를 고르느라 세워 둔 도깨비를 다시 걷게 한다.
            if (_wanderers.TryGetValue(_selectedId, out var wanderer) && wanderer) wanderer.SetPaused(false);
            _selectedId = 0; _placing = false;
            _pendingItem = null; _pendingPlaceable = null;
            RefreshSelection("전시물을 꾹 눌러 편집하세요.");
        }

        private void RefreshSelection(string message)
        {
            if (Menu) Menu.SetMessage(message);
            foreach (var pair in _editingAlphas)
            {
                if (!pair.Key) continue;
                var color = pair.Key.color; color.a = pair.Value; pair.Key.color = color;
            }
            _editingAlphas.Clear();
            GameObject selected = null;
            if (_selectedId != 0 && !_objects.TryGetValue(_selectedId, out selected) &&
                _wanderers.TryGetValue(_selectedId, out var wanderer) && wanderer) selected = wanderer.gameObject;
            if (!selected) return;
            foreach (var renderer in selected.GetComponentsInChildren<SpriteRenderer>())
            {
                var color = renderer.color; _editingAlphas.Add(renderer, color.a);
                color.a *= .6f; renderer.color = color;
            }
        }

        /// <summary>RT 좌표의 적중 결과를 화면 공용 선택 대상으로 변환합니다.</summary>
        public void SelectAtTexturePoint(Vector2 texturePoint)
        {
            // 목록이 떠 있을 때 목록 밖을 누르면 목록만 닫는다.
            if (IsPicking) { ClosePicker(); return; }
            if (IsEditingCell || !_selectionController || !_selectionController.CanSelect) return;
            int id = ItemAtTexturePoint(texturePoint);
            if (_wanderers.TryGetValue(id, out var wanderer))
                _selectionController.Select(new ExhibitionSelectionTarget("home-npc:" + wanderer.RelicId, wanderer.transform));
            else if (id != 0)
                _selectionController.Select(new ExhibitionSelectionTarget("housing:" + id, _objects[id].transform));
            else _selectionController.Clear();
        }

        private int DepthOrder(GameObject obj) => ExhibitionDepth.OrderFor(_surface.transform.InverseTransformPoint(obj.transform.position).y);

        private void CreateControls()
        {
            var canvas = new GameObject("Housing Controls", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            var ui = canvas.GetComponent<Canvas>();
            ui.renderMode = RenderMode.ScreenSpaceCamera; ui.worldCamera = SceneCamera;
            ui.planeDistance = 1; ui.sortingOrder = 3000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.matchWidthOrHeight = .5f;
            Menu = gameObject.AddComponent<HousingRadialMenu>();
            Menu.Configure((RectTransform)canvas.transform, SceneCamera, _font);
            HideHoldMenu();
            Picker = gameObject.AddComponent<HousingPlacementPicker>();
            Picker.Configure((RectTransform)canvas.transform, _font, _root.RenderLayer);
            Picker.Chosen += OnPickerChosen;
            Picker.Cancelled += ClosePicker;
            MirrorSceneRoot.SetLayer(canvas.transform, _root.RenderLayer);
            // Configure may have run before Start. Register the new raycaster either way.
            _root.uiRaycasters = GetComponentsInChildren<GraphicRaycaster>(true);
            foreach (var raycaster in _root.uiRaycasters) raycaster.enabled = false;
        }

        private void LateUpdate()
        {
            if (!CanEdit && (IsEditingCell || IsPicking)) { ResetSelection(); HideHoldMenu(); ClosePicker(); }
            // The platform's intro animates its opacity separately from its transform.
            if (_content) _content.gameObject.SetActive(GetComponent<ExhibitionIntro>().IsComplete);
        }

        private void OnDisable()
        {
            if (_selectionController) _selectionController.Clear();
            ClosePicker();
            if (Menu) ResetSelection();
            HideHoldMenu();
        }

        private void OnDestroy()
        {
            if (Inventory) Inventory.Changed -= OnInventoryChanged;
            if (Picker) { Picker.Chosen -= OnPickerChosen; Picker.Cancelled -= ClosePicker; }
            _visuals?.Dispose();
        }
    }
}
