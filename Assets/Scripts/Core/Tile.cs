namespace Game.Core
{
    /// <summary>
    /// A single board cell. Holds terrain only — occupancy is tracked by the Board,
    /// so a tile never needs to know which piece is standing on it.
    /// </summary>
    public sealed class Tile
    {
        public Axial Coords { get; }
        public bool Walkable { get; set; }
        public int MoveCost { get; set; }

        public Tile(Axial coords, bool walkable = true, int moveCost = 1)
        {
            Coords = coords;
            Walkable = walkable;
            MoveCost = moveCost < 1 ? 1 : moveCost;
        }
    }
}
