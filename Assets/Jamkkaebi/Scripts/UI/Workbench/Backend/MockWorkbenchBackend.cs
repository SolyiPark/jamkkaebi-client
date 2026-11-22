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
        private readonly IWorkbenchRelicCatalog _catalog;
        private readonly System.Random _random;
        private readonly Func<DateTime> _clock;
        private readonly float _delaySeconds;

        // requestId → 그 탭에서 처음 연 결과
        private readonly Dictionary<string, BoxOpenOutcome> _openedBoxes = new();
        // sessionToken → 시작했지만 아직 보고하지 않은 판
        private readonly Dictionary<string, PendingPlay> _pending = new();
        // sessionToken → 이미 정산한 판
        private readonly Dictionary<string, SettledPlay> _settled = new();
        // relicId → 사용자가 지은 정령 이름. 없으면 기본 이름을 쓴다.
        private readonly Dictionary<int, string> _customNames = new();
        private int _dropReportResponses;
        
        /// <summary>
        /// 개발용. 0보다 크면 보고를 정산한 뒤 응답만 실패로 돌려준다(응답 유실 흉내). 한 번 흉내 낼 때마다 1 줄어든다.
        /// 재전송이 같은 정산을 받는지 확인할 때 쓴다.
        /// </summary>
        public int DropReportResponses
        {
            get => _dropReportResponses;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _dropReportResponses = value;
            }
        }

        /// <param name="totalRelics">전체 유물 수. relicId는 1..totalRelics에서 뽑는다.</param>
        /// <param name="catalog">relic ID로 유물 기본 이름과 봉인 매개체를 찾는 카탈로그</param>
        /// <param name="clock">지금 시각. 테스트에서 시간을 움직이려고 주입한다.</param>
        /// <param name="delaySeconds">가짜 네트워크 지연. 테스트에서는 0.</param>
        public MockWorkbenchBackend(RelicInventory inventory, PlayPlanner planner, int totalRelics,
            IWorkbenchRelicCatalog catalog, System.Random random, Func<DateTime> clock, float delaySeconds = 0.3f)
        {
            if (inventory  == null) throw new ArgumentNullException(nameof(inventory));
            if (planner  == null) throw new ArgumentNullException(nameof(planner));
            if (totalRelics <= 0) throw new ArgumentOutOfRangeException(nameof(totalRelics));
            // 가진 유물이 모두 뽑기 범위 안에 있어야 lockedCount(전체 - 보유)가 음수가 되지 않는다.
            // relicId는 OwnedRelic이 1 이상을 보장하므로 위쪽 경계만 본다. 0은 "범위 밖 없음"이다.
            int outOfRange = inventory.RelicIds.FirstOrDefault(id => id > totalRelics);
            if (outOfRange != 0)
                throw new ArgumentException($"relicId {outOfRange}가 뽑기 범위 1..{totalRelics} 밖에 있습니다.", nameof(inventory));
            // 정령 정렬(인벤토리)과 응답의 선명도(계획기)가 같은 기준을 쓰도록 짝을 맞춘다.
            if (!ReferenceEquals(inventory.ClarityPolicy, planner.ClarityPolicy))
                throw new ArgumentException("인벤토리와 계획기는 같은 선명도 정책을 써야 합니다.", nameof(planner));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (random  == null) throw new ArgumentNullException(nameof(random));
            if (clock  == null) throw new ArgumentNullException(nameof(clock));
            if (delaySeconds < 0f) throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            
            _inventory = inventory;
            _planner = planner;
            _settlement = new PurificationSettlement(planner);
            _totalRelics = totalRelics;
            _catalog = catalog;
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
                new WorkbenchSpirit(s.RelicId, SpiritNameOf(s.RelicId), _planner.ClarityPolicy.ClarityOf(s, now),
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
            
            // 선명도와 다음 변화 시각은 최초 정화를 모두 마친 도깨비에게만 있다.
            // 계획·목록과 같은 계산(기기 시각 역행 처리 포함)을 쓴다.
            Clarity? clarity = null;
            DateTime? changesAt = null;
            if (relic.IsCompleted)
            {
                clarity = _planner.ClarityPolicy.ClarityOf(relic, now);
                changesAt = _planner.ClarityPolicy.NextChangeAt(relic, now);
            }

            // 정령 이름은 최초 정화를 마친 뒤에만 공개한다(서버 RelicCard와 같음).
            string spiritName = relic.IsCompleted ? SpiritNameOf(relic.RelicId) : null;

            RelicDetail detail;
            try
            {
                detail = new RelicDetail(relic.RelicId, relic.Status, spiritName, clarity, relic.LastPurifiedAt,
                    changesAt, relic.CareCount, _planner.PlanFor(relic, now));
            }
            catch (Exception e)
            {
                // 기본 이름 함수가 빈 이름을 주는 등 응답을 만들 수 없으면 실패로 알린다.
                failed(e.Message);
                yield break;
            }
            
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

            if (!_catalog.TryFind(relicId, out WorkbenchRelicInfo info))
            {
                failed($"{WorkbenchText.UnknownRelic}");
                yield break;
            }

            SealMedium medium = info.SealMedium;

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

            long seed = _random.Next();

            completed(new PurificationSession(token, relicId, plan.Route, plan.Phase, chosen, medium, seed));
        }

        public IEnumerator ReportPurification(string sessionToken, PlayReport report,
            Action<SettlementResult> completed, Action<string> failed)
        {
            yield return Delay();

            if (string.IsNullOrEmpty(sessionToken))
            {
                failed("결과를 확인할 수 없어요.");
                yield break;
            }
            if (report == null)
            {
                failed("결과를 확인할 수 없어요.");
                yield break;
            }

            if (_settled.TryGetValue(sessionToken, out SettledPlay settled))
            {
                if (settled.Report.SameAs(report))
                {
                    if (TryDropResponse(failed)) yield break;
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
            
            SettlementResult settlement = _settlement.Settle(relic, pending.Plan, pending.Difficulty, report.Result, now);
            SettledPlay play = new SettledPlay(report, settlement);
            
            _pending.Remove(sessionToken);
            _settled.Add(sessionToken, play);

            if (TryDropResponse(failed)) yield break;
            completed(settlement);
        }

        public IEnumerator RenameSpirit(int relicId, string name, Action<string> completed, Action<string> failed)
        {
            yield return Delay();

            if (!_inventory.TryGet(relicId, out OwnedRelic relic))
            {
                failed("가진 유물이 아니에요.");
                yield break;
            }
            // 이름은 최초 정화를 마친 정령에게만 있다(서버 requireCompleted).
            if (!relic.IsCompleted)
            {
                failed("정화를 모두 마친 정령만 이름을 지을 수 있어요.");
                yield break;
            }
            if (!SpiritNameRule.TryNormalize(name, out string normalized, out string error))
            {
                failed(error);
                yield break;
            }

            if (normalized == null) _customNames.Remove(relicId);
            else _customNames[relicId] = normalized;

            completed(SpiritNameOf(relicId));
        }

        /// <summary>서버 spiritNameOf와 같다. 사용자 이름이 있으면 그것, 없으면 기본 이름. 찾지 못하면 작업대 고정 문구를 돌려준다.</summary>
        private string SpiritNameOf(int relicId)
        {
            if (_customNames.TryGetValue(relicId, out string custom)) return custom;
            return (_catalog.TryFind(relicId, out WorkbenchRelicInfo relic)
                ? relic.DefaultSpiritName
                : WorkbenchText.UnknownRelic);
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
            public PlayReport Report { get; }
            public SettlementResult Settlement { get; }

            public SettledPlay(PlayReport report, SettlementResult settlement)
            {
                Report = report;
                Settlement = settlement;
            }
        }

        /// <summary>DropReportResponses 횟수가 남아 있으면 횟수를 1만큼 줄이고 true. 횟수가 0이면 false.</summary>
        private bool TryDropResponse(Action<string> failed)
        {
            if (DropReportResponses == 0) return false;
            DropReportResponses -= 1;
            failed("응답을 받지 못했어요. (모의 응답 유실)");
            return true;
        }
    }
}