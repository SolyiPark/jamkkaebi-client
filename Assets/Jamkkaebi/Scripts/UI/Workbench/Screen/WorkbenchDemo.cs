using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 서버 연결 전 작업대 화면을 띄우는 모의 백엔드 구성. 정령 목록이 보이도록 정령 세 마리로 시작한다.
    /// 상태는 Play 세션 동안 메모리에만 있다.
    /// </summary>
    public static class WorkbenchDemo
    {
        public static MockWorkbenchBackend CreateBackend(Func<DateTime> clock, float delaySeconds = 0.3f)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));

            DateTime now = clock();
            OwnedRelic[] relics =
            {
                Spirit(2, now - TimeSpan.FromDays(3)),   // 선명
                Spirit(8, now - TimeSpan.FromDays(10)),  // 흐려짐
                Spirit(9, now - TimeSpan.FromDays(20))   // 재봉인
            };

            RelicInventory inventory = new RelicInventory(ClarityPolicy.Default, RelicInventory.DefaultWorkbenchCapacity, relics);
            PlayPlanner planner = new PlayPlanner(ClarityPolicy.Default, RestorePolicy.Default, MinigameDifficulty.Normal);
            return new MockWorkbenchBackend(inventory, planner, WorkbenchRelicSeed.Instance.Count, DefaultSpiritName,
                new Random(), clock, delaySeconds);
        }

        public static string DefaultSpiritName(int relicId) =>
            WorkbenchRelicSeed.Instance.TryFind(relicId, out WorkbenchRelicInfo info) ? info.DefaultSpiritName : WorkbenchText.UnknownRelic;

        private static OwnedRelic Spirit(int relicId, DateTime lastPurifiedAt) =>
            new OwnedRelic(relicId, lastPurifiedAt - TimeSpan.FromDays(1), RelicStatus.Completed, Phase.Guard,
                lastPurifiedAt, null, null, 0);
    }
}
