using System;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Workbench;

namespace Jamkkaebi.Scripts.UI.Shell
{
    /// <summary>작업대의 relicId를 도감 종류 ID(CollectionId)로 바꿔 도감 기록에 등록한다.</summary>
    public sealed class CollectionRecorder
    {
        private readonly CollectionProgress _progress;
        private readonly IWorkbenchRelicCatalog _relics;

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
    }
}