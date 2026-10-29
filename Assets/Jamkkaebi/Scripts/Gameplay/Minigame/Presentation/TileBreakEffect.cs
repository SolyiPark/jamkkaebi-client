using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 타일 덮개가 깨지면 덮개 그림을 조각내어 흩뿌린다.
    /// </summary>
    /// <remarks>
    /// 덮개 그림을 N×N으로 잘라 조각마다 SpriteRenderer 하나를 쓴다. 따로 그린 그림이 없어도 매개체마다 그 덮개가 깨져 보인다.
    /// 조각 오브젝트는 다 쓰면 끄고 다음에 다시 쓴다(풀링). 매번 만들고 지우면 가비지가 쌓여 프레임이 튈 수 있다.
    /// 움직임은 모두 타일 한 칸 크기(TileSize) 단위라 보드 크기가 달라져도 같은 느낌으로 보인다.
    /// </remarks>
    public class TileBreakEffect : MonoBehaviour
    {
        [SerializeField] private MinigameSessionAdapter _adapter;
        [Tooltip("조각을 놓을 부모. 타일과 같은 보드 컨테이너를 쓴다.")]
        [SerializeField] private Transform _parent;

        [Header("조각")]
        [Tooltip("덮개를 가로·세로 몇 조각으로 자를지.")]
        [SerializeField] [Range(2, 5)] private int _piecesPerSide = 3;
        [Tooltip("격자 선(10)보다 위에 그린다.")]
        [SerializeField] private int _sortingOrder = 20;
        [SerializeField] [Min(0.05f)] private float _lifetime = 0.45f;

        [Header("움직임 (타일 한 칸 = 1)")]
        [Tooltip("가운데에서 바깥쪽으로 튀어 나가는 속도 범위(칸/초).")]
        [SerializeField] private FloatRange _outwardSpeed = new FloatRange(1.5f, 3f);
        [Tooltip("처음에 위로 더해 주는 속도 범위(칸/초). 포물선을 그리며 떨어지게 한다.")]
        [SerializeField] private FloatRange _upwardSpeed = new FloatRange(1f, 2f);
        [SerializeField] private float _gravity = 9f;
        [Tooltip("조각 회전 속도의 최댓값(도/초).")]
        [SerializeField] private float _maxSpinSpeed = 540f;
        [Tooltip("사라질 때의 크기 비율.")]
        [SerializeField] [Range(0f, 1f)] private float _endScale = 0.6f;
        [Tooltip("수명 중 이 비율이 지나면 흐려지기 시작한다.")]
        [SerializeField] [Range(0f, 1f)] private float _fadeStart = 0.4f;

        [Header("도구·상황별")]
        [Tooltip("공격 파괴(망치)는 조각이 더 세게 튄다.")]
        [SerializeField] private float _attackSpeedMultiplier = 1.35f;
        [Tooltip("강화만 벗겨질 때 떨어져 나가는 조각 수.")]
        [SerializeField] [Min(1)] private int _reinforcementChipCount = 3;
        [Tooltip("강화만 벗겨질 때 조각의 크기·속도 비율.")]
        [SerializeField] [Range(0.1f, 1f)] private float _reinforcementChipScale = 0.6f;

        /// <summary>날아가는 조각 하나의 상태.</summary>
        private class Fragment
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public float SpinSpeed;
            public float Age;
            public Vector3 StartScale;
            public Color Color;
        }

        private readonly List<Fragment> _fragments = new List<Fragment>();
        // 덮개 그림마다 잘라 둔 조각 그림. 같은 매개체 덮개는 한 번만 자른다
        private readonly Dictionary<Sprite, Sprite[]> _pieceCache = new Dictionary<Sprite, Sprite[]>();

        private void OnEnable()
        {
            if (_adapter != null) _adapter.TileBroken += Play;
        }

        private void OnDisable()
        {
            if (_adapter != null) _adapter.TileBroken -= Play;
        }

        private void OnDestroy()
        {
            // Sprite.Create로 만든 조각 그림은 직접 지워야 메모리에서 빠진다
            foreach (Sprite[] pieces in _pieceCache.Values)
            {
                foreach (Sprite piece in pieces) Destroy(piece);
            }
            _pieceCache.Clear();
        }

        private void Play(TileBreakInfo info)
        {
            if (_parent == null || info.View == null || info.CoverSprite == null) return;

            Sprite[] pieces = GetPieces(info.CoverSprite);
            float unit = _adapter.TileSize;
            float speedMultiplier = info.Tool == ToolMode.AttackDestroy ? _attackSpeedMultiplier : 1f;

            if (info.IsFullBreak)
            {
                for (int i = 0; i < pieces.Length; i++) Launch(info, pieces, i, unit, speedMultiplier, 1f);
                return;
            }

            // 강화만 벗겨질 때는 몇 조각만 작게 떨어져 나간다
            for (int n = 0; n < _reinforcementChipCount; n++)
            {
                int index = Random.Range(0, pieces.Length);
                Launch(info, pieces, index, unit, speedMultiplier * _reinforcementChipScale, _reinforcementChipScale);
            }
        }

        /// <summary>
        /// index번 조각을 덮개에서의 원래 자리에 놓고 바깥쪽으로 튀어 나가게 한다.
        /// </summary>
        private void Launch(TileBreakInfo info, Sprite[] pieces, int index, float unit, float speedMultiplier, float scale)
        {
            int column = index % _piecesPerSide;
            int row = index / _piecesPerSide;

            // 조각 중심이 타일 중심에서 얼마나 떨어져 있는지(-0.5 ~ 0.5, 덮개 크기 비율)
            var direction = new Vector2(
                (column + 0.5f) / _piecesPerSide - 0.5f,
                (row + 0.5f) / _piecesPerSide - 0.5f);

            Transform tile = info.View.transform;
            Vector3 tileScale = info.View.BaseScale;
            Vector2 coverSize = info.CoverSprite.bounds.size;
            var offset = new Vector3(direction.x * coverSize.x * tileScale.x, direction.y * coverSize.y * tileScale.y, 0f);

            // 가운데 조각은 방향이 없으므로 아무 방향으로나 보낸다
            Vector2 outward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;
            Vector2 velocity = outward * _outwardSpeed.RandomValue()
                               + Vector2.up * _upwardSpeed.RandomValue();

            Fragment fragment = GetFragment();
            fragment.Renderer.sprite = pieces[index];
            fragment.Color = info.CoverColor;
            fragment.Renderer.color = info.CoverColor;
            fragment.Transform.localPosition = tile.localPosition + offset;
            fragment.Transform.localRotation = Quaternion.identity;
            fragment.StartScale = tileScale * scale;
            fragment.Transform.localScale = fragment.StartScale;
            fragment.Velocity = (Vector3)(velocity * (unit * speedMultiplier));
            fragment.SpinSpeed = Random.Range(-_maxSpinSpeed, _maxSpinSpeed);
            fragment.Age = 0f;
            fragment.Transform.gameObject.SetActive(true);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;   // 일시정지 중에는 조각도 멈춘다

            float gravity = _gravity * _adapter.TileSize;
            foreach (Fragment fragment in _fragments)
            {
                if (!fragment.Transform.gameObject.activeSelf) continue;

                fragment.Age += deltaTime;
                float t = fragment.Age / _lifetime;   // 0에서 1로 가는 진행도
                if (t >= 1f)
                {
                    fragment.Transform.gameObject.SetActive(false);
                    continue;
                }

                // 등가속도 운동: 속도에 중력을 더하고, 위치에 속도를 더한다
                fragment.Velocity += Vector3.down * (gravity * deltaTime);
                fragment.Transform.localPosition += fragment.Velocity * deltaTime;
                fragment.Transform.Rotate(0f, 0f, fragment.SpinSpeed * deltaTime);
                fragment.Transform.localScale = fragment.StartScale * Mathf.Lerp(1f, _endScale, t);

                // 앞부분은 또렷하게 두고, _fadeStart 이후에 투명해진다
                float fade = Mathf.InverseLerp(1f, _fadeStart, t);
                Color color = fragment.Color;
                color.a *= fade;
                fragment.Renderer.color = color;
            }
        }

        /// <summary>
        /// 쉬고 있는 조각 오브젝트를 하나 꺼낸다. 없으면 새로 만든다.
        /// </summary>
        private Fragment GetFragment()
        {
            foreach (Fragment fragment in _fragments)
            {
                if (!fragment.Transform.gameObject.activeSelf) return fragment;
            }

            var go = new GameObject("TileFragment");
            go.layer = _parent.gameObject.layer;
            go.transform.SetParent(_parent, false);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = _sortingOrder;

            var created = new Fragment { Transform = go.transform, Renderer = spriteRenderer };
            _fragments.Add(created);
            return created;
        }

        /// <summary>
        /// 덮개 그림을 N×N 조각 그림으로 자른다. 인덱스는 왼쪽 아래부터 오른쪽으로, 그다음 위 줄 순서다.
        /// </summary>
        /// <remarks>
        /// 원본 텍스처를 복사하지 않고 영역만 나눠 가리키므로 Read/Write 설정이 필요 없다.
        /// 덮개 그림이 스프라이트 아틀라스에 묶이면 textureRect가 달라질 수 있으니 아틀라스에 넣지 않는다.
        /// </remarks>
        private Sprite[] GetPieces(Sprite cover)
        {
            if (_pieceCache.TryGetValue(cover, out Sprite[] cached)) return cached;

            Rect rect = cover.textureRect;
            float pieceWidth = rect.width / _piecesPerSide;
            float pieceHeight = rect.height / _piecesPerSide;
            var pieces = new Sprite[_piecesPerSide * _piecesPerSide];

            for (int row = 0; row < _piecesPerSide; row++)
            {
                for (int column = 0; column < _piecesPerSide; column++)
                {
                    var pieceRect = new Rect(rect.x + column * pieceWidth, rect.y + row * pieceHeight, pieceWidth, pieceHeight);
                    Sprite piece = Sprite.Create(cover.texture, pieceRect, new Vector2(0.5f, 0.5f),
                        cover.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    piece.name = $"{cover.name}_Piece{row * _piecesPerSide + column}";
                    pieces[row * _piecesPerSide + column] = piece;
                }
            }

            _pieceCache[cover] = pieces;
            return pieces;
        }
    }
}
