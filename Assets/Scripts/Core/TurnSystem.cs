using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Initiative-ordered turn rotation. Higher initiative acts first.
    /// Dead pieces are removed so the cycle only contains living units, and the
    /// index is fixed up on removal so "current" never skips or repeats a unit.
    /// </summary>
    public sealed class TurnSystem
    {
        private readonly List<Piece> _order;
        private int _index;

        public TurnSystem(IEnumerable<Piece> pieces)
        {
            // OrderByDescending is stable, so equal initiative keeps insertion order.
            _order = pieces.OrderByDescending(p => p.Initiative).ToList();
            _index = 0;
        }

        public IReadOnlyList<Piece> Order => _order;
        public int Count => _order.Count;
        public Piece Current => _order.Count > 0 ? _order[_index] : null;

        public void Advance()
        {
            if (_order.Count == 0) return;
            _index = (_index + 1) % _order.Count;
        }

        public void Remove(Piece piece)
        {
            int removed = _order.IndexOf(piece);
            if (removed < 0) return;

            _order.RemoveAt(removed);
            if (_order.Count == 0) { _index = 0; return; }

            // Keep the index pointing at the same logical "current" unit.
            if (removed < _index) _index--;
            if (_index >= _order.Count) _index = 0;
        }
    }
}
