using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using Jamkkaebi.Scripts.Gameplay.Purification;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    /// <summary>미니게임 세션을 서버 결과 보고(PlayReport)로 바꾸는 규칙을 검사합니다.</summary>
    public class MinigameReportTests
    {
        private static MinigameBoardConfig Config(int destructiveToolLimit = 19, int scoutToolLimit = 3, int allowedThreatHits = 3) =>
            new MinigameBoardConfig(
                width: 7, height: 8, shapeCount: 3, shapeCellTotal: 9,
                threatTileCount: 5, helperTileCount: 2, bonusTileCount: 0,
                reinforcedOnShapeCount: 2, reinforcedOnFieldCount: 1,
                destructiveToolLimit: destructiveToolLimit, scoutToolLimit: scoutToolLimit,
                allowedThreatHits: allowedThreatHits);

        /// <summary>
        /// 테스트용 격자. 지정한 칸만 위협 타일이고, 오른쪽 아래 한 칸이 목표 형상(다 캐면 성공)이다.
        /// </summary>
        private static MinigameGrid Grid(int width, int height, params Vector2Int[] threats)
        {
            Tile[,] tiles = new Tile[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                tiles[x, y] = new Tile(Array.IndexOf(threats, new Vector2Int(x, y)) >= 0 ? TileContent.Threat : TileContent.Empty);

            List<PolyominoGroup> groups = new List<PolyominoGroup>
            {
                new PolyominoGroup(PolyominoShape.Dot, new List<Vector2Int> { new Vector2Int(width - 1, height - 1) })
            };
            return new MinigameGrid(tiles, groups);
        }

        /// <summary>지정한 칸에 정성 보너스 타일을 놓은 격자. 오른쪽 아래 한 칸이 목표 형상이다.</summary>
        private static MinigameGrid GridWithBonus(int width, int height, Vector2Int bonus)
        {
            Tile[,] tiles = new Tile[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                tiles[x, y] = new Tile(new Vector2Int(x, y) == bonus ? TileContent.Bonus : TileContent.Empty);

            List<PolyominoGroup> groups = new List<PolyominoGroup>
            {
                new PolyominoGroup(PolyominoShape.Dot, new List<Vector2Int> { new Vector2Int(width - 1, height - 1) })
            };
            return new MinigameGrid(tiles, groups);
        }

        private static void Tap(MinigameSession session, int x, int y) =>
            session.UseDestructiveTool(new Vector2Int(x, y), DestructiveToolType.Safe);

        // ---------- 인자 ----------

        /// <summary>세션이 null이거나, 진행 중인데 포기도 아니면 거부하는지 검사합니다.</summary>
        [Test]
        public void InvalidArguments_Throw()
        {
            MinigameBoardConfig config = Config();
            MinigameSession inProgress = new MinigameSession(Grid(2, 2), config);

            Assert.Throws<ArgumentNullException>(() => MinigameReport.From(null, config, false));
            Assert.Throws<ArgumentException>(() => MinigameReport.From(inProgress, config, false));
        }

        // ---------- 결과와 실패 사유 ----------

        /// <summary>목표 형상을 다 캐면 성공이고, 도구·정찰 사용 횟수를 "예산 - 남은 횟수"로 담는지 검사합니다.</summary>
        [Test]
        public void Succeeded_ReportsUsage()
        {
            MinigameBoardConfig config = Config();
            MinigameSession session = new MinigameSession(Grid(2, 2), config);
            session.UseScoutTool(new Vector2Int(0, 0), out _);
            Tap(session, 0, 0);
            Tap(session, 1, 1);
            Assert.AreEqual(SessionState.Succeeded, session.State);

            PlayReport report = MinigameReport.From(session, config, false);

            Assert.AreEqual(PlayResult.Success, report.Result);
            Assert.IsNull(report.FailReason);
            Assert.AreEqual(2, report.TapsUsed);
            Assert.AreEqual(1, report.ScoutsUsed);
            Assert.AreEqual(0, report.Damage);
            Assert.AreEqual(0, report.BonusJeongseong);
        }

        /// <summary>손상 게이지가 다 차서 실패하면 사유가 손상 100%이고 손상도가 100인지 검사합니다.</summary>
        [Test]
        public void DamageFull_ReportsDamageFull()
        {
            MinigameBoardConfig config = Config(allowedThreatHits: 1);
            MinigameSession session = new MinigameSession(Grid(2, 2, new Vector2Int(0, 0)), config);
            Tap(session, 0, 0);
            Assert.AreEqual(SessionState.Failed, session.State);

            PlayReport report = MinigameReport.From(session, config, false);

            Assert.AreEqual(PlayResult.Failure, report.Result);
            Assert.AreEqual(FailReason.DamageFull, report.FailReason);
            Assert.AreEqual(PlayReport.MaxDamage, report.Damage);
        }

        /// <summary>도구를 다 써서 실패하면 사유가 도구 소진인지 검사합니다.</summary>
        [Test]
        public void ToolsExhausted_ReportsTapsExhausted()
        {
            MinigameBoardConfig config = Config(destructiveToolLimit: 1);
            MinigameSession session = new MinigameSession(Grid(2, 2), config);
            Tap(session, 0, 0);
            Assert.AreEqual(SessionState.Failed, session.State);

            PlayReport report = MinigameReport.From(session, config, false);

            Assert.AreEqual(FailReason.TapsExhausted, report.FailReason);
            Assert.AreEqual(1, report.TapsUsed);
            Assert.AreEqual(0, report.Damage);
        }

        /// <summary>마지막 도구로 손상 100%와 도구 소진이 함께 일어나면 손상 100%로 보는지 검사합니다(기획 확정).</summary>
        [Test]
        public void DamageFullAndToolsExhausted_PrefersDamageFull()
        {
            MinigameBoardConfig config = Config(destructiveToolLimit: 1, allowedThreatHits: 1);
            MinigameSession session = new MinigameSession(Grid(2, 2, new Vector2Int(0, 0)), config);
            Tap(session, 0, 0);
            Assert.AreEqual(0, session.RemainingDestructiveToolUses);

            Assert.AreEqual(FailReason.DamageFull, MinigameReport.From(session, config, false).FailReason);
        }

        /// <summary>진행 중에 포기하면 실패·포기로 보고하는지 검사합니다.</summary>
        [Test]
        public void GaveUp_ReportsGaveUp()
        {
            MinigameBoardConfig config = Config();
            MinigameSession session = new MinigameSession(Grid(2, 2), config);
            Tap(session, 0, 0);

            PlayReport report = MinigameReport.From(session, config, true);

            Assert.AreEqual(PlayResult.Failure, report.Result);
            Assert.AreEqual(FailReason.GaveUp, report.FailReason);
            Assert.AreEqual(1, report.TapsUsed);
        }

        /// <summary>이미 끝난 판은 포기 표시가 있어도 실제 결과로 보고하는지 검사합니다.</summary>
        [Test]
        public void Finished_IgnoresGaveUpFlag()
        {
            MinigameBoardConfig config = Config();
            MinigameSession session = new MinigameSession(Grid(2, 2), config);
            Tap(session, 1, 1);

            Assert.AreEqual(PlayResult.Success, MinigameReport.From(session, config, true).Result);
        }

        /// <summary>성공한 판은 캐낸 정성 보너스 타일 수를 담는지 검사합니다(정성의 양은 서버가 계산).</summary>
        [Test]
        public void Succeeded_ReportsBonusTileCount()
        {
            MinigameBoardConfig config = Config();
            MinigameSession session = new MinigameSession(GridWithBonus(2, 2, new Vector2Int(0, 0)), config);
            Tap(session, 0, 0);
            Tap(session, 1, 1);
            Assert.AreEqual(1, session.RevealedBonusTileCount);

            Assert.AreEqual(1, MinigameReport.From(session, config, false).BonusJeongseong);
        }

        /// <summary>
        /// 실패한 판도 캐낸 보너스 타일 수를 그대로 보고하는지 검사합니다.
        /// 실패한 판에 정성을 주지 않는 것은 서버 정산(RewardCalculator)이 정한다.
        /// </summary>
        [Test]
        public void Failed_StillReportsBonusTileCount()
        {
            MinigameBoardConfig config = Config(destructiveToolLimit: 1);
            MinigameSession session = new MinigameSession(GridWithBonus(2, 2, new Vector2Int(0, 0)), config);
            Tap(session, 0, 0);
            Assert.AreEqual(SessionState.Failed, session.State);

            PlayReport report = MinigameReport.From(session, config, false);

            Assert.AreEqual(PlayResult.Failure, report.Result);
            Assert.AreEqual(1, report.BonusJeongseong);
        }

        /// <summary>포기한 판도 포기 전까지 캐낸 보너스 타일 수를 그대로 보고하는지 검사합니다.</summary>
        [Test]
        public void GaveUp_StillReportsBonusTileCount()
        {
            MinigameBoardConfig config = Config();
            MinigameSession session = new MinigameSession(GridWithBonus(2, 2, new Vector2Int(0, 0)), config);
            Tap(session, 0, 0);

            PlayReport report = MinigameReport.From(session, config, true);

            Assert.AreEqual(FailReason.GaveUp, report.FailReason);
            Assert.AreEqual(1, report.BonusJeongseong);
        }

        // ---------- 손상도 내림 ----------

        /// <summary>손상도는 내림이다. 1/3이면 33%(반올림해도 33), 2/3이면 66%(반올림하면 67)인지 검사합니다.</summary>
        [Test]
        public void Damage_IsFloored()
        {
            MinigameBoardConfig config = Config(allowedThreatHits: 3);
            MinigameSession session = new MinigameSession(Grid(3, 3, new Vector2Int(0, 0), new Vector2Int(1, 0)), config);

            Tap(session, 0, 0);
            Assert.AreEqual(33, MinigameReport.From(session, config, true).Damage);

            Tap(session, 1, 0);
            Assert.AreEqual(66, MinigameReport.From(session, config, true).Damage);
        }

        // ---------- DamagePercent (보고·결과 창·HUD가 함께 쓰는 규칙) ----------

        /// <summary>경계와 내림: 0 → 0, 1 → 100, 1/3 → 33, 2/3 → 66(반올림이면 67), 0.999 → 99(100은 실패한 판에만).</summary>
        [TestCase(0f, 0)]
        [TestCase(1f, 100)]
        [TestCase(1f / 3f, 33)]
        [TestCase(2f / 3f, 66)]
        [TestCase(0.999f, 99)]
        [TestCase(0.005f, 0)]
        public void DamagePercent_FloorsToWholePercent(float gauge, int expected)
        {
            Assert.AreEqual(expected, MinigameReport.DamagePercent(gauge));
        }

        /// <summary>허용 횟수 1~20의 모든 딱 떨어지는 비율(k/n)이 정확한 정수 %(내림)로 나오는지 검사합니다.</summary>
        [Test]
        public void DamagePercent_AllExactRatios_MatchIntegerMath()
        {
            for (int n = 1; n <= 20; n++)
            {
                for (int k = 0; k <= n; k++)
                {
                    float gauge = (float)k / n;
                    Assert.AreEqual(k * 100 / n, MinigameReport.DamagePercent(gauge), $"{k}/{n}");
                }
            }
        }

        /// <summary>보고의 손상도와 같은 규칙인지 검사합니다(결과 창·HUD 숫자가 서버 값과 같아야 함).</summary>
        [Test]
        public void DamagePercent_MatchesReportDamage()
        {
            MinigameBoardConfig config = Config(allowedThreatHits: 3);
            MinigameSession session = new MinigameSession(Grid(3, 3, new Vector2Int(0, 0), new Vector2Int(1, 0)), config);
            Tap(session, 0, 0);
            Tap(session, 1, 0);

            Assert.AreEqual(MinigameReport.DamagePercent(session.DamageGauge), MinigameReport.From(session, config, true).Damage);
        }

        /// <summary>
        /// 딱 떨어지는 비율은 float 오차로 1이 덜 나오지 않는지 검사합니다.
        /// 7/10은 float로 0.69999999…라서, 100을 곱한 뒤 그대로 내리면 69가 될 수 있습니다.
        /// </summary>
        [Test]
        public void Damage_ExactRatio_NotLoweredByFloatError()
        {
            MinigameBoardConfig config = Config(allowedThreatHits: 10);
            Vector2Int[] threats =
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1),
                new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2)
            };
            MinigameSession session = new MinigameSession(Grid(4, 4, threats), config);

            int[] expected = { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
            for (int i = 0; i < threats.Length; i++)
            {
                Tap(session, threats[i].x, threats[i].y);
                Assert.AreEqual(expected[i], MinigameReport.From(session, config, true).Damage, $"위협 {i + 1}/10");
            }
        }
    }
}
