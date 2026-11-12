using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    public sealed class ExhibitionHousing : MonoBehaviour
    {
        [SerializeField] private ExhibitionSurface _surface;
        [SerializeField] private Transform _content;
        [SerializeField] private Font _font;
        [SerializeField] private Material _material;
        [SerializeField] private HousingItemDefinition _defaultItem;
        [SerializeField] private Vector2Int _initialCell = new Vector2Int(2, 2);
        [SerializeField] private bool _spawnInitialItem = true;
        [SerializeField] private ExhibitionBehaviorPolicy _policy;
        [SerializeField] private ExhibitionSelectionController _selectionController;
        private MirrorSceneRoot _root;
        private ExhibitionDummyVisuals _visuals;
        private readonly Dictionary<int, GameObject> _objects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, HousingItemDefinition> _definitions = new Dictionary<int, HousingItemDefinition>();
        private HousingItemDefinition _pendingItem;
        private int _selectedId;
        private bool _placing;
        private readonly Dictionary<SpriteRenderer, float> _editingAlphas = new Dictionary<SpriteRenderer, float>();
        public ExhibitionSurface Surface => _surface;
        public Camera SceneCamera => _root.sceneCamera;
        public HousingLayout Layout { get; private set; }
        public DokkaebiWanderer Wanderer { get; private set; }
        public HousingRadialMenu Menu { get; private set; }
        public bool IsPlacing => _placing;
        public bool IsEditingCell => _placing || _selectedId != 0;
        public bool CanEdit => !_policy || _policy.AllowLayoutEditing;
        public ExhibitionBehaviorPolicy BehaviorPolicy => _policy;
        public ExhibitionSelectionController SelectionController => _selectionController;
        public HousingItemDefinition DefaultItem => _defaultItem;
        // A catalog/inventory bridge chooses an item and then calls BeginPlacement(definition).
        public event Action PlacementRequested;
        public event Action<HousingPlacement> PlacementAdded;
        public event Action<HousingPlacement> PlacementRemoved;

        public void ShowHoldMenu(Vector2 texturePoint, int itemId)
        {
            if (!CanEdit) return;
            if (_selectionController) _selectionController.Clear();
            bool floorMenu = itemId == 0;
            Menu.SetMessage(floorMenu ? "배치·취소 방향으로 밀어주세요." : "원하는 버튼 방향으로 밀어 선택하세요.");
            Menu.Show(texturePoint, floorMenu);
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
            CreateControls();
            if (_spawnInitialItem) TryAdd(_defaultItem, _initialCell);
            SpawnDokkaebi();
            ResetSelection();
        }

        private GameObject ContentObject(string name)
        {
            var obj = new GameObject(name);
            _root.RegisterSpawnedObject(obj);
            obj.transform.SetParent(_content, false);
            obj.AddComponent<ExhibitionDepth>().Configure(_surface, obj.transform);
            return obj;
        }

        private void SpawnDokkaebi()
        {
            for (int x = 0; x < _surface.Grid.Size.x; x++)
                for (int y = 0; y < _surface.Grid.Size.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!Layout.IsWalkable(cell)) continue;
                    var obj = ContentObject("Dokkaebi Dummy");
                    _visuals.Dokkaebi(obj.transform);
                    var hitArea = obj.AddComponent<BoxCollider2D>();
                    hitArea.size = new Vector2(.24f, .45f); hitArea.offset = new Vector2(0, .22f);
                    MirrorSceneRoot.SetLayer(obj.transform, _root.RenderLayer);
                    Wanderer = obj.AddComponent<DokkaebiWanderer>();
                    Wanderer.Configure(Layout, _surface, cell, _policy);
                    return;
                }
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

        private void UpdateFacing(HousingPlacement placement)
        {
            var obj = _objects[placement.Id];
            if (!_definitions[placement.Id].VisualPrefab) _visuals.SetFacing(obj.transform, placement.QuarterTurns);
            else foreach (var component in obj.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IHousingRotationView view) view.SetFacing(placement.QuarterTurns);
        }

        public int ItemAtTexturePoint(Vector2 texturePoint)
        {
            var ray = SceneCamera.ScreenPointToRay(texturePoint);
            int id = 0;
            int frontOrder = int.MinValue;
            foreach (var pair in _objects)
            {
                var obj = pair.Value;
                if (!obj.activeInHierarchy) continue;
                var plane = new Plane(Vector3.forward, obj.transform.position);
                if (!plane.Raycast(ray, out float distance)) continue;
                var hitArea = obj.GetComponent<BoxCollider2D>();
                Vector2 local = obj.transform.InverseTransformPoint(ray.GetPoint(distance));
                var delta = local - hitArea.offset;
                if (Mathf.Abs(delta.x) > hitArea.size.x * .5f || Mathf.Abs(delta.y) > hitArea.size.y * .5f) continue;
                int order = ExhibitionDepth.OrderFor(_surface.transform.InverseTransformPoint(obj.transform.position).y);
                if (order < frontOrder) continue;
                frontOrder = order; id = pair.Key;
            }
            return id;
        }

        public void ExecuteAction(int id, HousingAction action)
        {
            if (action == HousingAction.Cancel) { ResetSelection(); return; }
            if (!CanEdit) return;
            if (_selectionController) _selectionController.Clear();
            if (action == HousingAction.Place) { RequestPlacement(); return; }
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

        public void RequestPlacement()
        {
            if (!CanEdit) return;
            if (_selectionController) _selectionController.Clear();
            ResetSelection();
            if (PlacementRequested != null) PlacementRequested.Invoke();
            else BeginPlacement(_defaultItem);
        }

        public bool BeginPlacement(HousingItemDefinition definition)
        {
            if (!CanEdit || !definition || !definition.IsValid) return false;
            if (_selectionController) _selectionController.Clear();
            _pendingItem = definition;
            _selectedId = 0; _placing = true;
            RefreshSelection("빈칸을 터치해 전시물을 배치하세요.");
            return true;
        }

        public void SelectCell(Vector2Int cell)
        {
            if (!CanEdit || !_surface.Grid.Contains(cell)) return;
            if (_placing)
            {
                if (TryAdd(_pendingItem, cell)) ResetSelection();
                else RefreshSelection("다른 빈칸을 선택하세요.");
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
            _selectedId = 0; _placing = false;
            _pendingItem = null;
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
            if (_selectedId == 0 || !_objects.TryGetValue(_selectedId, out var selected)) return;
            foreach (var renderer in selected.GetComponentsInChildren<SpriteRenderer>())
            {
                var color = renderer.color; _editingAlphas.Add(renderer, color.a);
                color.a *= .6f; renderer.color = color;
            }
        }

        /// <summary>RT 좌표의 적중 결과를 화면 공용 선택 대상으로 변환합니다.</summary>
        public void SelectAtTexturePoint(Vector2 texturePoint)
        {
            if (IsEditingCell || !_selectionController || !_selectionController.CanSelect) return;
            int itemId = ItemAtTexturePoint(texturePoint);
            GameObject selected = itemId == 0 ? null : _objects[itemId];
            string id = itemId == 0 ? null : "housing:" + itemId;
            if (Wanderer && Wanderer.gameObject.activeInHierarchy)
            {
                var obj = Wanderer.gameObject;
                var hitArea = obj.GetComponent<BoxCollider2D>();
                var ray = SceneCamera.ScreenPointToRay(texturePoint);
                var plane = new Plane(Vector3.forward, obj.transform.position);
                if (hitArea && plane.Raycast(ray, out float distance))
                {
                    Vector2 local = obj.transform.InverseTransformPoint(ray.GetPoint(distance));
                    Vector2 delta = local - hitArea.offset;
                    if (Mathf.Abs(delta.x) <= hitArea.size.x * .5f && Mathf.Abs(delta.y) <= hitArea.size.y * .5f &&
                        (!selected || DepthOrder(obj) >= DepthOrder(selected)))
                    { selected = obj; id = "home-npc:" + Wanderer.GetInstanceID(); }
                }
            }
            if (selected) _selectionController.Select(new ExhibitionSelectionTarget(id, selected.transform));
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
            MirrorSceneRoot.SetLayer(canvas.transform, _root.RenderLayer);
            // Configure may have run before Start. Register the new raycaster either way.
            _root.uiRaycasters = GetComponentsInChildren<GraphicRaycaster>(true);
            foreach (var raycaster in _root.uiRaycasters) raycaster.enabled = false;
        }

        private void LateUpdate()
        {
            if (!CanEdit && IsEditingCell) { ResetSelection(); HideHoldMenu(); }
            // The platform's intro animates its opacity separately from its transform.
            if (_content) _content.gameObject.SetActive(GetComponent<ExhibitionIntro>().IsComplete);
        }

        private void OnDisable()
        {
            if (_selectionController) _selectionController.Clear();
            if (Menu) ResetSelection();
            HideHoldMenu();
        }

        private void OnDestroy() => _visuals?.Dispose();
    }
}
