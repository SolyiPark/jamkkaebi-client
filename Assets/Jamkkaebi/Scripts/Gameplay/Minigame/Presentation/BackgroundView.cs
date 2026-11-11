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

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _fallbackSprite = _renderer.sprite;
            
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
                _renderer.color = Color.white;
            }
            else
            {
                _renderer.sprite = _fallbackSprite;
                _renderer.drawMode = SpriteDrawMode.Simple;
                _renderer.color = skin.BackgroundColor;
            }
            
            FitToCamera();
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