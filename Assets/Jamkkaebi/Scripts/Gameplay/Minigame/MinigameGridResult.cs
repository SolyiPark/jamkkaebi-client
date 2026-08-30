using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Minigame
{
    public class MinigameGridResult
    {
        public MinigameGrid Grid { get; }
        public IReadOnlyList<int> TargetGroupIds { get; }

        public MinigameGridResult(MinigameGrid grid, IReadOnlyList<int> targetGroupIds)
        {
            Grid = grid;
            TargetGroupIds = targetGroupIds;
        }
    }
}