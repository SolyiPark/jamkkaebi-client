using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameSessionTests
    {
        MinigamePhaseConfig config = new MinigamePhaseConfig(
            width: 7,
            height: 8,
            shapes: new List<PolyominoShape> { PolyominoShape.Stairs4, PolyominoShape.Line2, PolyominoShape.LShape3 },
            threatTileCount: 5,
            helperTileCount: 2,
            reinforcedTileCount: 3,
            timeLimit: 60f,
            destructiveToolLimit: 19,
            scoutToolLimit: 3,
            allowedThreatHits: 3 
        );

        [Test]
        public void Constructor_WithBaseConfig_InitializesGridAndResourcesPerSpec()
        {
            // Arrange & Act
            MinigameSession session = new MinigameSession(config);
            
            // Assert - Grid 구조
            Assert.AreEqual(7, session.Grid.Width);
            Assert.AreEqual(8, session.Grid.Height);
            Assert.AreEqual(3, session.Grid.PolyominoGroups.Count);
            
            // Assert - 자원 초기값
            Assert.AreEqual(SessionState.InProgress, session.State);
            Assert.AreEqual(19, session.RemainingDestructiveToolUses);
            Assert.AreEqual(3, session.RemainingScoutToolUses);
            Assert.AreEqual(60f, session.RemainingSeconds);
            Assert.AreEqual(0f, session.DamageGauge);

            // Assert - 특수 타일 배치
            Assert.AreEqual(5, CountTiles(session.Grid, t => t.Content == TileContent.Threat));
            Assert.AreEqual(2, CountTiles(session.Grid, t => t.Content == TileContent.Helper));
            Assert.AreEqual(3, CountTiles(session.Grid, t => t.IsReinforced));
        }

        [Test]
        public void UseDestructiveTool_ThreatTileRevealed_IncreasesDamageGauge()
        {
            // Arrange
            Dictionary<Vector2Int, TileContent> layout = new Dictionary<Vector2Int, TileContent> {{new Vector2Int(0, 0), TileContent.Threat}};
            MinigameGrid grid = BuildGrid(2, 2, layout);
            MinigameSession session = new MinigameSession(grid, config);
            
            // Act
            DestroyResult result = session.UseDestructiveTool(new Vector2Int(0, 0), DestructiveToolType.Safe);
            
            // Assert
            Assert.AreEqual(DestroyResult.Success, result);
            Assert.AreEqual(1f/3f, session.DamageGauge, 0.0001f);
            Assert.AreEqual(config.DestructiveToolLimit - 1, session.RemainingDestructiveToolUses);
        }

        [Test]
        public void UseDestructiveTool_ReinforcedThreatTile_FirstUseConsumesReinforcement_SecondUseReveals()
        {
            // Arrange
            Dictionary<Vector2Int, (TileContent, bool)> layout = new Dictionary<Vector2Int, (TileContent, bool)>
                { { new Vector2Int(0, 0), (TileContent.Threat, true) } };
            MinigameGrid grid = BuildGrid(2, 2, layout);
            MinigameSession session = new MinigameSession(grid, config);
            Tile targetTile = grid.GetTile(0, 0);
            
            // Act 1: 첫 번째 시도
            session.UseDestructiveTool(new Vector2Int(0, 0), DestructiveToolType.Safe);
            
            // Assert 1: 강화만 소모, 아직 안 열림, 게이지 변동 없음
            Assert.IsFalse(targetTile.IsReinforced);
            Assert.IsFalse(targetTile.IsRevealed);
            Assert.AreEqual(0f, session.DamageGauge, 0.0001f);
            
            // Act 2: 두 번째 시도
            session.UseDestructiveTool(new Vector2Int(0, 0), DestructiveToolType.Safe);
            
            // Assert 2: 타일 개봉, 대미지 게이지 상승
            Assert.IsTrue(targetTile.IsRevealed);
            Assert.AreEqual(1f/3f, session.DamageGauge, 0.0001f);
        }

        [Test]
        public void UseDestructiveTool_OnRevealedTile_ReturnsNoValidTargets()
        {
            // Arrange
            MinigameGrid grid = BuildGrid(2, 2, new Dictionary<Vector2Int, TileContent>());
            MinigameSession session = new MinigameSession(grid, config);

            grid.GetTile(0, 0).Reveal();
            
            // Act: 이미 개봉된 타일에 안전형 도구 사용
            DestroyResult result = session.UseDestructiveTool(new Vector2Int(0, 0), DestructiveToolType.Safe);
            
            // Assert: 결과는 NoValidTargets
            Assert.AreEqual(DestroyResult.NoValidTargets, result);
            Assert.AreEqual(config.DestructiveToolLimit, session.RemainingDestructiveToolUses);
        }

        [Test]
        public void UseDestructiveTool_OnFourFifthsRevealedTile_ReturnsSuccess()
        {
            // Arrange
            MinigameGrid grid = BuildGrid(3, 3, new Dictionary<Vector2Int, TileContent>());
            MinigameSession session = new MinigameSession(grid, config);
            
            grid.GetTile(1, 0).Reveal();
            grid.GetTile(0, 1).Reveal();
            grid.GetTile(1, 1).Reveal();
            grid.GetTile(1, 2).Reveal();
            
            // Act: 4칸만 개봉된 타일에 공격형 도구 사용
            DestroyResult result = session.UseDestructiveTool(new Vector2Int(1, 1), DestructiveToolType.Attack);
            
            // Assert: 결과는 Success
            Assert.AreEqual(DestroyResult.Success, result);
            Assert.AreEqual(config.DestructiveToolLimit - 1, session.RemainingDestructiveToolUses);
            Assert.IsTrue(grid.GetTile(2, 1).IsRevealed);
        }

        [Test]
        public void UseTool_WithNoUsesRemaining_ReturnsNoUsesRemaining()
        {
            // Arrange
            MinigamePhaseConfig zeroUsesConfig = new MinigamePhaseConfig(
                width: 7,
                height: 8,
                shapes: new List<PolyominoShape> { PolyominoShape.Stairs4, PolyominoShape.Line2, PolyominoShape.LShape3 },
                threatTileCount: 5,
                helperTileCount: 2,
                reinforcedTileCount: 3,
                timeLimit: 60f,
                destructiveToolLimit: 0,
                scoutToolLimit: 3,
                allowedThreatHits: 3 
            );

            MinigameGrid grid = BuildGrid(2, 2, new Dictionary<Vector2Int, TileContent>());
            MinigameSession session = new MinigameSession(grid, zeroUsesConfig);
            
            // Act
            DestroyResult result = session.UseDestructiveTool(new Vector2Int(0, 0), DestructiveToolType.Safe);
            
            // Assert
            Assert.AreEqual(DestroyResult.NoUsesRemaining, result);
            Assert.IsFalse(grid.GetTile(0, 0).IsRevealed);
        }

        [Test]
        public void UseDestructiveTool_SessionAlreadyFailed_ReturnsSessionNotInProgress()
        {
            // Arrange
            MinigamePhaseConfig shortTimeConfig = new MinigamePhaseConfig(
                width: 7,
                height: 8,
                shapes: new List<PolyominoShape> { PolyominoShape.Stairs4, PolyominoShape.Line2, PolyominoShape.LShape3 },
                threatTileCount: 5,
                helperTileCount: 2,
                reinforcedTileCount: 3,
                timeLimit: 1f,
                destructiveToolLimit: 19,
                scoutToolLimit: 3,
                allowedThreatHits: 3 
            );
            
            MinigameGrid grid = BuildGrid(2, 2, new Dictionary<Vector2Int, TileContent>());
            MinigameSession session = new MinigameSession(grid, shortTimeConfig);
            
            session.AdvanceTime(2f);
            Assert.AreEqual(SessionState.Failed, session.State);
            
            // Act
            DestroyResult result = session.UseDestructiveTool(new Vector2Int(0, 0), DestructiveToolType.Safe);
            
            // Assert
            Assert.AreEqual(DestroyResult.SessionNotInProgress, result);
            Assert.IsFalse(grid.GetTile(0, 0).IsRevealed);
        }

        [Test]
        public void UseDestructiveTool_HelperRevealed_RowHasMoreUnrevealedTiles_OpensRow()
        {
            // Arrange
            Dictionary<Vector2Int, TileContent> layout = new Dictionary<Vector2Int, TileContent>
            {
                {new Vector2Int(1, 1), TileContent.Helper}
            };
            MinigameGrid grid = BuildGrid(3, 3, layout);
            MinigameSession session = new MinigameSession(grid, config);
            
            grid.GetTile(1, 0).Reveal(); // 세로 방향 미개봉 타일을 줄여둠
            
            // Act
            session.UseDestructiveTool(new Vector2Int(1, 1), DestructiveToolType.Safe);
            
            // Assert : y = 1인 가로 타일이 전체 열려야 함
            Assert.IsTrue(grid.GetTile(0, 1).IsRevealed);
            Assert.IsTrue(grid.GetTile(2, 1).IsRevealed);
            
            // Assert : 세로는 추가로 열리지 않아야 함
            Assert.IsFalse(grid.GetTile(1, 2).IsRevealed);
        }
        
        [Test]
        public void UseDestructiveTool_HelperRevealed_ColumnHasMoreUnrevealedTiles_OpensColumn()
        {
            // Arrange
            Dictionary<Vector2Int, TileContent> layout = new Dictionary<Vector2Int, TileContent>
            {
                {new Vector2Int(1, 1), TileContent.Helper}
            };
            MinigameGrid grid = BuildGrid(3, 3, layout);
            MinigameSession session = new MinigameSession(grid, config);
            
            grid.GetTile(0, 1).Reveal(); // 가로 방향 미개봉 타일을 줄여둠
            
            // Act
            session.UseDestructiveTool(new Vector2Int(1, 1), DestructiveToolType.Safe);
            
            // Assert : x = 1인 세로 타일이 전체 열려야 함
            Assert.IsTrue(grid.GetTile(1, 0).IsRevealed);
            Assert.IsTrue(grid.GetTile(1, 2).IsRevealed);
            
            // Assert : 가로는 추가로 열리지 않아야 함
            Assert.IsFalse(grid.GetTile(2, 1).IsRevealed);
        }
        
        [Test]
        public void UseDestructiveTool_HelperRevealed_SameNumberOfTilesRevealed_OpensRow()
        {
            // Arrange
            Dictionary<Vector2Int, TileContent> layout = new Dictionary<Vector2Int, TileContent>
            {
                {new Vector2Int(1, 1), TileContent.Helper}
            };
            MinigameGrid grid = BuildGrid(3, 3, layout);
            MinigameSession session = new MinigameSession(grid, config);
            
            // Act
            session.UseDestructiveTool(new Vector2Int(1, 1), DestructiveToolType.Safe);
            
            // Assert : y = 1인 가로 타일이 전체 열려야 함
            Assert.IsTrue(grid.GetTile(0, 1).IsRevealed);
            Assert.IsTrue(grid.GetTile(2, 1).IsRevealed);
            
            // Assert : 세로는 추가로 열리지 않아야 함
            Assert.IsFalse(grid.GetTile(1, 0).IsRevealed);
            Assert.IsFalse(grid.GetTile(1, 2).IsRevealed);
        }
        
        [Test]
        public void UseDestructiveTool_HelperRevealed_AnotherHelperRevealed_OccursChainReaction()
        {
            // Arrange
            Dictionary<Vector2Int, TileContent> layout = new Dictionary<Vector2Int, TileContent>
            {
                {new Vector2Int(1, 1), TileContent.Helper},
                {new Vector2Int(0, 1), TileContent.Helper}
            };
            MinigameGrid grid = BuildGrid(3, 3, layout);
            MinigameSession session = new MinigameSession(grid, config);
            
            // Act : 먼저 y = 1인 가로 줄 오픈, 그 다음 (0, 1)의 헬퍼 타일이 공개되면 가로는 이미 모두 열렸기 때문에 세로를 열어야 함
            session.UseDestructiveTool(new Vector2Int(1, 1), DestructiveToolType.Safe);
            
            // Assert : 연쇄 반응으로 인해 x = 0인 세로 타일이 열려야 함
            Assert.IsTrue(grid.GetTile(0, 0).IsRevealed);
            Assert.IsTrue(grid.GetTile(0, 2).IsRevealed);
        }
        
        // ==============
        //      헬퍼들
        // ==============
        
        private int CountTiles(MinigameGrid grid, Func<Tile, bool> predicate)
        {
            int count = 0;

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (predicate(grid.GetTile(x, y)))
                    {
                        count++;
                    }
                }
            }
            
            return count;
        }

        private MinigameGrid BuildGrid(int width, int height, Dictionary<Vector2Int, TileContent> layout,
            IReadOnlyList<PolyominoGroup> groups = null)
        {
            Dictionary<Vector2Int, (TileContent, bool)> expandedLayout =
                layout.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value, false));
            
            return BuildGrid(width, height, expandedLayout, groups);
        }
        
        private MinigameGrid BuildGrid(int width, int height, Dictionary<Vector2Int, (TileContent Content, bool IsReinforced)> layout,
            IReadOnlyList<PolyominoGroup> groups = null)
        {
            Tile[,] tiles = new Tile[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    
                    // Key가 없는 경우 tileInfo에 기본값을 채움.
                    // Content의 기본값은 Empty(열거형의 첫번째 값), bool의 기본값은 false
                    // 따라서 layout으로 지정하지 않은 좌표에 대해서는 내용물이 없는 일반 타일을 설치
                    layout.TryGetValue(coord, out var tileInfo);
                    
                    tiles[x, y] = new Tile(tileInfo.Content, tileInfo.IsReinforced);
                }
            }
            
            // 폴리오미노 그룹이 지정되지 않았다면(null이라면) 오른쪽 최하단에 한칸짜리 더미 폴리오미노 생성
            // EvaluateEndCondition의 AreAllPolyominoesExcavated()가 리스트가 비어 있으면 true를 반환하기 때문
            // 테스트에 폴리오미노가 필요하지 않아서 지정하지 않은 경우에, 폴리오미노가 없어서 바로 Succeeded 상태가 되는 것을 방지하기 위함
            IReadOnlyList<PolyominoGroup> finalGroups = groups ?? new List<PolyominoGroup>
            {
                new PolyominoGroup(PolyominoShape.Dot,
                    new List<Vector2Int> { new Vector2Int(width - 1, height - 1) })
            };
            
            return new MinigameGrid(tiles, finalGroups);
        }
    }
}