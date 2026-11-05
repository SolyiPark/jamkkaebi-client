using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>화면마다 독립된 선택 수명·강조 복원과 카메라 요청을 관리합니다.</summary>
    public sealed class ExhibitionSelectionController : MonoBehaviour
    {
        [SerializeField] private ExhibitionBehaviorPolicy _policy;
        [SerializeField] private ExhibitionCameraController _cameraController;
        [SerializeField, Range(0, 1)] private float _selectedOpacity = .75f;
        private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();
        private float[] _alphas = Array.Empty<float>();
        public ExhibitionBehaviorPolicy Policy => _policy;
        public ExhibitionSelectionTarget Current { get; private set; }
        public bool CanSelect => isActiveAndEnabled && (!_policy || _policy.AllowObjectSelection);
        public event Action<ExhibitionSelectionTarget> SelectionChanged;

        public void Configure(ExhibitionBehaviorPolicy policy, ExhibitionCameraController cameraController)
        {
            if (_policy != policy || _cameraController != cameraController) Clear();
            _policy = policy; _cameraController = cameraController;
        }

        /// <summary>같은 표현 대상을 다시 누르면 해제합니다. 적중 판정은 화면 어댑터가 소유합니다.</summary>
        public bool Select(ExhibitionSelectionTarget target)
        {
            if (!CanSelect) { Clear(); return false; }
            if (target == null || !target.IsAlive || (Current != null && Current.Root == target.Root))
            { Clear(); return false; }
            RestoreHighlight();
            Current = target;
            _renderers = target.Root.GetComponentsInChildren<SpriteRenderer>(true);
            _alphas = new float[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var color = _renderers[i].color; _alphas[i] = color.a;
                color.a *= _selectedOpacity; _renderers[i].color = color;
            }
            UpdateFocus();
            SelectionChanged?.Invoke(Current);
            return true;
        }

        public void Clear()
        {
            bool hadSelection = Current != null;
            RestoreHighlight(); Current = null;
            if (_cameraController) _cameraController.ClearFocus();
            if (hadSelection) SelectionChanged?.Invoke(null);
        }

        private void RestoreHighlight()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!_renderers[i]) continue;
                var color = _renderers[i].color; color.a = _alphas[i]; _renderers[i].color = color;
            }
            _renderers = Array.Empty<SpriteRenderer>(); _alphas = Array.Empty<float>();
        }

        private void UpdateFocus()
        {
            if (!_cameraController) return;
            if (Current != null && Current.IsAlive && _policy && _policy.EnableSelectionFocus)
                _cameraController.SetFocus(Current.Root);
            else _cameraController.ClearFocus();
        }

        private void Update()
        {
            if (Current == null) return;
            if (!CanSelect || !Current.IsAlive) Clear();
            else UpdateFocus();
        }
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
    }
}
