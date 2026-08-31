namespace Jamkkaebi.Scripts.Gameplay.Minigame
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

        public void Reveal()
        {
            if (IsReinforced)
            {
                IsReinforced = false;
                return;
            }
            
            IsRevealed = true;
        }

        public void Reinforce()
        {
            IsReinforced = true;
        }
    }
}