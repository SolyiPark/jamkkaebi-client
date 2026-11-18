using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 서버 없이 작업대를 돌려 보는 모의 백엔드. 상태는 메모리에만 둔다.
    /// 서버의 멱등 규칙(requestId, sessionToken)과 세션 대체 규칙을 흉내 낸다.
    /// </summary>
    public sealed class MockWorkbenchBackend : IWorkbenchBackend
    {
        // 서버 application.yml 기본값과 같은 임시 수치
        public const int DuplicateJeongseong = 10;
        public const int DuplicateCrystal = 3;
        public const int PurchasePrice = 50;

        private readonly RelicInventory _inventory;
        private readonly PlayPlanner _planner;
        private readonly PurificationSettlement _settlement;
        private readonly int _totalRelics;
        private readonly System.Random _random;
        private readonly Func<DateTime> _clock;
        private readonly float _delaySeconds;

        // requestId → 그 탭에서 처음 연 결과
        private readonly Dictionary<string, BoxOpenOutcome> _openedBoxes = new();
        // sessionToken → 시작했지만 아직 보고하지 않은 판
        private readonly Dictionary<string, PendingPlay> _pending = new();
        // sessionToken → 이미 정산한 판
        private readonly Dictionary<string, SettledPlay> _settled = new();

        /// <param name="totalRelics">전체 유물 수. relicId는 1..totalRelics에서 뽑는다.</param>
        /// <param name="clock">지금 시각. 테스트에서 시간을 움직이려고 주입한다.</param>
        /// <param name="delaySeconds">가짜 네트워크 지연. 테스트에서는 0.</param>
        public MockWorkbenchBackend(RelicInventory inventory, PlayPlanner planner, int totalRelics,
            System.Random random, Func<DateTime> clock, float delaySeconds = 0.3f)
        {
            if (inventory  == null) throw new ArgumentNullException(nameof(inventory));
            if (planner  == null) throw new ArgumentNullException(nameof(planner));
            if (totalRelics <= 0) throw new ArgumentOutOfRangeException(nameof(totalRelics));
            if (inventory.Count > totalRelics) throw new ArgumentException("인벤토리의 유물 개수가 전체 유물 개수보다 많습니다.", nameof(totalRelics));
            if (random  == null) throw new ArgumentNullException(nameof(random));
            if (clock  == null) throw new ArgumentNullException(nameof(clock));
            if (delaySeconds < 0f) throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            
            _inventory = inventory;
            _planner = planner;
            _settlement = new PurificationSettlement(planner);
            _totalRelics = totalRelics;
            _random  = random;
            _clock = clock;
            _delaySeconds = delaySeconds;
        }

        public IEnumerator GetWorkbench(Action<WorkbenchSnapshot> completed, Action<string> failed)
        {
            // 서버까지 다녀오는 시간 흉내내기
            yield return Delay();

            DateTime now = _clock();
            IReadOnlyList<OwnedRelic> slotRelics = _inventory.WorkbenchSlots();
            IReadOnlyList<OwnedRelic> ownedSpirits = _inventory.SpiritsByCareOrder(now);
            IEnumerable<WorkbenchSlot> slots =
                slotRelics.Select(r => new WorkbenchSlot(r.RelicId, r.RevealLevel, r.CurrentPhase));
            IEnumerable<WorkbenchSpirit> spirits = ownedSpirits.Select(s =>
                new WorkbenchSpirit(s.RelicId, _planner.ClarityPolicy.ClarityOf(s.LastPurifiedAt.Value, now),
                    s.CareCount));

            WorkbenchSnapshot snapshot;
            try
            {
                snapshot = new WorkbenchSnapshot(_inventory.WorkbenchCapacity, slots, spirits,
                    _totalRelics - _inventory.Count);
            }
            catch (Exception e)
            {
                failed(e.Message);
                yield break;
            }
            
            completed(snapshot);
        }

        public IEnumerator GetRelicDetail(int relicId, Action<RelicDetail> completed, Action<string> failed)
        {
            // 서버까지 다녀오는 시간 흉내내기
            yield return Delay();

            if (!_inventory.TryGet(relicId, out OwnedRelic relic))
            {
                failed($"가진 유물이 아니에요.");
                yield break;
            }
            
            DateTime now = _clock();
            
            // 선명도와 재봉인 시각은 최초 정화를 모두 마친 도깨비에게만 있다.
            // 완료 상태면 OwnedRelic이 LastPurifiedAt을 보장하므로 .Value가 안전하다.
            Clarity? clarity = null;
            DateTime? resealAt = null;
            // 완료한 경우
            if (relic.IsCompleted)
            {
                DateTime lastPurifiedAt = relic.LastPurifiedAt.Value;
                clarity = _planner.ClarityPolicy.ClarityOf(lastPurifiedAt, now);
                resealAt = _planner.ClarityPolicy.ResealAt(lastPurifiedAt, now);
            }

            RelicDetail detail = new RelicDetail(relic.RelicId, relic.Status, clarity, resealAt, relic.CareCount,
                _planner.PlanFor(relic, now));
            
            completed(detail);
        }

        public IEnumerator OpenBox(BoxSource source, string requestId,
            Action<BoxOpenOutcome> completed, Action<string> failed)
        {
            yield return Delay();

            if (!Enum.IsDefined(typeof(BoxSource), source))
            {
                failed("상자를 열 수 없어요.");
                yield break;
            }
            if (string.IsNullOrEmpty(requestId))
            {
                failed("상자를 열 수 없어요.");
                yield break;
            }
            
            if (_openedBoxes.TryGetValue(requestId, out BoxOpenOutcome cached))
            {
                completed(cached);
                yield break;
            }

            if (_inventory.IsWorkbenchFull)
            {
                failed("작업대가 가득 찼어요. 진행 중인 정화를 먼저 완료해주세요.");
                yield break;
            }

            DateTime now = _clock();
            int acquiredRelicId = _random.Next(1, _totalRelics + 1);
            AcquireResult result = _inventory.Acquire(acquiredRelicId, now);
            
            int jGained = result == AcquireResult.Duplicate ? DuplicateJeongseong : 0;
            int cGained = result == AcquireResult.Duplicate ? DuplicateCrystal : 0;
            int jSpent = source == BoxSource.Purchase ? PurchasePrice : 0;
            
            BoxOpenOutcome outcome = new BoxOpenOutcome(result, acquiredRelicId, jGained, cGained, jSpent);
            _openedBoxes.Add(requestId, outcome);
            
            completed(outcome);
        }

        public IEnumerator StartPurification(int relicId, MinigameDifficulty? difficulty,
            Action<PurificationSession> completed, Action<string> failed)
        {
            yield return Delay();

            if (!_inventory.TryGet(relicId, out OwnedRelic relic))
            {
                failed("가진 유물이 아니에요.");
                yield break;
            }
            
            DateTime now = _clock();
            PlayPlan plan = _planner.PlanFor(relic, now);
            MinigameDifficulty chosen;

            if (!difficulty.HasValue)
            {
                if (plan.DifficultySelectable)
                {
                    failed("난이도를 골라 주세요.");
                    yield break;
                }

                chosen = plan.AllowedDifficulties[0];
            }
            else
            {
                if (plan.Allows(difficulty.Value))
                {
                    chosen = difficulty.Value;
                }
                else
                {
                    failed("이 난이도는 플레이 할 수 없어요.");
                    yield break;
                }
            }

            IReadOnlyList<string> targets = _pending
                .Where(p => p.Value.RelicId == relicId)
                .Select(p => p.Key)
                .ToList();

            foreach (string t in targets)
            {
                _pending.Remove(t);
            }

            string token = Guid.NewGuid().ToString();
            PendingPlay play = new PendingPlay(relicId, plan, chosen);
            
            _pending.Add(token, play);

            completed(new PurificationSession(token, relicId, plan.Route, plan.Phase, chosen));
        }

        public IEnumerator ReportPurification(string sessionToken, PlayResult result,
            Action<SettlementResult> completed, Action<string> failed)
        {
            yield return Delay();

            if (string.IsNullOrEmpty(sessionToken))
            {
                failed("결과를 확인할 수 없어요.");
                yield break;
            }
            if (!Enum.IsDefined(typeof(PlayResult), result))
            {
                failed("결과를 확인할 수 없어요.");
                yield break;
            }

            if (_settled.TryGetValue(sessionToken, out SettledPlay settled))
            {
                if (settled.Result == result)
                {
                    completed(settled.Settlement);
                    yield break;
                }
                else
                {
                    failed("이미 결과가 기록된 판이에요.");
                    yield break;
                }
            }

            if (!_pending.TryGetValue(sessionToken, out PendingPlay pending))
            {
                failed("결과를 확인할 수 없어요.");
                yield break;
            }

            if (!_inventory.TryGet(pending.RelicId, out OwnedRelic relic))
            {
                failed("유물을 찾을 수 없어요.");
                yield break;
            }
            DateTime now = _clock();
            
            SettlementResult settlement = _settlement.Settle(relic, pending.Plan, pending.Difficulty, result, now);
            SettledPlay play = new SettledPlay(result, settlement);
            
            _pending.Remove(sessionToken);
            _settled.Add(sessionToken, play);

            completed(settlement);
        }

        private IEnumerator Delay()
        {
            if (_delaySeconds > 0f) yield return new WaitForSecondsRealtime(_delaySeconds);
        }

        /// <summary>시작했지만 아직 보고하지 않은 판. 시작 시점의 계획을 들고 있는다.</summary>
        private sealed class PendingPlay
        {
            public int RelicId { get; }
            public PlayPlan Plan { get; }
            public MinigameDifficulty Difficulty { get; }

            public PendingPlay(int relicId, PlayPlan plan, MinigameDifficulty difficulty)
            {
                RelicId = relicId;
                Plan = plan;
                Difficulty = difficulty;
            }
        }

        /// <summary>이미 정산한 판. 같은 보고가 다시 오면 Settlement를 그대로 돌려준다.</summary>
        private sealed class SettledPlay
        {
            public PlayResult Result { get; }
            public SettlementResult Settlement { get; }

            public SettledPlay(PlayResult result, SettlementResult settlement)
            {
                Result = result;
                Settlement = settlement;
            }
        }
    }
}