using System;
using System.Collections.Generic;
using System.Linq;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    /// <summary>
    /// 난이도별 폴리오미노 세트 선택 규칙
    /// </summary>
    public readonly struct PolyominoSetRule
    {
        /// <summary>
        /// 타일 칸 수의 합의 하한.
        /// </summary>
        /// <remarks>
        /// 폴리오미노 세트를 고를 때, 고른 폴리오미노의 타일 칸 수의 합이 이보다 크거나 같아야 함.
        /// </remarks>
        public int MinTileSum { get; }
        /// <summary>
        /// 타일 칸 수의 합의 상한.
        /// </summary>
        /// <remarks>
        /// 폴리오미노 세트를 고를 때, 고른 폴리오미노의 타일 칸 수의 합이 이보다 작거나 같아야 함.
        /// </remarks>
        public int MaxTileSum { get; }
        /// <summary>
        /// 사용 가능한 모양의 종류. 서로 다른 모양이어야 함.
        /// </summary>
        public IReadOnlyList<PolyominoShape> ShapePool { get; }

        /// <summary>
        /// 폴리오미노 세트 규칙을 만든다.
        /// </summary>
        /// <param name="minTileSum"> 고른 모양들의 타일 칸 수 합의 하한(포함). 1 이상이어야 한다.</param>
        /// <param name="maxTileSum"> 고른 모양들의 타일 칸 수 합의 상한(포함). 하한 이상이어야 한다.</param>
        /// <param name="shapePool"> 사용 가능한 모양 풀. 비어 있지 않고, 서로 다른 모양이어야 한다.</param>
        /// <exception cref="ArgumentNullException"><paramref name="shapePool"/>이 null일 때.</exception>
        /// <exception cref="ArgumentException">
        /// 풀이 비었거나, 하한이 0 이하이거나, 상한이 하한보다 낮거나, 풀에 중복이 있을 때.
        /// </exception>
        public PolyominoSetRule(int minTileSum, int maxTileSum, IReadOnlyList<PolyominoShape> shapePool)
        {
            if (shapePool == null)
            {
                throw new ArgumentNullException(nameof(shapePool));
            }
            if (shapePool.Count == 0)
            {
                throw new ArgumentException("모양 풀이 비어 있습니다.", nameof(shapePool));
            }
            if (minTileSum <= 0)
            {
                throw new ArgumentException($"하한이 0 이하입니다. 타일은 최소 1칸 이상 필요합니다. (입력값: {minTileSum})", nameof(minTileSum));
            }
            if (maxTileSum < minTileSum)
            {
                throw new ArgumentException($"상한이 최소 하한보다 낮습니다. 상한은 하한보다 높거나 같아야 합니다. (상한: {maxTileSum} < 하한: {minTileSum})", nameof(maxTileSum));
            }
            
            HashSet<PolyominoShape> shapes = new HashSet<PolyominoShape>();
            foreach (PolyominoShape shape in shapePool)
            {
                if (!shapes.Add(shape))
                {
                    throw new ArgumentException($"모양 풀에 중복된 모양({shape})이 있습니다.", nameof(shapePool));
                }
            }
            
            MinTileSum = minTileSum;
            MaxTileSum = maxTileSum;
            ShapePool = shapePool.ToArray();    // 방어적 복사
        }
    }
}