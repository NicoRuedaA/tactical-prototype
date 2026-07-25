using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Alternating-team turn rotation. Each team's pieces act in descending
    /// initiative order; when both teams are alive, turns alternate teams.
    /// </summary>
    public sealed class TurnSystem
    {
        private readonly List<Piece> _order;
        private readonly List<Piece> _playerOrder;
        private readonly List<Piece> _enemyOrder;
        private int _playerIndex;
        private int _enemyIndex;
        private Team _currentTeam;
        private Piece _selected;

        public TurnSystem(IEnumerable<Piece> pieces)
        {
            // OrderByDescending is stable, so equal initiative keeps insertion order.
            _order = pieces.OrderByDescending(p => p.Initiative).ToList();
            _playerOrder = _order.Where(p => p.Team == Team.Player).ToList();
            _enemyOrder = _order.Where(p => p.Team == Team.Enemy).ToList();
            RebuildDisplayOrder();

            _currentTeam = _playerOrder.Count > 0 ? Team.Player : Team.Enemy;
        }

        public IReadOnlyList<Piece> Order => _order;
        public int Count => _order.Count;
        /// <summary>Team that owns the current action opportunity.</summary>
        public Team CurrentTeam => _currentTeam;
        public Piece Current
        {
            get { return _selected; }
        }

        /// <summary>
        /// Selects any living piece belonging to the active team. Selection is
        /// deliberately separate from advancing the turn so the player (or AI)
        /// chooses the actor immediately before submitting its one action.
        /// </summary>
        public bool Select(Piece piece)
        {
            if (piece == null || piece.IsDead || piece.Team != _currentTeam)
                return false;

            var teamOrder = OrderFor(piece.Team);
            int index = teamOrder.IndexOf(piece);
            if (index < 0)
                return false;

            SetIndex(piece.Team, index);
            _selected = piece;
            return true;
        }

        public void Advance()
        {
            if (_order.Count == 0) return;

            Team actingTeam = _currentTeam;
            _selected = null;
            AdvanceTeamIndex(actingTeam);

            Team opposingTeam = Other(actingTeam);
            _currentTeam = OrderFor(opposingTeam).Count > 0
                ? opposingTeam
                : actingTeam;
        }

        public void Remove(Piece piece)
        {
            if (_selected == piece)
                _selected = null;
            _order.Remove(piece);
            RemoveFromTeamOrder(_playerOrder, Team.Player, piece);
            RemoveFromTeamOrder(_enemyOrder, Team.Enemy, piece);

            if (OrderFor(_currentTeam).Count == 0)
                _currentTeam = Other(_currentTeam);
        }

        private void RemoveFromTeamOrder(List<Piece> teamOrder, Team team, Piece piece)
        {
            int removed = teamOrder.IndexOf(piece);
            if (removed < 0) return;

            teamOrder.RemoveAt(removed);
            if (teamOrder.Count == 0)
            {
                SetIndex(team, 0);
                return;
            }

            int index = IndexFor(team);
            if (removed < index) index--;
            if (index >= teamOrder.Count) index = 0;
            SetIndex(team, index);
        }

        private void AdvanceTeamIndex(Team team)
        {
            var teamOrder = OrderFor(team);
            if (teamOrder.Count == 0) return;
            SetIndex(team, (IndexFor(team) + 1) % teamOrder.Count);
        }

        private void RebuildDisplayOrder()
        {
            _order.Clear();
            int player = 0;
            int enemy = 0;
            while (player < _playerOrder.Count || enemy < _enemyOrder.Count)
            {
                if (player < _playerOrder.Count) _order.Add(_playerOrder[player++]);
                if (enemy < _enemyOrder.Count) _order.Add(_enemyOrder[enemy++]);
            }
        }

        private List<Piece> OrderFor(Team team) =>
            team == Team.Player ? _playerOrder : _enemyOrder;

        private int IndexFor(Team team) =>
            team == Team.Player ? _playerIndex : _enemyIndex;

        private void SetIndex(Team team, int index)
        {
            if (team == Team.Player) _playerIndex = index;
            else _enemyIndex = index;
        }

        private static Team Other(Team team) =>
            team == Team.Player ? Team.Enemy : Team.Player;
    }
}
