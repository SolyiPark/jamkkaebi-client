namespace Jamkkaebi.Scripts.Gameplay.Minigame.Core
{
    public class Tile
    {
        public TileContent Content { get; }
        public bool IsRevealed { get; private set; }
        public bool IsReinforced { get; private set; }

        public Tile(TileContent content, bool isReinforced = false)
        {
            Content = content;
            IsRevealed = false;
            IsReinforced = isReinforced;
        }

        public RevealOutcome Reveal()
        {
            if (IsRevealed) return RevealOutcome.AlreadyRevealed;
            if (IsReinforced)
            {
                IsReinforced = false;
                return RevealOutcome.ReinforcementConsumed;
            }
            
            IsRevealed = true;
            return RevealOutcome.Revealed;
        }

        public void Reinforce()
        {
            IsReinforced = true;
        }
    }
}