using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 보드 뒤에 깔리는 배경.
    /// </summary>
    /// <remarks>
    /// 매개체의 봉인된(어두운) 배경 위에 깨어난(밝은) 배경을 같은 위치·크기로 한 장 더 겹치고,
    /// 페이즈가 정한 비율(<see cref="SetTone"/>의 awakening)만큼 투명도를 올려 두 배경을 섞는다.
    /// 두 이미지는 크기와 Pixels Per Unit이 같아 무늬가 정확히 겹친다.
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundView : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        
        private SpriteRenderer _renderer;
        private SpriteRenderer _awakenedRenderer;
        private Sprite _fallbackSprite;
        private Color _baseColor = Color.white;   // 매개체가 정한 색
        private Color _tint = Color.white;        // 페이즈가 정한 밝기. 두 색을 곱해 칠한다
        private float _awakening;                 // 깨어난 배경을 섞는 비율(0~1)
        private float _lastAspect;
        private float _lastOrthographicSize;
        private Vector3 _lastCameraPosition;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _fallbackSprite = _renderer.sprite;
            CreateAwakenedRenderer();
            
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
                _awakenedRenderer.sprite = skin.AwakenedBackgroundSprite;
            }
            else
            {
                // 패턴이 없는 매개체는 색으로만 칠하므로 섞을 깨어난 배경도 없다
                _renderer.sprite = _fallbackSprite;
                _renderer.drawMode = SpriteDrawMode.Simple;
                _baseColor = skin.BackgroundColor;
                _awakenedRenderer.sprite = null;
            }

            RefreshColors();
            FitToCamera();
        }

        /// <summary>
        /// 페이즈에 맞춰 배경 밝기를 정한다.
        /// </summary>
        /// <param name="tint">봉인된 배경에 곱할 색. 흰색이면 원래 색 그대로이고, 어두울수록 배경이 어두워진다.</param>
        /// <param name="awakening">깨어난(밝은) 배경을 섞는 비율. 0이면 봉인된 배경만, 1이면 깨어난 배경만 보인다.</param>
        public void SetTone(Color tint, float awakening)
        {
            _tint = tint;
            _awakening = Mathf.Clamp01(awakening);
            RefreshColors();
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

        private void CreateAwakenedRenderer()
        {
            // 자식으로 두면 위치를 따로 맞출 필요가 없다. 타일 모드에서는 부모 스케일이 1이라 크기도 그대로 맞는다
            var go = new GameObject("AwakenedBackground");
            go.transform.SetParent(transform, false);
            _awakenedRenderer = go.AddComponent<SpriteRenderer>();
            _awakenedRenderer.drawMode = SpriteDrawMode.Tiled;
            _awakenedRenderer.sortingLayerID = _renderer.sortingLayerID;
            _awakenedRenderer.sortingOrder = _renderer.sortingOrder + 1;
        }

        private void RefreshColors()
        {
            _renderer.color = _baseColor * _tint;
            _awakenedRenderer.color = new Color(1f, 1f, 1f, _awakening);
            _awakenedRenderer.enabled = _awakening > 0f && _awakenedRenderer.sprite != null;
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
                _awakenedRenderer.size = _renderer.size;
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
