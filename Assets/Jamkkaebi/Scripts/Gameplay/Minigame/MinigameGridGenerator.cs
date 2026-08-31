using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public static class MinigameGridGenerator
    {
        private static bool TryPlacePolyomino(PolyominoShape shape, Tile[,] tiles, List<Vector2Int> emptyCells, out List<Vector2Int> placedCoordinates)
        {
            // 모양 가져오고 회전하기
            Vector2Int[] polyShape = PolyominoShapes.Definitions[shape];
            
            int rotationCount = Random.Range(0, 4);
            Vector2Int[] rotatedShape = PolyominoRotator.Rotate(polyShape, rotationCount);
            
            // 오프셋 범위 계산하기
            Vector2Int polyBox =
                new Vector2Int(rotatedShape.GetUpperBound(0) + 1, rotatedShape.GetUpperBound(1) + 1);
            
            Vector2Int offset = new Vector2Int(Random.Range(0, tiles.GetLength(0) - polyBox.x + 1),
                Random.Range(0, tiles.GetLength(1) - polyBox.y + 1));
            
            // 타일 하나씩, 배치 가능한지 검사하기
            List<Vector2Int> tryingCoordinates = new List<Vector2Int>();
            for (int i = 0; i < rotatedShape.Length; i++){
                tryingCoordinates.Add(rotatedShape[i]+offset);
                // 그리드 범위를 벗어났는지 검사
                if (tryingCoordinates[i].x >= tiles.GetLength(0) || tryingCoordinates[i].y >= tiles.GetLength(1))
                {
                    placedCoordinates = new List<Vector2Int>();
                    return false;
                }
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

        public static bool TryPlaceSingleTile(TileContent content, Tile[,] tiles, List<Vector2Int> emptyCells, out Vector2Int placedCoordinate)
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

        public static void PlaceTileType(TileContent content, int count, Tile[,] tiles, List<Vector2Int> emptyCells)
        {
            for (int c = 0; c < count; c++)
            {
                if (!TryPlaceSingleTile(content, tiles, emptyCells, out _))
                {
                    throw new ArgumentException($"{c}번째 {content} 타일을 배치하는 데 실패하였습니다. 그리드가 모두 찼습니다.");
                }
            }
        }
        
        public static MinigameGridResult Generate(MinigamePhaseConfig config)
        {
            // 1. 타일, 빈 셀 리스트 준비
            const int maxAttempts = 1000;
            
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

                for (int attmept = 0; attmept < maxAttempts; attmept++) {
                    if (TryPlacePolyomino(config.Shapes[i], tiles, emptyCells, out var polyCoordinate))
                    {
                        groups[i] = new PolyominoGroup(config.Shapes[i], polyCoordinate);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    throw new ArgumentException($"폴리오미노 배치 실패: {i}번째 {config.Shapes[i]} 모양을 {maxAttempts}번 시도했지만 배치할 자리를 찾지 못했습니다.");
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
            
            // 5. 전체 그리드에서 타일 강화하기
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
            Dictionary<int, PolyominoGroup> groupDict = new Dictionary<int, PolyominoGroup>();

            for (int i = 0; i < config.Shapes.Count; i++)
            {
                groupDict.Add(i, groups[i]);
            }
            
            MinigameGrid grid = new MinigameGrid(tiles, groupDict);
            IReadOnlyList<int> groupdIds = new List<int>(groupDict.Keys);
            
            MinigameGridResult result = new MinigameGridResult(grid, groupdIds);
            
            return result;
        }
    }
}