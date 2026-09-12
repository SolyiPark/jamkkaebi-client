using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public static class MinigameGridGenerator
    {
        private static bool TryPlacePolyomino(PolyominoShape shape, Tile[,] tiles, List<Vector2Int> emptyCells, out List<Vector2Int> placedCoordinates)
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
            
            Vector2Int[] rotatedShape = rotatedShapeCandidates[Random.Range(0, rotatedShapeCandidates.Count)];
            
            // 오프셋 범위 계산하기
            Vector2Int polyBox = GetBoundingBox(rotatedShape);
            
            Vector2Int offset = new Vector2Int(Random.Range(0, tiles.GetLength(0) - polyBox.x + 1),
                Random.Range(0, tiles.GetLength(1) - polyBox.y + 1));
            
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

        private static bool TryPlaceSingleTile(TileContent content, Tile[,] tiles, List<Vector2Int> emptyCells, out Vector2Int placedCoordinate)
        {
            // 그리드가 꽉 찼다면 실패
            if (emptyCells.Count == 0)
            {
                placedCoordinate = new Vector2Int(-1, -1);
                return false;
            }
            
            // 빈 셀 목록에서 랜덤으로 골라 타일 놓기
            Vector2Int tryingCoordinate = emptyCells[Random.Range(0, emptyCells.Count)];
            tiles[tryingCoordinate.x, tryingCoordinate.y] = new Tile(content);
            emptyCells.Remove(tryingCoordinate);

            placedCoordinate = tryingCoordinate;
            return true;
        }

        private static void PlaceTileType(TileContent content, int count, Tile[,] tiles, List<Vector2Int> emptyCells)
        {
            for (int c = 0; c < count; c++)
            {
                if (!TryPlaceSingleTile(content, tiles, emptyCells, out _))
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
        
        public static MinigameGrid Generate(MinigamePhaseConfig config)
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
                    if (TryPlacePolyomino(config.Shapes[i], tiles, emptyCells, out var polyCoordinates))
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
            PlaceTileType(TileContent.Threat, config.ThreatTileCount, tiles, emptyCells);
            PlaceTileType(TileContent.Helper, config.HelperTileCount, tiles, emptyCells);
            
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
                Vector2Int coordinate = cells[Random.Range(0, cells.Count)];
                tiles[coordinate.x, coordinate.y].Reinforce();
                cells.Remove(coordinate);
            }
            
            // 6. 최종 그리드 만들기
            MinigameGrid result = new MinigameGrid(tiles, groups);
            
            return result;
        }
    }
}