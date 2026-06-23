using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Cost-aware BFS over the board, adapted from DnDHexCombat's GraphSearch.
    /// Returns the tiles reachable within a movement budget and supports path
    /// reconstruction. Obstacles and occupied tiles block movement.
    /// </summary>
    public static class Pathfinding
    {
        public static BfsResult GetReachable(Board board, Axial start, int movePoints)
        {
            var cameFrom = new Dictionary<Axial, Axial?> { [start] = null };
            var costSoFar = new Dictionary<Axial, int> { [start] = 0 };
            var frontier = new Queue<Axial>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                foreach (var next in board.Neighbors(current))
                {
                    var tile = board.GetTile(next);
                    if (!tile.Walkable) continue;
                    // Units block movement: you cannot path through an occupied tile.
                    if (board.IsOccupied(next)) continue;

                    int newCost = costSoFar[current] + tile.MoveCost;
                    if (newCost > movePoints) continue;

                    if (!costSoFar.ContainsKey(next))
                    {
                        costSoFar[next] = newCost;
                        cameFrom[next] = current;
                        frontier.Enqueue(next);
                    }
                    else if (newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        cameFrom[next] = current;
                    }
                }
            }

            cameFrom.Remove(start); // the start tile is not a reachable destination
            return new BfsResult(cameFrom);
        }
    }

    /// <summary>The reachable set produced by a BFS, plus path lookups.</summary>
    public readonly struct BfsResult
    {
        private readonly Dictionary<Axial, Axial?> _cameFrom;

        public BfsResult(Dictionary<Axial, Axial?> cameFrom) => _cameFrom = cameFrom;

        public IEnumerable<Axial> ReachableTiles =>
            _cameFrom != null ? _cameFrom.Keys : System.Array.Empty<Axial>();

        public bool CanReach(Axial dest) => _cameFrom != null && _cameFrom.ContainsKey(dest);

        /// <summary>Full path from origin to destination, inclusive. Empty if unreachable.</summary>
        public List<Axial> PathTo(Axial dest)
        {
            var path = new List<Axial>();
            if (_cameFrom == null || !_cameFrom.ContainsKey(dest)) return path;

            Axial? current = dest;
            while (current != null)
            {
                path.Add(current.Value);
                current = _cameFrom.TryGetValue(current.Value, out var prev) ? prev : null;
            }
            path.Reverse();
            return path;
        }
    }
}
