using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>
    /// 내가 가진 유물 전체. relicId로 찾고, 작업대 슬롯과 정령 목록을 나눠 보여 준다.
    /// </summary>
    /// <remarks>작업대 용량 규칙은 서버 BoxService.requireWorkbenchRoom과 같다.</remarks>
    public sealed class RelicInventory
    {
        /// <summary>서버 BoxProperties.workbenchCapacity 기본값.</summary>
        public const int DefaultWorkbenchCapacity = 3;
        
        private readonly Dictionary<int, OwnedRelic> _relics =  new Dictionary<int, OwnedRelic>();
        private readonly ClarityPolicy _clarityPolicy;

        public int WorkbenchCapacity { get; }

        /// <exception cref="ArgumentNullException">정책이나 목록이 null인 경우.</exception>
        /// <exception cref="ArgumentOutOfRangeException">용량이 1 미만인 경우.</exception>
        /// <exception cref="ArgumentException">목록에 null이나 같은 relicId가 있는 경우.</exception>
        public RelicInventory(ClarityPolicy clarityPolicy, int workbenchCapacity, IEnumerable<OwnedRelic> relics)
        {
            _clarityPolicy = clarityPolicy ?? throw new ArgumentNullException(nameof(clarityPolicy));
            if (workbenchCapacity < 1) throw new ArgumentOutOfRangeException(nameof(workbenchCapacity), "작업대 용량은 1 이상이어야 합니다.");
            if (relics == null) throw new ArgumentNullException(nameof(relics));
            
            WorkbenchCapacity = workbenchCapacity;
            
            // 서버 설정 변경 등의 이유로 진행 중 유물이 용량보다 많아도 받아들인다. 새 뽑기만 막힌다.
            foreach (OwnedRelic relic in relics)
            {
                if (relic == null) throw new ArgumentException("목록에 null 유물이 있습니다.", nameof(relics));
                if (_relics.ContainsKey(relic.RelicId))
                    throw new ArgumentException($"같은 relicId({relic.RelicId})가 두 번 있습니다.", nameof(relics));
                _relics.Add(relic.RelicId, relic);
            }
        }

        /// <summary>선명도 판정과 정령 정렬에 쓰는 정책. 같은 정책을 써야 하는 쪽이 짝을 맞추는 데 쓴다.</summary>
        public ClarityPolicy ClarityPolicy => _clarityPolicy;

        public int Count => _relics.Count;

        /// <summary>가진 유물의 relicId. 순서는 정하지 않는다.</summary>
        public IReadOnlyCollection<int> RelicIds => _relics.Keys;

        public bool Contains(int relicId) => _relics.ContainsKey(relicId);

        /// <summary>relicId로 유물을 찾는다.</summary>
        /// <returns>가지고 있으면 true.</returns>
        public bool TryGet(int relicId, out OwnedRelic relic) => _relics.TryGetValue(relicId, out relic);

        /// <summary>작업대 슬롯을 차지한 유물 수(최초 정화 중인 유물).</summary>
        public int InProgressCount => _relics.Values.Count(r => !r.IsCompleted);

        /// <summary>작업대가 가득 차서 상자를 열 수 없는지.</summary>
        public bool IsWorkbenchFull => InProgressCount >= WorkbenchCapacity;

        /// <summary>작업대 슬롯. 최초 정화 중인 유물을 획득한 순서로, 같으면 relicId 순.</summary>
        public IReadOnlyList<OwnedRelic> WorkbenchSlots()
        {
            return _relics.Values
                .Where(r => !r.IsCompleted)
                .OrderBy(r => r.AcquiredAt)
                .ThenBy(r => r.RelicId)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>정령 목록. 돌봐야 할 순서로(재봉인 → 흐려짐 → 선명), 같으면 relicId 순.</summary>
        public IReadOnlyList<OwnedRelic> SpiritsByCareOrder(DateTime now)
        {
            return _relics.Values
                .Where(r => r.IsCompleted)
                .OrderByDescending(r => _clarityPolicy.ClarityOf(r, now))
                .ThenBy(r => r.RelicId)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>상자에서 나온 유물을 받는다.</summary>
        /// <exception cref="InvalidOperationException">작업대가 가득 찬 경우. 중복이어도 막는다.</exception>
        public AcquireResult Acquire(int relicId, DateTime now)
        {
            if (IsWorkbenchFull) throw new InvalidOperationException("작업대가 가득 차서 상자를 열 수 없습니다.");
            if (_relics.ContainsKey(relicId)) return AcquireResult.Duplicate;
            
            _relics.Add(relicId, OwnedRelic.Acquired(relicId, now));
            return AcquireResult.New;
        }
    }
}