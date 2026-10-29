using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 최솟값~최댓값 범위. 연출에서 "이 범위 안에서 무작위로" 고를 값을 Inspector에 Min·Max로 보여 준다.
    /// </summary>
    /// <remarks>
    /// Vector2로 범위를 받으면 Inspector에 X·Y로 표시되어 무엇이 최솟값인지 알기 어렵다.
    /// 에디터에서는 FloatRangeDrawer가 한 줄에 Min·Max 칸으로 그린다.
    /// </remarks>
    [Serializable]
    public struct FloatRange
    {
        [SerializeField] private float _min;
        [SerializeField] private float _max;

        public FloatRange(float min, float max)
        {
            _min = min;
            _max = max;
        }

        public float Min => _min;
        public float Max => _max;

        /// <summary>범위 안의 무작위 값(Min 이상 Max 이하).</summary>
        public float RandomValue() => UnityEngine.Random.Range(_min, _max);
    }
}
