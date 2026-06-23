using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The hex board: the source of truth for terrain (tiles) and occupancy (pieces).
    /// Keeping occupancy here — not on the pieces — avoids two places disagreeing.
    /// </summary>
    public sealed class Board
    {
        private readonly Dictionary<Axial, Tile> _tiles = new Dictionary<Axial, Tile>();
        private readonly Dictionary<Axial, Piece> _occupants = new Dictionary<Axial, Piece>();

        public IReadOnlyCollection<Tile> Tiles => _tiles.Values;

        public void AddTile(Tile tile) => _tiles[tile.Coords] = tile;

        public bool Contains(Axial c) => _tiles.ContainsKey(c);

        public Tile GetTile(Axial c) => _tiles.TryGetValue(c, out var t) ? t : null;

        public bool IsWalkable(Axial c) => _tiles.TryGetValue(c, out var t) && t.Walkable;

        /// <summary>Neighbouring coordinates that actually exist on this board.</summary>
        public IEnumerable<Axial> Neighbors(Axial c)
        {
            foreach (var n in c.Neighbors())
                if (_tiles.ContainsKey(n))
                    yield return n;
        }

        public bool IsOccupied(Axial c) => _occupants.ContainsKey(c);

        public Piece OccupantAt(Axial c) => _occupants.TryGetValue(c, out var p) ? p : null;

        public void Place(Piece piece, Axial c)
        {
            _occupants[c] = piece;
            piece.Coords = c;
        }

        public void MovePiece(Piece piece, Axial dest)
        {
            _occupants.Remove(piece.Coords);
            _occupants[dest] = piece;
            piece.Coords = dest;
        }

        public void RemovePiece(Piece piece) => _occupants.Remove(piece.Coords);

        /// <summary>Builds a simple rectangular axial board, all tiles walkable.</summary>
        public static Board CreateRectangle(int width, int height)
        {
            var board = new Board();
            for (int r = 0; r < height; r++)
                for (int q = 0; q < width; q++)
                    board.AddTile(new Tile(new Axial(q, r)));
            return board;
        }
    }
}
