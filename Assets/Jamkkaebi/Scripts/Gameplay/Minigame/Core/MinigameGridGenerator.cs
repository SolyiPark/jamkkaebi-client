using System;
using System.Collections.Generic;
using Codice.CM.Common.Checkin.Partial;
using UnityEngine;
using Random = System.Random;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public static class MinigameGridGenerator
    {
        // 간격 검사에 쓰이는 이웃 방향. 4방향은 상하좌우, 8방향은 대각선까지
        private static readonly Vector2Int[] FourWayOffsets =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        private static readonly Vector2Int[] EightWayOffsets =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };
        
        // 형상 배치가 막히면 이 순서로 간격 규칙을 풀어 다시 배치한다.
        private static readonly ShapeSpacing[] SpacingRelaxOrder =
        {
            ShapeSpacing.EightWay, ShapeSpacing.FourWay, ShapeSpacing.None
        };
        
        private static bool TryPlacePolyomino(PolyominoShape shape, Tile[,] tiles, bool[,] blocked, List<Vector2Int> emptyCells, Random rng, ShapeSpacing spacing, out List<Vector2Int> placedCoordinates, out int rotationCount)
        {
            IReadOnlyList<Vector2Int> polyShape = PolyominoShapes.Definitions[shape];
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);

            // 놓을 수 있는 (회전, 위치) 후보를 모두 모은다.
            // 회전 -> x -> y 순서로 모으므로 같은 입력이면 목록 순서도 같다.
            var candidates = new List<(Vector2Int[] Cells, int RotationCount)>();

            for (int n = 0; n < 4; n++)
            {
                Vector2Int[] rotatedShape = PolyominoRotator.Rotate(polyShape, n);
                Vector2Int box = GetBoundingBox(rotatedShape);
                if (box.x > width || box.y > height)
                {
                    continue;
                }

                for (int x = 0; x <= width - box.x; x++)
                {
                    for (int y = 0; y <= height - box.y; y++)
                    {
                        Vector2Int offset = new Vector2Int(x, y);
                        List<Vector2Int> tryingCoordinates = new List<Vector2Int>();
                        bool isFree = true;
                        for (int i = 0; i < rotatedShape.Length; i++)
                        {
                            Vector2Int cell = rotatedShape[i] + offset;
                            if (blocked[cell.x, cell.y])
                            {
                                isFree = false;
                                break;
                            }
                            tryingCoordinates.Add(cell);
                        }

                        if (isFree)
                        {
                            candidates.Add((tryingCoordinates.ToArray(), n));
                        }
                    }
                }
            }

            if (candidates.Count == 0)
            {
                placedCoordinates = new List<Vector2Int>();
                rotationCount = 0;
                return false;
            }
            
            // 이 형상에서 rng는 여기서 한 번만 쓴다.
            var chosen = candidates[rng.Next(0, candidates.Count)];
            
            foreach (Vector2Int cell in chosen.Cells)
            {
                tiles[cell.x, cell.y] = new Tile(TileContent.Target);
                emptyCells.Remove(cell);
            }
            
            MarkBlocked(blocked, chosen.Cells, spacing);
            
            placedCoordinates = new List<Vector2Int>(chosen.Cells);
            rotationCount = chosen.RotationCount;
            return true;
        }

        // 모든 형상을 주어진 간격 규칙으로 놓는다. 하나라도 못 놓으면 false.
        // 실패해도 rng는 소비된 상태로 남는다. 다음 단계가 이어서 쓰므로 같은 시드면 결과도 같다.
        private static bool TryPlaceAllPolyominoes(IReadOnlyList<PolyominoShape> shapes, Tile[,] tiles,
            List<Vector2Int> emptyCells, Random rng, ShapeSpacing spacing, out PolyominoGroup[] groups)
        {
            bool[,] blocked = new bool[tiles.GetLength(0), tiles.GetLength(1)];
            groups = new PolyominoGroup[shapes.Count];

            for (int i = 0; i < shapes.Count; i++)
            {
                if (!TryPlacePolyomino(shapes[i], tiles, blocked, emptyCells, rng, spacing,
                        out List<Vector2Int> polyCoordinates, out int rotationCount))
                {
                    return false;
                }
                
                groups[i] = new PolyominoGroup(shapes[i], polyCoordinates, rotationCount);
            }

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

        private static Vector2Int[] GetNeighborOffsets(ShapeSpacing spacing)
        {
            switch (spacing)
            {
                case ShapeSpacing.FourWay: return FourWayOffsets;
                case ShapeSpacing.EightWay: return EightWayOffsets;
                default: return Array.Empty<Vector2Int>();
            }
        }

        private static void MarkBlocked(bool[,] blocked, IReadOnlyList<Vector2Int> cells, ShapeSpacing spacing)
        {
            Vector2Int[] neighborOffsets = GetNeighborOffsets(spacing);
            foreach (Vector2Int cell in cells)
            {
                blocked[cell.x, cell.y] = true;
                
                foreach (Vector2Int neighborOffset in neighborOffsets)
                {
                    Vector2Int neighbor = cell + neighborOffset;
                    if (neighbor.x < 0 || neighbor.x >= blocked.GetLength(0) || neighbor.y < 0 ||
                        neighbor.y >= blocked.GetLength(1))
                    {
                        continue;
                    }
                    
                    blocked[neighbor.x, neighbor.y] = true;
                }
            }
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
            
            // 1~2. 형상 배치. 기본 간격 규칙부터 시작해 막히면 한 단계씩 풀어 빈 보드에서 다시 놓는다.
            Tile[,] tiles = null;
            List<Vector2Int> emptyCells = null;
            PolyominoGroup[] groups = null;
            ShapeSpacing appliedShapeSpacing = ShapeSpacing.None;
            bool placed = false;
            
            int startIndex = Array.IndexOf(SpacingRelaxOrder, MinigameRules.DefaultShapeSpacing);
            for (int s = startIndex; s < SpacingRelaxOrder.Length; s++)
            {
                tiles = new Tile[config.Width, config.Height];
                emptyCells = CreateAllCells(config.Width, config.Height);

                if (TryPlaceAllPolyominoes(shapes, tiles, emptyCells, rng, SpacingRelaxOrder[s], out groups))
                {
                    appliedShapeSpacing = SpacingRelaxOrder[s];
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                throw new InvalidOperationException("폴리오미노 배치 실패: 간격 규칙을 모두 완화해도 형상을 놓을 자리가 없습니다.");
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
            MinigameGrid result = new MinigameGrid(tiles, groups, appliedShapeSpacing);
            
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
        /// <exception cref="InvalidOperationException">형상 수와 칸 합에 맞는 모양 세트가 없을 때, 설정대로 타일을 배치할 수 없을 때(칸 수 초과, 모양이 들어가지 않음, 간격 규칙을 모두 풀어도 형상을 놓을 자리가 없음).</exception>
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

        private static List<Vector2Int> CreateAllCells(int width, int height)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
            
            return cells;
        }
    }
}