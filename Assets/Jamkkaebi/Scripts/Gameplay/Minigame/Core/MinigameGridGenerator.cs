using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public static class MinigameGridGenerator
    {
        private static bool TryPlacePolyomino(PolyominoShape shape, Tile[,] tiles, List<Vector2Int> emptyCells, Random rng, out List<Vector2Int> placedCoordinates)
        {
            // 모양 가져오고 회전하기
            // 회전 방향은 그리드 범위 안에서 가능한 후보를 뽑고 그 안에서 무작위 선택
            IReadOnlyList<Vector2Int> polyShape = PolyominoShapes.Definitions[shape];

            List<Vector2Int[]> rotatedShapeCandidates = new List<Vector2Int[]>();
            
            for (int n = 0; n < 4; n++)
            {
                Vector2Int[] rotatedShapeCandidate = PolyominoRotator.Rotate(polyShape, n);
                Vector2Int polyBoxCandidate = GetBoundingBox(rotatedShapeCandidate);
                if (polyBoxCandidate.x <= tiles.GetLength(0) && polyBoxCandidate.y <= tiles.GetLength(1))
                {
                    rotatedShapeCandidates.Add(rotatedShapeCandidate);
                }
            }
            
            Vector2Int[] rotatedShape = rotatedShapeCandidates[rng.Next(0, rotatedShapeCandidates.Count)];
            
            // 오프셋 범위 계산하기
            Vector2Int polyBox = GetBoundingBox(rotatedShape);
            
            Vector2Int offset = new Vector2Int(rng.Next(0, tiles.GetLength(0) - polyBox.x + 1),
                rng.Next(0, tiles.GetLength(1) - polyBox.y + 1));
            
            // 타일 하나씩, 배치 가능한지 검사하기
            List<Vector2Int> tryingCoordinates = new List<Vector2Int>();
            for (int i = 0; i < rotatedShape.Length; i++){
                tryingCoordinates.Add(rotatedShape[i]+offset);
                
                // 중복 배치 검사
                if (tiles[tryingCoordinates[i].x, tryingCoordinates[i].y] != null)
                {
                    placedCoordinates = new List<Vector2Int>();
                    return false;
                }
            }

            // 타일 배치
            for (int i = 0; i < rotatedShape.Length; i++)
            {
                tiles[tryingCoordinates[i].x, tryingCoordinates[i].y] = new Tile(TileContent.Target);
                emptyCells.Remove(tryingCoordinates[i]);
            }
            
            placedCoordinates = tryingCoordinates;
            return true;
        }

        private static bool TryPlaceSingleTile(TileContent content, Tile[,] tiles, List<Vector2Int> emptyCells, Random rng, out Vector2Int placedCoordinate)
        {
            // 그리드가 꽉 찼다면 실패
            if (emptyCells.Count == 0)
            {
                placedCoordinate = new Vector2Int(-1, -1);
                return false;
            }
            
            // 빈 셀 목록에서 랜덤으로 골라 타일 놓기
            Vector2Int tryingCoordinate = emptyCells[rng.Next(0, emptyCells.Count)];
            tiles[tryingCoordinate.x, tryingCoordinate.y] = new Tile(content);
            emptyCells.Remove(tryingCoordinate);

            placedCoordinate = tryingCoordinate;
            return true;
        }

        private static void PlaceTileType(TileContent content, int count, Tile[,] tiles, List<Vector2Int> emptyCells, Random rng)
        {
            for (int c = 0; c < count; c++)
            {
                if (!TryPlaceSingleTile(content, tiles, emptyCells, rng, out _))
                {
                    throw new InvalidOperationException($"{c}번째 {content} 타일을 배치하는 데 실패하였습니다. 그리드가 모두 찼습니다.");
                }
            }
        }

        // 후보 칸 중에서 서로 다른 칸 count개를 골라 강화한다. 고른 칸은 후보에서 빠진다.
        private static void ReinforceRandomCells(Tile[,] tiles, List<Vector2Int> candidates, int count, Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                int index = rng.Next(0, candidates.Count);
                Vector2Int coordinate = candidates[index];
                tiles[coordinate.x, coordinate.y].Reinforce();
                candidates.RemoveAt(index);
            }
        }

        private static void ValidateConfig(MinigameBoardConfig config, IReadOnlyList<PolyominoShape> shapes)
        {
            // 0. 강화 타일 수는 0 이상이어야 한다. 음수면 상한 검사를 통과하고 배치 반복도 돌지 않아 잘못된 설정이 조용히 통과한다.
            if (config.ReinforcedOnShapeCount < 0)
            {
                throw new ArgumentException(
                    $"형상 안 강화 타일 수는 0 이상이어야 합니다. (입력값: {config.ReinforcedOnShapeCount})", nameof(config));
            }
            if (config.ReinforcedOnFieldCount < 0)
            {
                throw new ArgumentException(
                    $"형상 밖 강화 타일 수는 0 이상이어야 합니다. (입력값: {config.ReinforcedOnFieldCount})", nameof(config));
            }
            if (config.BonusTileCount < 0)
            {
                throw new ArgumentException(
                    $"보너스 타일 수는 0 이상이어야 합니다. (입력값: {config.BonusTileCount})", nameof(config));
            }
            
            // 1. 폴리오미노 전체 칸 수 + ThreatTileCount + HelperTileCount <= Width * Height
            int polyTileCount = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                polyTileCount += PolyominoShapes.Definitions[shapes[i]].Count;
            }

            int tileCount = polyTileCount + config.ThreatTileCount + config.HelperTileCount;
            if (tileCount > config.Width * config.Height)
            {
                throw new InvalidOperationException(
                    $"{config.Width} * {config.Height} 그리드에 {tileCount}개의 타일을 배치할 수 없습니다.");
            }
            
            // 2. 형상 안 강화 칸 수 <= 목표 형상 칸 수
            if (config.ReinforcedOnShapeCount > polyTileCount)
            {
                throw new InvalidOperationException(
                    $"형상 안 강화 타일 {config.ReinforcedOnShapeCount}칸이 목표 형상 칸 수 {polyTileCount}를 넘습니다.");
            }
            
            // 2-1. 형상 밖 강화 칸 수 <= 일반 칸 수(목표·위협·도움을 놓고 남는 칸)
            int plainCellCount = config.Width * config.Height - tileCount;
            if (config.ReinforcedOnFieldCount > plainCellCount)
            {
                throw new InvalidOperationException(
                    $"형상 밖 강화 타일 {config.ReinforcedOnFieldCount}칸을 놓을 일반 칸이 {plainCellCount}칸뿐입니다.");
            }
            
            // 2-2. 보너스 타일 수 <= 일반 칸 수. 보너스와 형상 밖 강화는 같은 칸에 겹칠 수 있어 따로 비교한다.
            if (config.BonusTileCount > plainCellCount)
            {
                throw new InvalidOperationException(
                    $"보너스 타일 {config.BonusTileCount}칸을 놓을 일반 칸이 {plainCellCount}칸뿐입니다.");
            }
            
            // 3. 각 shape이 4방향 회전 중 최소 하나라도 그리드에 들어맞는지
            for (int i = 0; i < shapes.Count; i++)
            {
                IReadOnlyList<Vector2Int> polyShape = PolyominoShapes.Definitions[shapes[i]];
                bool fitsInGrid = false;
                
                for (int n = 0; n < 4; n++)
                {
                    Vector2Int[] rotatedShape = PolyominoRotator.Rotate(polyShape, n);
                    Vector2Int polyBox = GetBoundingBox(rotatedShape);
                    if (polyBox.x <= config.Width && polyBox.y <= config.Height)
                    {
                        fitsInGrid = true;
                        break;
                    }
                }

                if (!fitsInGrid)
                {
                    throw new InvalidOperationException(
                        $"{config.Width} * {config.Height} 그리드에 {shapes[i]} 폴리오미노를 배치할 수 없습니다.");
                }
            }
        }

        private static Vector2Int GetBoundingBox(Vector2Int[] shape)
        {
            int maxX = shape[0].x;
            int maxY = shape[0].y;

            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i].x > maxX)
                {
                    maxX = shape[i].x;
                }

                if (shape[i].y > maxY)
                {
                    maxY = shape[i].y;
                }
            }
            
            return new Vector2Int(maxX + 1, maxY + 1);
        }
        
        /// <summary>
        /// 호출자가 준 난수 생성기와 모양 목록으로 설정에 맞는 그리드를 생성한다.
        /// </summary>
        /// <remarks>
        /// rng는 호출자가 만든 것을 받으며, 이미 소비된 상태에서 이어서 사용한다.
        /// 이 안에서 rng를 소비하는 순서가 보드를 결정하므로, 순서를 바꾸면 같은 시드의 보드가 달라진다.
        /// </remarks>
        /// <param name="config">그리드 크기, 특수 타일 개수 등이 담긴 설정.</param>
        /// <param name="shapes">배치할 목표 모양 목록.</param>
        /// <param name="rng">그리드 생성에 쓸 난수 생성기.</param>
        /// <returns>새로 생성된 그리드.</returns>
        /// <exception cref="InvalidOperationException">설정대로 타일을 배치할 수 없을 때.</exception>
        private static MinigameGrid GenerateCore(MinigameBoardConfig config, IReadOnlyList<PolyominoShape> shapes, Random rng)
        {
            // 0. configuration 유효성 검사(이 수치대로 그리드 배치가 실제로 가능한지)
            ValidateConfig(config, shapes);
            
            // 1. 타일, 빈 셀 리스트 준비
            const int MaxAttempts = 1000;
            
            Tile[,] tiles = new Tile[config.Width, config.Height];
            List<Vector2Int> emptyCells = new List<Vector2Int>();
            for (int x = 0; x < config.Width; x++)
            {
                for (int y = 0; y < config.Height; y++)
                {
                    emptyCells.Add(new Vector2Int(x, y));
                }
            }
            
            // 2. 폴리오미노 배치
            PolyominoGroup[] groups = new PolyominoGroup[shapes.Count];

            for (int i = 0; i < shapes.Count; i++) {
                bool placed = false;

                for (int attempt = 0; attempt < MaxAttempts; attempt++) {
                    if (TryPlacePolyomino(shapes[i], tiles, emptyCells, rng, out var polyCoordinates))
                    {
                        groups[i] = new PolyominoGroup(shapes[i], polyCoordinates);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    throw new InvalidOperationException($"폴리오미노 배치 실패: {i}번째 {shapes[i]} 모양을 {MaxAttempts}번 시도했지만 배치할 자리를 찾지 못했습니다.");
                }
            }
            
            // 3. 위협, 도움 타일 배치
            PlaceTileType(TileContent.Threat, config.ThreatTileCount, tiles, emptyCells, rng);
            PlaceTileType(TileContent.Helper, config.HelperTileCount, tiles, emptyCells, rng);
            
            // 남은 빈 셀이 곧 일반 칸이다. 형상 밖 강화 후보로 쓰기 위해 복사해 둔다.
            List<Vector2Int> plainCells = new List<Vector2Int>(emptyCells);
            
            // 3-1. 보너스 타일 배치. 일반 칸에서 고르며, plainCells에서는 빼지 않으므로 형상 밖 강화와 겹칠 수 있다.
            PlaceTileType(TileContent.Bonus, config.BonusTileCount, tiles, emptyCells, rng);
            
            // 4. 남은 셀을 일반 타일로 채우기
            for (int i = 0; i < emptyCells.Count; i++)
            {
                tiles[emptyCells[i].x, emptyCells[i].y] = new Tile(TileContent.Empty);
            }
            
            // 5. 강화 타일 배치. 형상 안을 먼저, 형상 밖 일반 칸을 나중에 고른다(순서를 바꾸면 같은 시드의 보드가 달라진다).
            //    위협·도움 타일은 강화하지 않는다.
            List<Vector2Int> shapeCells = new List<Vector2Int>();
            foreach (PolyominoGroup group in groups)
            {
                shapeCells.AddRange(group.Coordinates);
            }
            
            ReinforceRandomCells(tiles, shapeCells, config.ReinforcedOnShapeCount, rng);
            ReinforceRandomCells(tiles, plainCells, config.ReinforcedOnFieldCount, rng);
            
            // 6. 최종 그리드 만들기
            MinigameGrid result = new MinigameGrid(tiles, groups);
            
            return result;
        }

        /// <summary>
        /// 시드로 난수를 만들어, 형상 수와 칸 합에 맞는 목표 모양을 고르고 미니게임 그리드를 생성한다.
        /// </summary>
        /// <remarks>
        /// 같은 설정과 같은 시드는 항상 같은 그리드를 만든다.
        /// 모양은 <see cref="MinigameRules.ShapePool"/>에서 서로 다른 것으로 고르며, 칸 합은 정확히 <see cref="MinigameBoardConfig.ShapeCellTotal"/>이 된다.
        /// 모양 선택이 배치보다 먼저 난수를 소비하므로, 순서를 바꾸면 같은 시드의 보드가 달라진다.
        /// </remarks>
        /// <param name="config">그리드 크기, 형상 수와 칸 합, 특수 타일 개수 등이 담긴 설정.</param>
        /// <param name="seed">난수 생성 시드. 같은 값이면 같은 그리드가 나온다.</param>
        /// <returns>새로 생성된 그리드.</returns>
        /// <exception cref="ArgumentException">형상 수나 칸 합이 0 이하일 때, 강화 타일 수나 보너스 타일 수가 음수일 때.</exception>
        /// <exception cref="InvalidOperationException">형상 수와 칸 합에 맞는 모양 세트가 없을 때, 설정대로 타일을 배치할 수 없을 때(칸 수 초과, 모양이 들어가지 않음, 배치 시도 횟수 초과).</exception>
        public static MinigameGrid Generate(MinigameBoardConfig config, long seed)
        {
            Random rng = CreateRng(seed);
            PolyominoSetRule rule = new PolyominoSetRule(config.ShapeCellTotal, config.ShapeCellTotal, MinigameRules.ShapePool);
            IReadOnlyList<PolyominoShape> shapes = PolyominoSetPicker.Pick(rule, config.ShapeCount, rng);
            return GenerateCore(config, shapes, rng);
        }

        // System.Random은 int 시드만 받으므로, long 시드의 상위 32비트와 하위 32비트를 XOR해서 int로 접는다.
        // 상위 비트만 달라도 결과가 달라지게 하려는 것이며, 프로젝트 설정과 무관하게 잘리도록 unchecked로 감쌌다.
        private static Random CreateRng(long seed)
        {
            return new Random(unchecked((int)(seed ^ (seed >> 32))));
        }
    }
}