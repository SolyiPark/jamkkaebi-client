using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public static class PolyominoSetPicker
    {
        /// <summary>
        /// 규칙에 맞는 폴리오미노 세트를 하나 뽑는다.
        /// </summary>
        /// <remarks>
        /// rng를 정확히 한 번 소비. 같은 규칙, 같은 shapeCount, 같은 rng 상태면 같은 세트를 반환. 풀의 순서가 바뀌면 결과도 바뀐다.
        /// </remarks>
        /// <param name="rule"> 폴리오미노 세트 선택 규칙.</param>
        /// <param name="shapeCount"> 뽑을 모양의 개수. 1 이상이어야 한다.</param>
        /// <param name="rng"> 세트를 고르는 데 쓰는 난수 생성기. 정확히 한 번 소비된다.</param>
        /// <returns>서로 다른 모양 <paramref name="shapeCount"/>개로 이뤄진 세트. 타일 칸 수 합이 규칙의 범위 안에 든다.</returns>
        /// <exception cref="ArgumentNullException"> rng가 null일 때</exception>
        /// <exception cref="ArgumentException"> 선택할 모양 개수가 0 이하이거나, 모양 풀이 연결되지 않았을 때</exception>
        /// <exception cref="InvalidOperationException"> 조건을 만족하는 폴리오미노 세트 후보가 없을 때.</exception>
        public static IReadOnlyList<PolyominoShape> Pick(PolyominoSetRule rule, int shapeCount, Random rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }
            if (shapeCount <= 0)
            {
                throw new ArgumentException($"선택할 모양 개수가 0 이하입니다. (입력값: {shapeCount})", nameof(shapeCount));
            }
            if (rule.ShapePool == null)
            {
                throw new ArgumentException("모양 풀이 연결되지 않았습니다.", nameof(rule));
            }

            List<PolyominoShape[]> candidates = new List<PolyominoShape[]>();
            CollectCandidates(rule, 0, shapeCount, 0, new List<PolyominoShape>(), candidates);

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    $"다음 조건을 만족하는 폴리오미노 세트 후보가 없습니다. 폴리오미노 개수: {shapeCount}, 타일의 합 N: {rule.MinTileSum} <= N <= {rule.MaxTileSum}, 모양 풀 크기: {rule.ShapePool.Count}");
            }
            
            return candidates[rng.Next(0, candidates.Count)];
        }

        /// <summary>
        /// 규칙을 만족하는 폴리오미노 세트 후보를 모두 모은다.
        /// </summary>
        /// <remarks>
        /// 호출이 끝나면 current가 호출 전 상태로 되돌아온다.
        /// </remarks>
        /// <param name="rule"> 폴리오미노 세트 선택 규칙.</param>
        /// <param name="startIndex"> 이번 호출에서 고를 수 있는 첫 위치. 풀의 이 위치부터 끝까지만 선택 후보가 된다.</param>
        /// <param name="remaining"> 아직 더 골라야 하는 모양의 개수. 초기값은 shapeCount, 0이면 세트 완성.</param>
        /// <param name="currentSum"> 지금까지 고른 모양들의 타일 칸 수 합.</param>
        /// <param name="current"> 지금까지 고른 모양을 담는 임시 목록.</param>
        /// <param name="candidates"> 결과를 모으는 곳. 조건을 통과한 세트를 복사해서 추가한다.</param>
        private static void CollectCandidates(PolyominoSetRule rule, int startIndex, int remaining, int currentSum,
            List<PolyominoShape> current, List<PolyominoShape[]> candidates)
        {
            // 모양은 최소 1칸이라 합은 줄어들 수 없다. 이미 상한을 넘겼으면 더 고를 필요가 없다.
            if (currentSum > rule.MaxTileSum) return;
            
            if (remaining == 0)
            {
                if (currentSum <= rule.MaxTileSum && currentSum >= rule.MinTileSum)
                {
                    candidates.Add(current.ToArray());
                }

                return;
            }

            for (int i = startIndex; i < rule.ShapePool.Count; i++)
            {
                current.Add(rule.ShapePool[i]);
                CollectCandidates(rule, i + 1, remaining - 1, currentSum + PolyominoShapes.Definitions[rule.ShapePool[i]].Count, current, candidates);
                // 이번 선택을 되돌려서 다음 모양을 같은 상태에서 시도할 수 있게 한다.
                current.RemoveAt(current.Count - 1);
            }
        }
    }
}