using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Phase-based turn system. Each team gets a phase where they choose one piece
    /// and perform one action (pass, move, or attack), then the turn passes to the
    /// other team. No initiative order — teams alternate phases.
    /// </summary>
    public sealed class TurnSystem
    {
        private readonly List<Piece> _playerPieces;
        private readonly List<Piece> _enemyPieces;
        private Team _currentTeam;
        private Piece _selected;

        public TurnSystem(IEnumerable<Piece> pieces)
        {
            _playerPieces = pieces.Where(p => p.Team == Team.Player).ToList();
            _enemyPieces = pieces.Where(p => p.Team == Team.Enemy).ToList();

            _currentTeam = _playerPieces.Count > 0 ? Team.Player : Team.Enemy;
        }

        /// <summary>All pieces in the system (player + enemy).</summary>
        public IReadOnlyList<Piece> Order => _playerPieces.Concat(_enemyPieces).ToList();
        public int Count => _playerPieces.Count + _enemyPieces.Count;

        /// <summary>Team that owns the current phase.</summary>
        public Team CurrentTeam => _currentTeam;

        /// <summary>The piece currently selected to act (null if none selected yet).</summary>
        public Piece Current
        {
            get { return _selected; }
        }

        /// <summary>
        /// Selects a living piece belonging to the active team. The player (or AI)
        /// chooses which piece to use for this phase's action.
        /// </summary>
        public bool Select(Piece piece)
        {
            if (piece == null || piece.IsDead || piece.Team != _currentTeam)
                return false;

            _selected = piece;
            return true;
        }

        /// <summary>
        /// Advances to the next team's phase. Clears the current selection.
        /// </summary>
        public void Advance()
        {
            _selected = null;

            Team opposingTeam = Other(_currentTeam);
            var opposingPieces = PiecesFor(opposingTeam);

            // If the opposing team has no living pieces, stay on current team
            _currentTeam = opposingPieces.Count > 0 ? opposingTeam : _currentTeam;
        }

        /// <summary>
        /// Removes a piece from the system (e.g., when it dies).
        /// </summary>
        public void Remove(Piece piece)
        {
            if (_selected == piece)
                _selected = null;

            _playerPieces.Remove(piece);
            _enemyPieces.Remove(piece);

            // If current team has no living pieces, pass to opposing team
            if (PiecesFor(_currentTeam).Count == 0)
                _currentTeam = Other(_currentTeam);
        }

        private List<Piece> PiecesFor(Team team) =>
            team == Team.Player ? _playerPieces : _enemyPieces;

        private static Team Other(Team team) =>
            team == Team.Player ? Team.Enemy : Team.Player;
    }
}
