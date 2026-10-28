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

        private static void ValidateConfig(MinigamePhaseConfig config)
        {
            // 1. ReinforcedTileCount <= Width * Height
            if (config.ReinforcedTileCount > config.Width * config.Height)
            {
                throw new InvalidOperationException(
                    $"{config.Width} * {config.Height} 그리드에는 {config.ReinforcedTileCount}개의 강화타일을 배치할 수 없습니다.");
            }
            
            // 2. 폴리오미노 전체 칸 수 + ThreatTileCount + HelperTileCount <= Width * Height
            int polyTileCount = 0;
            for (int i = 0; i < config.Shapes.Count; i++)
            {
                polyTileCount += PolyominoShapes.Definitions[config.Shapes[i]].Count;
            }

            int tileCount = polyTileCount + config.ThreatTileCount + config.HelperTileCount;
            if (tileCount > config.Width * config.Height)
            {
                throw new InvalidOperationException(
                    $"{config.Width} * {config.Height} 그리드에 {tileCount}개의 타일을 배치할 수 없습니다.");
            }
            
            // 3. 각 shape이 4방향 회전 중 최소 하나라도 그리드에 들어맞는지
            for (int i = 0; i < config.Shapes.Count; i++)
            {
                IReadOnlyList<Vector2Int> polyShape = PolyominoShapes.Definitions[config.Shapes[i]];
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
                        $"{config.Width} * {config.Height} 그리드에 {config.Shapes[i]} 폴리오미노를 배치할 수 없습니다.");
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
        /// 호출자가 준 난수 생성기로 설정에 맞는 그리드를 생성한다.
        /// </summary>
        /// <remarks>
        /// rng는 호출자가 만든 것을 받으며, 이미 소비된 상태에서 이어서 사용한다.
        /// 이 안에서 rng를 소비하는 순서가 보드를 결정하므로, 순서를 바꾸면 같은 시드의 보드가 달라진다.
        /// </remarks>
        /// <param name="config">그리드 크기, 모양, 특수 타일 개수 등이 담긴 설정.</param>
        /// <param name="rng">그리드 생성에 쓸 난수 생성기.</param>
        /// <returns>새로 생성된 그리드.</returns>
        /// <exception cref="InvalidOperationException">설정대로 타일을 배치할 수 없을 때.</exception>
        private static MinigameGrid GenerateCore(MinigamePhaseConfig config, Random rng)
        {
            // 0. configuration 유효성 검사(이 수치대로 그리드 배치가 실제로 가능한지)
            ValidateConfig(config);
            
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
            PolyominoGroup[] groups = new PolyominoGroup[config.Shapes.Count];

            for (int i = 0; i < config.Shapes.Count; i++) {
                bool placed = false;

                for (int attempt = 0; attempt < MaxAttempts; attempt++) {
                    if (TryPlacePolyomino(config.Shapes[i], tiles, emptyCells, rng, out var polyCoordinates))
                    {
                        groups[i] = new PolyominoGroup(config.Shapes[i], polyCoordinates);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    throw new InvalidOperationException($"폴리오미노 배치 실패: {i}번째 {config.Shapes[i]} 모양을 {MaxAttempts}번 시도했지만 배치할 자리를 찾지 못했습니다.");
                }
            }
            
            // 3. 위협, 도움 타일 배치
            PlaceTileType(TileContent.Threat, config.ThreatTileCount, tiles, emptyCells, rng);
            PlaceTileType(TileContent.Helper, config.HelperTileCount, tiles, emptyCells, rng);
            
            // 4. 남은 셀을 일반 타일로 채우기
            for (int i = 0; i < emptyCells.Count; i++)
            {
                tiles[emptyCells[i].x, emptyCells[i].y] = new Tile(TileContent.Empty);
            }
            
            // 5. 전체 그리드에서 타일 강화하기 (타깃/위협/도움 타일도 강화 대상에 포함됨)
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int x = 0; x < config.Width; x++)
            {
                for (int y = 0; y < config.Height; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }

            for (int i = 0; i < config.ReinforcedTileCount; i++)
            {
                Vector2Int coordinate = cells[rng.Next(0, cells.Count)];
                tiles[coordinate.x, coordinate.y].Reinforce();
                cells.Remove(coordinate);
            }
            
            // 6. 최종 그리드 만들기
            MinigameGrid result = new MinigameGrid(tiles, groups);
            
            return result;
        }

        /// <summary>
        /// 시드로 난수를 만들어 설정에 맞는 미니게임 그리드를 생성한다.
        /// </summary>
        /// <remarks>
        /// 같은 설정과 같은 시드는 항상 같은 그리드를 만든다.
        /// </remarks>
        /// <param name="config">그리드 크기, 모양, 특수 타일 개수 등이 담긴 설정.</param>
        /// <param name="seed">난수 생성 시드. 같은 값이면 같은 그리드가 나온다.</param>
        /// <returns>새로 생성된 그리드.</returns>
        /// <exception cref="InvalidOperationException">설정대로 타일을 배치할 수 없을 때(칸 수 초과, 모양이 들어가지 않음, 배치 시도 횟수 초과).</exception>
        public static MinigameGrid Generate(MinigamePhaseConfig config, long seed)
        {
            return GenerateCore(config, CreateRng(seed));
        }

        /// <summary>
        /// 규칙에 맞게 고른 폴리오미노 세트로 미니게임 그리드를 생성한다.
        /// </summary>
        /// <remarks>
        /// 같은 config, rule, shapeCount, seed는 같은 그리드를 만든다.
        /// Pick과 GenerateCore가 같은 rng를 공유하며, Pick이 먼저 난수를 소비하므로 같은 시드라도 기존 Generate(config, seed)와 보드가 다르다.
        /// config.Shapes의 값은 사용되지 않는다(rule이 대신 정한다).
        /// </remarks>
        /// <param name="config">그리드 크기, 특수 타일 개수 등이 담긴 설정.</param>
        /// <param name="rule">고를 모양의 타일 합 범위, 모양 풀이 담긴 규칙.</param>
        /// <param name="shapeCount">고를 모양의 개수. 1 이상이어야 한다. 서버에서 받는다.</param>
        /// <param name="seed">rng 생성 시드. 서버에서 받는다.</param>
        /// <returns>새로 생성된 그리드.</returns>
        /// <exception cref="ArgumentException">shapeCount가 0 이하일 때, rule의 모양 풀이 연결되지 않았을 때.</exception>
        /// <exception cref="InvalidOperationException">폴리오미노 세트 후보가 없을 때, 폴리오미노 세트를 그리드에 배치할 수 없을 때(칸 수 초과, 모양이 들어가지 않음, 배치 시도 횟수 초과).</exception>
        public static MinigameGrid Generate(MinigamePhaseConfig config, PolyominoSetRule rule, int shapeCount,
            long seed)
        {
            Random rng = CreateRng(seed);
            IReadOnlyList<PolyominoShape> shapes = PolyominoSetPicker.Pick(rule, shapeCount, rng);
            MinigamePhaseConfig configWithShapes = config.WithShapes(shapes);
            return GenerateCore(configWithShapes, rng);
        }

        // System.Random은 int 시드만 받으므로, long 시드의 상위 32비트와 하위 32비트를 XOR해서 int로 접는다.
        // 상위 비트만 달라도 결과가 달라지게 하려는 것이며, 프로젝트 설정과 무관하게 잘리도록 unchecked로 감쌌다.
        private static Random CreateRng(long seed)
        {
            return new Random(unchecked((int)(seed ^ (seed >> 32))));
        }
    }
}