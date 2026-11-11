using System;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        
        private SpriteRenderer _renderer;
        private float _lastAspect;
        private float _lastOrthographicSize;
        private Vector3 _lastCameraPosition;
        private Sprite _fallbackSprite;
        private Color _baseColor = Color.white;   // 매개체가 정한 색
        private Color _tint = Color.white;        // 페이즈가 정한 밝기. 두 색을 곱해 칠한다

        [Tooltip("배경을 원래보다 밝게 할 때 위에 덮는 색. 알파는 페이즈가 정한다.")]
        [SerializeField] private Color _liftColor = new Color(1f, 0.98f, 0.92f, 1f);
        private SpriteRenderer _liftRenderer;     // 곱하기로는 원래보다 밝게 못 하므로, 옅은 색 판을 한 장 더 덮는다
        private float _lift;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _fallbackSprite = _renderer.sprite;
            CreateLiftRenderer();
            
            if (_camera == null)
            {
                Debug.LogError($"{nameof(BackgroundView)}: _camera가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (!_camera.orthographic)
            {
                Debug.LogError($"{nameof(BackgroundView)}: camera가 직교 카메라가 아닙니다.", this);
                enabled = false;
            }
        }

        public void Apply(SealMediumData skin)
        {
            if (skin == null)
            {
                throw new ArgumentNullException(nameof(skin));
            }

            if (skin.BackgroundSprite != null)
            {
                _renderer.sprite = skin.BackgroundSprite;
                _renderer.drawMode = SpriteDrawMode.Tiled;
                _baseColor = Color.white;
            }
            else
            {
                _renderer.sprite = _fallbackSprite;
                _renderer.drawMode = SpriteDrawMode.Simple;
                _baseColor = skin.BackgroundColor;
            }

            _renderer.color = _baseColor * _tint;
            FitToCamera();
        }

        /// <summary>
        /// 페이즈에 맞춰 배경 밝기를 정한다.
        /// </summary>
        /// <param name="tint">배경에 곱할 색. 흰색이면 원래 색 그대로이고, 어두울수록 배경이 어두워진다.</param>
        /// <param name="lift">원래보다 밝게 할 정도(0~1). 옅은 색 판을 이만큼의 불투명도로 위에 덮는다.</param>
        public void SetTone(Color tint, float lift)
        {
            _tint = tint;
            _lift = Mathf.Clamp01(lift);
            _renderer.color = _baseColor * _tint;
            RefreshLiftColor();
        }

        private void CreateLiftRenderer()
        {
            if (_fallbackSprite == null) return;

            // 배경과 스케일이 섞이지 않도록 자식이 아닌 별도 오브젝트로 두고, 매번 화면 크기에 맞춘다
            var go = new GameObject("BackgroundLift");
            _liftRenderer = go.AddComponent<SpriteRenderer>();
            _liftRenderer.sprite = _fallbackSprite;
            _liftRenderer.sortingLayerID = _renderer.sortingLayerID;
            _liftRenderer.sortingOrder = _renderer.sortingOrder + 1;
            RefreshLiftColor();
        }

        private void RefreshLiftColor()
        {
            if (_liftRenderer == null) return;

            Color color = _liftColor;
            color.a = _lift;
            _liftRenderer.color = color;
            _liftRenderer.enabled = _lift > 0f;
        }

        private void FitLift(float viewWidth, float viewHeight)
        {
            if (_liftRenderer == null) return;

            Vector2 spriteSize = _liftRenderer.sprite.bounds.size;
            Transform lift = _liftRenderer.transform;
            lift.localScale = new Vector3(viewWidth / spriteSize.x, viewHeight / spriteSize.y, 1f);
            lift.position = new Vector3(_camera.transform.position.x, _camera.transform.position.y, transform.position.z);
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(_lastAspect, _camera.aspect) 
                || !Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize) 
                || _lastCameraPosition != _camera.transform.position)
            {
                FitToCamera();
            }
        }

        private void FitToCamera()
        {
            if (_camera == null || _renderer.sprite == null) return;
            
            // 1. 화면 크기 (월드 단위)
            float viewHeight = _camera.orthographicSize * 2f;
            float viewWidth = viewHeight * _camera.aspect;
            FitLift(viewWidth, viewHeight);
            
            Vector3 position = transform.position;

            if (_renderer.drawMode == SpriteDrawMode.Tiled)
            {
                transform.localScale = Vector3.one;
                _renderer.size = new Vector2(viewWidth, viewHeight);
                position.x = _camera.transform.position.x;
                position.y = _camera.transform.position.y;
                transform.position = position;
                
                RememberCameraState();
                return;
            }
            
            // 2. 스프라이트의 원래 크기
            Vector2 spriteSize = _renderer.sprite.bounds.size;
            
            // 3. 가로로 몇 배, 세로로 몇 배 키워야 화면과 같아지는지 계산
            float scaleX = viewWidth / spriteSize.x;
            float scaleY = viewHeight / spriteSize.y;
            
            // 4. 둘 중 큰 쪽이 최종 배율
            float scale = Mathf.Max(scaleX, scaleY);
            
            // 5. 적용
            transform.localScale = new Vector3(scale, scale, 1f);
            
            Vector3 offset = _renderer.sprite.bounds.center * scale;
            position.x = _camera.transform.position.x - offset.x;
            position.y = _camera.transform.position.y - offset.y;
            transform.position = position;
            
            // 6. 필드 값 갱신
            RememberCameraState();
        }

        private void RememberCameraState()
        {
            _lastAspect = _camera.aspect;
            _lastOrthographicSize = _camera.orthographicSize;
            _lastCameraPosition = _camera.transform.position;
        }
    }
}