using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public readonly struct PolyominoGroup
    {
        public PolyominoShape Shape { get; }
        /// <summary>
        /// 배치된 칸 좌표. i번째 좌표는 <see cref="PolyominoShapes.Definitions"/>에서 같은 형상의 i번째 칸이다.
        /// </summary>
        public IReadOnlyList<Vector2Int> Coordinates { get; }
        /// <summary>
        /// 기본 방향에서 반시계로 90°씩 돌린 횟수. 0~3.
        /// </summary>
        /// <remarks>칸 조각 그림을 같은 방향으로 돌려 표시할 때 쓴다.</remarks>
        public int RotationCount { get; }

        /// <summary>
        /// 배치된 폴리오미노 하나를 만든다.
        /// </summary>
        /// <param name="shape">형상 종류.</param>
        /// <param name="coordinates">칸 인덱스 순서대로 정렬된 그리드 좌표.</param>
        /// <param name="rotationCount">기본 방향에서 반시계로 90°씩 돌린 횟수. 0~3.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="rotationCount"/>가 0~3 밖일 때.</exception>
        public PolyominoGroup(PolyominoShape shape, IReadOnlyList<Vector2Int> coordinates, int rotationCount = 0)
        {
            if (rotationCount < 0 || rotationCount > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(rotationCount),
                    $"회전 횟수는 0~3이어야 합니다. (입력값: {rotationCount})");
            }
            
            Shape = shape;
            Coordinates = new List<Vector2Int>(coordinates);
            RotationCount = rotationCount;
        }
    }
}