using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation.Backdrop
{
    /// <summary>
    /// 자기 영역 안에서 재, 녹 조각 같은 작은 입자를 흩날리게 한다.
    /// </summary>
    /// <remarks>
    /// 배경 캔버스(UI) 위에서 돌기 때문에 ParticleSystem 대신 RawImage 몇 개를 미리 만들어 두고 재사용한다.
    /// 생성량은 <see cref="Intensity"/>(0~1)에 비례하므로, 배경 연출이 오염도를 그대로 넘기면 정화될수록 입자가 줄어든다.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class UiDriftParticles : MonoBehaviour
    {
        /// <summary>입자 하나. 화면에 보이지 않을 때는 이미지를 꺼 두고 다음 생성 때 다시 쓴다.</summary>
        private class Particle
        {
            public RawImage Image;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public float SwayPhase;
            public float SpinSpeed;
            public float MaxAlpha;
            public bool IsAlive;
        }

        [SerializeField] private Texture _texture;
        [Tooltip("동시에 보일 수 있는 최대 개수. 미리 이만큼 만들어 둔다.")]
        [SerializeField, Min(1)] private int _maxCount = 24;
        [Tooltip("강도 1일 때 초당 생성 수.")]
        [SerializeField, Min(0f)] private float _spawnPerSecond = 6f;
        [SerializeField] private FloatRange _lifetime = new FloatRange(3f, 5f);
        [Tooltip("입자 한 변의 크기(px).")]
        [SerializeField] private FloatRange _size = new FloatRange(8f, 18f);

        [Header("움직임")]
        [Tooltip("초당 이동 거리(px). 위로 오르면 y가 양수, 떨어지면 음수.")]
        [SerializeField] private Vector2 _velocity = new Vector2(0f, 60f);
        [Tooltip("입자마다 속도에 더해지는 무작위 흔들림(px/s).")]
        [SerializeField, Min(0f)] private float _velocityJitter = 20f;
        [Tooltip("좌우로 살랑이는 폭(px).")]
        [SerializeField, Min(0f)] private float _swayAmplitude = 20f;
        [SerializeField, Min(0.01f)] private float _swayPeriod = 2.5f;
        [Tooltip("초당 최대 회전 각도. 0이면 돌지 않는다.")]
        [SerializeField, Min(0f)] private float _maxSpinSpeed;

        [Header("색")]
        [SerializeField] private Color _color = Color.white;
        [Tooltip("일부 입자에 섞을 강조 색(예: 불씨).")]
        [SerializeField] private Color _accentColor = new Color(1f, 0.55f, 0.2f, 1f);
        [Tooltip("강조 색이 나올 확률. 0이면 쓰지 않는다.")]
        [SerializeField, Range(0f, 1f)] private float _accentChance;

        private Particle[] _particles;
        private RectTransform _area;
        private float _spawnBudget;
        private float _intensity;

        /// <summary>새로 생기는 입자의 기본 색. 이미 떠 있는 입자는 바뀌지 않는다.</summary>
        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        /// <summary>생성량 배율(0~1). 0이면 새 입자를 만들지 않고, 남은 입자는 수명대로 사라진다.</summary>
        public float Intensity
        {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            _area = (RectTransform)transform;
            _particles = new Particle[_maxCount];
            for (int i = 0; i < _maxCount; i++)
            {
                _particles[i] = new Particle { Image = CreateImage(i) };
            }
        }

        private void OnDisable()
        {
            // 다른 매개체로 바뀌었다가 돌아왔을 때 지난 판의 입자가 남아 있지 않게 한다
            if (_particles == null) return;
            foreach (Particle particle in _particles)
            {
                Kill(particle);
            }
            _spawnBudget = 0f;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            _spawnBudget += _spawnPerSecond * _intensity * deltaTime;
            while (_spawnBudget >= 1f)
            {
                _spawnBudget -= 1f;
                TrySpawn();
            }

            foreach (Particle particle in _particles)
            {
                if (particle.IsAlive)
                {
                    Tick(particle, deltaTime);
                }
            }
        }

        private RawImage CreateImage(int index)
        {
            var go = new GameObject($"Particle{index}", typeof(RectTransform), typeof(RawImage));
            go.layer = gameObject.layer;
            go.transform.SetParent(_area, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            RawImage image = go.GetComponent<RawImage>();
            image.texture = _texture;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private void TrySpawn()
        {
            Particle particle = FindFreeParticle();
            if (particle == null) return;   // 다 쓰고 있으면 이번 생성은 건너뛴다

            Rect area = _area.rect;
            RectTransform rect = particle.Image.rectTransform;
            rect.anchoredPosition = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
            rect.sizeDelta = Vector2.one * _size.RandomValue();
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Color color = Random.value < _accentChance ? _accentColor : _color;
            particle.MaxAlpha = color.a;
            color.a = 0f;
            particle.Image.color = color;
            particle.Image.enabled = true;

            particle.Velocity = _velocity + Random.insideUnitCircle * _velocityJitter;
            particle.Age = 0f;
            particle.Lifetime = Mathf.Max(0.1f, _lifetime.RandomValue());
            particle.SwayPhase = Random.Range(0f, Mathf.PI * 2f);
            particle.SpinSpeed = Random.Range(-_maxSpinSpeed, _maxSpinSpeed);
            particle.IsAlive = true;
        }

        private void Tick(Particle particle, float deltaTime)
        {
            particle.Age += deltaTime;
            if (particle.Age >= particle.Lifetime)
            {
                Kill(particle);
                return;
            }

            // 좌우 흔들림은 sin 곡선의 기울기(cos)를 속도로 더해, 위치가 sin 모양으로 오가게 한다
            float omega = Mathf.PI * 2f / _swayPeriod;
            float sway = Mathf.Cos(particle.Age * omega + particle.SwayPhase) * _swayAmplitude * omega;

            RectTransform rect = particle.Image.rectTransform;
            rect.anchoredPosition += new Vector2(particle.Velocity.x + sway, particle.Velocity.y) * deltaTime;
            if (particle.SpinSpeed != 0f)
            {
                rect.Rotate(0f, 0f, particle.SpinSpeed * deltaTime);
            }

            // 생성 직후와 사라지기 직전에 투명해져서 갑자기 나타나거나 끊기지 않는다
            Color color = particle.Image.color;
            color.a = particle.MaxAlpha * Mathf.Sin(Mathf.PI * particle.Age / particle.Lifetime);
            particle.Image.color = color;
        }

        private Particle FindFreeParticle()
        {
            foreach (Particle particle in _particles)
            {
                if (!particle.IsAlive) return particle;
            }
            return null;
        }

        private static void Kill(Particle particle)
        {
            particle.IsAlive = false;
            particle.Image.enabled = false;
        }
    }
}
