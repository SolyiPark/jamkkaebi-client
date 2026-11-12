using System;
using System.Collections;
using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>친구 전시 조회와 방문 전용 표현물·이동·입력 잠금의 수명을 소유합니다.</summary>
    public sealed class FriendExhibitionRebuilder : MonoBehaviour
    {
        [SerializeField] private MirrorSceneRoot _root;
        [SerializeField] private GameObject _stage;
        [SerializeField] private ExhibitionSurface _surface;
        [SerializeField] private Transform _content;
        [SerializeField] private ExhibitionLayer[] _layers;
        [SerializeField] private ExhibitionVisualCatalog _catalog;
        [SerializeField] private Material _material;
        [SerializeField] private ExhibitionCameraController _cameraController;
        [SerializeField] private ExhibitionBehaviorPolicy _policy;
        [SerializeField] private ExhibitionSelectionController _selectionController;
        private ExhibitionSelectionController _subscribedSelection;
        private bool _navigationWasLocked;
        private readonly Dictionary<VisitObject, GameObject> _views = new Dictionary<VisitObject, GameObject>();
        private readonly Dictionary<VisitObject, Vector3> _starts = new Dictionary<VisitObject, Vector3>();
        private readonly Dictionary<VisitObject, Animator[]> _animators = new Dictionary<VisitObject, Animator[]>();
        private Coroutine _request;
        private ExhibitionDummyVisuals _dummy;
        private bool _visiting;
        private bool _moving;
        private float _moveStarted;
        private float _nextWalk;
        private int _walkStep;
        private int _requestGeneration;
        private const float WalkDuration = .85f;
        private const float WalkPause = .7f;
        public ExhibitionVisitSession Session { get; } = new ExhibitionVisitSession();
        public ExhibitionSurface Surface => _surface;
        public ExhibitionCameraController CameraController => _cameraController;
        public ExhibitionSelectionController SelectionController => _selectionController;
        public ExhibitionBehaviorPolicy Policy => _policy;
        public bool StageActive => _stage && _stage.activeSelf;
        public IReadOnlyDictionary<VisitObject, GameObject> Views => _views;
        public event Action Changed;
        public event Action<VisitObject> ObjectSelected;

        /// <summary>일회성 씬 이관에서 방문 스테이지와 공용 표시 자산을 연결합니다.</summary>
        public void Configure(MirrorSceneRoot root, GameObject stage, ExhibitionSurface surface,
            Transform content, ExhibitionLayer[] layers, ExhibitionVisualCatalog catalog, Material material)
        {
            _root = root; _stage = stage; _surface = surface; _content = content;
            _layers = layers; _catalog = catalog; _material = material;
        }

        /// <summary>이전 요청을 무효화하고 새 방문을 로드합니다. 오류일 때도 나가기 전까지 잠금을 유지합니다.</summary>
        public void Visit(string code, IExhibitionBackend backend)
        {
            if (!_cameraController) _cameraController = GetComponent<ExhibitionCameraController>();
            if (!_cameraController) _cameraController = gameObject.AddComponent<ExhibitionCameraController>();
            _cameraController.Configure(_root.sceneCamera);
            if (!_selectionController) _selectionController = GetComponent<ExhibitionSelectionController>();
            if (!_selectionController) _selectionController = gameObject.AddComponent<ExhibitionSelectionController>();
            _selectionController.Configure(_policy, _cameraController);
            if (_subscribedSelection != _selectionController)
            {
                if (_subscribedSelection) _subscribedSelection.SelectionChanged -= OnSelectionChanged;
                _subscribedSelection = _selectionController;
                _subscribedSelection.SelectionChanged += OnSelectionChanged;
            }
            _selectionController.Clear();
            if (_request != null) StopCoroutine(_request);
            if (!_visiting)
            {
                _cameraController.CaptureView();
                _navigationWasLocked = _root.NavigationLocked;
            }
            _cameraController.ResetView();
            _visiting = true; _root.SetNavigationLocked(_navigationWasLocked || !_policy || _policy.LockNavigationDuringVisit);
            int revision = Session.Begin(code);
            if (Session.Scene == null) ClearViews();
            Changed?.Invoke();
            _request = StartCoroutine(Load(code, backend, revision, ++_requestGeneration));
        }

        private IEnumerator Load(string code, IExhibitionBackend backend, int revision, int generation)
        {
            yield return backend.GetExhibition(code, snapshot =>
            {
                if (generation != _requestGeneration) return;
                if (Session.Complete(revision, snapshot, _catalog.IsKnown)) { ClearViews(); BuildViews(); }
                Changed?.Invoke();
            }, error => { if (generation != _requestGeneration) return; Session.Fail(revision, error); Changed?.Invoke(); });
            if (generation == _requestGeneration) _request = null;
        }

        private void BuildViews()
        {
            _dummy ??= new ExhibitionDummyVisuals(_material);
            _stage.SetActive(true);
            _walkStep = 0; _moving = false; _nextWalk = Time.unscaledTime + WalkPause;
            foreach (var obj in Session.Scene.Objects)
            {
                var view = new GameObject("Visit " + obj.Placement.Key);
                view.transform.SetParent(_content, false);
                view.transform.position = _surface.CellWorldPosition(obj.Cell);
                view.AddComponent<ExhibitionDepth>().Configure(_surface, view.transform);
                var visual = new GameObject("Visual"); visual.transform.SetParent(view.transform, false);
                if (_catalog.TryGet(obj.Placement.IsBuilding, obj.Placement.Id, out var definition) && definition.Prefab)
                {
                    var instance = Instantiate(definition.Prefab, visual.transform, false);
                    instance.transform.localPosition = definition.FootOffset;
                    instance.transform.localScale = definition.Scale;
                    foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>()) renderer.sharedMaterial = _material;
                }
                else if (obj.Placement.IsBuilding) _dummy.ExhibitionStand(visual.transform);
                else _dummy.Dokkaebi(visual.transform);
                if (obj.Unknown)
                    foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>()) renderer.color = new Color(.85f, .65f, .35f);
                MirrorSceneRoot.SetLayer(view.transform, _root.gameObject.layer);
                _views.Add(obj, view);
                _animators.Add(obj, view.GetComponentsInChildren<Animator>());
            }
        }

        private void LateUpdate()
        {
            if (!_visiting || !_stage.activeSelf) return;
            UpdateMovement();
            _cameraController.Apply();
            foreach (var layer in _layers) layer.Apply(100, 0, _root.sceneCamera);
        }

        private void UpdateMovement()
        {
            if (_policy && !_policy.EnableAmbientMovement)
            {
                _moveStarted += Time.unscaledDeltaTime; _nextWalk += Time.unscaledDeltaTime;
                foreach (var animators in _animators.Values)
                    foreach (var animator in animators) animator.SetBool("IsMoving", false);
                return;
            }
            if (!_moving && Time.unscaledTime >= _nextWalk && Session.Scene != null && !Session.Loading)
            {
                _starts.Clear();
                foreach (var pair in _views) _starts.Add(pair.Key, pair.Value.transform.localPosition);
                Session.Scene.Walk(_walkStep++);
                _moving = true; _moveStarted = Time.unscaledTime;
            }
            if (!_moving) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _moveStarted) / WalkDuration);
            foreach (var pair in _views)
            {
                Vector3 destination = _content.InverseTransformPoint(_surface.CellWorldPosition(pair.Key.Cell));
                bool walking = (_starts[pair.Key] - destination).sqrMagnitude > .000001f && t < 1;
                pair.Value.transform.localPosition = Vector3.Lerp(_starts[pair.Key], destination, t);
                foreach (var animator in _animators[pair.Key]) animator.SetBool("IsMoving", walking);
            }
            if (t >= 1) { _moving = false; _nextWalk = Time.unscaledTime + WalkPause; }
        }

        /// <summary>이미 RT 픽셀로 변환된 좌표로 앞쪽 표현물을 선택합니다.</summary>
        public bool SelectAtTexturePoint(Vector2 texturePoint)
        {
            if (!_visiting || Session.Loading || !_stage.activeSelf || !_selectionController || !_selectionController.CanSelect) return false;
            var ray = _root.sceneCamera.ScreenPointToRay(texturePoint);
            var plane = new Plane(_surface.transform.forward, _surface.transform.position);
            if (!plane.Raycast(ray, out float distance)) { _selectionController.Clear(); return false; }
            Vector3 point = _content.InverseTransformPoint(ray.GetPoint(distance));
            VisitObject selected = null; int best = int.MinValue;
            foreach (var pair in _views)
            {
                var rect = _catalog.TryGet(pair.Key.Placement.IsBuilding, pair.Key.Placement.Id, out var definition)
                    ? definition.TouchRect : new Rect(-.18f, 0, .36f, .48f);
                if (!rect.Contains((Vector2)(point - pair.Value.transform.localPosition))) continue;
                int order = ExhibitionDepth.OrderFor(pair.Value.transform.localPosition.y);
                if (order >= best) { best = order; selected = pair.Key; }
            }
            if (selected == null) { _selectionController.Clear(); return false; }
            return _selectionController.Select(new ExhibitionSelectionTarget(selected.Placement.Key, _views[selected].transform));
        }

        private void OnSelectionChanged(ExhibitionSelectionTarget target)
        {
            VisitObject selected = null;
            if (target != null)
                foreach (var pair in _views)
                    if (pair.Value && pair.Value.transform == target.Root) { selected = pair.Key; break; }
            ObjectSelected?.Invoke(selected);
        }

        /// <summary>진행 중인 응답을 무효화하고 방문 객체·카메라·입력을 복원합니다.</summary>
        public void Leave()
        {
            ++_requestGeneration;
            if (_request != null) StopCoroutine(_request);
            _request = null; Session.Leave();
            ClearViews();
            if (_root)
            {
                if (_visiting) _root.SetNavigationLocked(_navigationWasLocked);
                if (_visiting && _root.sceneCamera)
                { _cameraController.RestoreView(); }
            }
            _visiting = false;
        }

        private void ClearViews()
        {
            if (_selectionController) _selectionController.Clear();
            foreach (var view in _views.Values) if (view) { view.SetActive(false); Destroy(view); }
            _views.Clear(); _starts.Clear(); _animators.Clear(); _moving = false;
            if (_stage) _stage.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_subscribedSelection) _subscribedSelection.SelectionChanged -= OnSelectionChanged;
            Leave(); _dummy?.Dispose();
        }
    }
}
