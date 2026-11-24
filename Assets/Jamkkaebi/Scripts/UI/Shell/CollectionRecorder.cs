using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Workbench;

namespace Jamkkaebi.Scripts.UI.Shell
{
    /// <summary>작업대의 relicId를 도감 종류 ID(CollectionId)로 바꿔 도감 기록에 등록한다.</summary>
    public sealed class CollectionRecorder
    {
        private readonly CollectionProgress _progress;
        private readonly IWorkbenchRelicCatalog _relics;
        private bool _hasBaseline;

        public CollectionRecorder(CollectionProgress progress, IWorkbenchRelicCatalog relics)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (relics == null) throw new ArgumentNullException(nameof(relics));

            _progress = progress;
            _relics = relics;
        }

        /// <summary>최초 정화를 모두 마친 유물을 도감에 등록한다.</summary>
        /// <param name="alreadySeen">true면 신규(N) 표시 없이 확인한 상태로 등록한다(시작할 때 이미 완료돼 있던 정령).</param>
        /// <returns>이번에 새로 등록했으면 true. 모르는 유물이거나 이미 등록돼 있으면 false.</returns>
        public bool RegisterCompleted(int relicId, bool alreadySeen)
        {
            if (!_relics.TryFind(relicId, out WorkbenchRelicInfo relic)) return false;

            string collectionId = relic.CollectionId;

            if (_progress.RegisterRestorationCompleted(collectionId))
            {
                if (alreadySeen) _progress.MarkViewed(collectionId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 작업대 조회 결과의 정령(최초 정화를 마친 유물) 목록으로 도감에 빠진 등록을 채운다.
        /// 처음 받은 목록은 시작 상태로 보고 확인한 상태로 등록하고, 그 뒤 목록에서 새로 보이는 정령은 신규(N)로 등록한다.
        /// </summary>
        /// <remarks>
        /// 정산 응답을 잃은 채 다른 화면으로 나가면 <see cref="RegisterCompleted"/>가 불리지 않는다.
        /// 백엔드(서버)의 정령 목록이 기준이므로, 작업대를 다시 조회할 때마다 이 메서드로 누락을 메운다.
        /// 시작 시 채우기와 작업대 홈 조회 중 어느 응답이 먼저 와도 먼저 온 쪽이 시작 상태가 된다.
        /// </remarks>
        /// <returns>이번에 새로 등록한 유물 수.</returns>
        public int SyncCompleted(IEnumerable<int> completedRelicIds)
        {
            if (completedRelicIds == null) throw new ArgumentNullException(nameof(completedRelicIds));

            bool alreadySeen = !_hasBaseline;
            int registered = 0;
            foreach (int relicId in completedRelicIds)
            {
                if (RegisterCompleted(relicId, alreadySeen)) registered++;
            }

            _hasBaseline = true;
            return registered;
        }
    }
}