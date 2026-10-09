using UnityEngine;

namespace MobilePrototype.Exhibition
{
    public sealed class HousingPlacement
    {
        public int Id { get; }
        public string ItemId { get; }
        public Vector2Int Cell { get; internal set; }
        public Vector2Int BaseSize { get; }
        public int QuarterTurns { get; internal set; }
        public Vector2Int Size => QuarterTurns % 2 == 0 ? BaseSize : new Vector2Int(BaseSize.y, BaseSize.x);

        public HousingPlacement(int id, string itemId, Vector2Int cell, Vector2Int size)
        { Id = id; ItemId = itemId; Cell = cell; BaseSize = size; }

        public bool Contains(Vector2Int cell) => cell.x >= Cell.x && cell.y >= Cell.y &&
            cell.x < Cell.x + Size.x && cell.y < Cell.y + Size.y;
    }
}
