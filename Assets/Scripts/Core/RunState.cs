using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Tracks the player's roster of pieces across an entire run (multiple sequential
    /// combats). HP persists because the same Piece object references are reused.
    /// Pure C# — no Unity dependencies, fully unit-testable.
    /// </summary>
    public sealed class RunState
    {
        private readonly List<Piece> _pieces;

        /// <summary>The player's pieces — same references survive across combats.</summary>
        public IReadOnlyList<Piece> Pieces => _pieces;

        /// <summary>The map graph driving navigation through the run.</summary>
        public MapGraph Graph { get; }

        /// <summary>Id of the last visited map node (delegates to Graph). Null if none.</summary>
        public string LastVisitedNodeId => Graph.LastVisitedNodeId;

        /// <summary>
        /// Primary constructor. Creates a RunState driven by a MapGraph.
        /// </summary>
        /// <param name="playerPieces">Initial player pieces. Must be non-null and non-empty.</param>
        /// <param name="graph">The map graph for this run. Must be non-null and validated.</param>
        /// <exception cref="ArgumentNullException">Thrown when playerPieces or graph is null.</exception>
        /// <exception cref="ArgumentException">Thrown when playerPieces is empty.</exception>
        public RunState(IEnumerable<Piece> playerPieces, MapGraph graph)
        {
            _pieces = playerPieces?.ToList()
                      ?? throw new ArgumentNullException(nameof(playerPieces));

            if (_pieces.Count == 0)
                throw new ArgumentException("Player pieces cannot be empty.", nameof(playerPieces));

            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// Legacy constructor that creates a linear fallback graph.
        /// </summary>
        [Obsolete("Use RunState(IEnumerable<Piece>, MapGraph) instead.")]
        public RunState(IEnumerable<Piece> playerPieces, int totalCombats = 3)
            : this(playerPieces, MakeLinearGraph(totalCombats))
        {
        }

        /// <summary>
        /// Advance combat state. Repurposed from index-based to graph-based:
        /// returns true while the Boss node has not yet been visited.
        /// </summary>
        public bool AdvanceCombat()
        {
            return !Graph.IsComplete;
        }

        /// <summary>True when all player pieces are dead.</summary>
        public bool IsPlayerDead => _pieces.All(p => p.IsDead);

        /// <summary>Returns pieces with Hp > 0 (alive and can fight).</summary>
        public IEnumerable<Piece> GetAlivePlayerPieces() =>
            _pieces.Where(p => !p.IsDead);

        /// <summary>
        /// Visit a map node (delegates to Graph.VisitNode).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown by Graph when the node is not connected or already visited.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when nodeId does not exist in Graph.</exception>
        public void VisitNode(string nodeId) => Graph.VisitNode(nodeId);

        /// <summary>
        /// Returns unvisited nodes adjacent to the last visited node
        /// (delegates to Graph.GetAvailableNodes).
        /// </summary>
        public IReadOnlyList<string> GetAvailableNodes() => Graph.GetAvailableNodes();

        /// <summary>
        /// Adds an ability to the specified piece. Null abilities are silently ignored.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        public void AddAbility(string pieceId, IAbilityData ability)
        {
            if (ability == null) return;
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));
            piece.AddAbility(ability);
        }

        /// <summary>
        /// Applies a flat stat boost to the specified piece.
        /// StatType.MaxHp is NOT in the StatType enum — use ApplyMaxHpBoost for HP.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown stat types.</exception>
        public void ApplyStatBoost(string pieceId, StatType stat, int amount)
        {
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));

            switch (stat)
            {
                case StatType.Damage:
                    piece.AddBonusDamage(amount);
                    break;
                case StatType.AttackRange:
                    piece.AddBonusAttackRange(amount);
                    break;
                case StatType.MoveRange:
                    piece.AddBonusMoveRange(amount);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stat), stat, $"Unknown stat type: {stat}");
            }
        }

        /// <summary>
        /// Increases a piece's max HP and heals by the same amount.
        /// Uses Piece.AddBonusMaxHp which does direct Hp manipulation.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        public void ApplyMaxHpBoost(string pieceId, int amount)
        {
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));
            piece.AddBonusMaxHp(amount);
        }

        /// <summary>Updates a piece's board coordinates for combat placement.</summary>
        /// <exception cref="ArgumentException">Thrown when pieceId is not found.</exception>
        public void PlacePiece(string pieceId, Axial coords)
        {
            var piece = _pieces.FirstOrDefault(p => p.Id == pieceId);
            if (piece == null)
                throw new ArgumentException($"Piece '{pieceId}' not found in run state.", nameof(pieceId));
            piece.Coords = coords;
        }

        /// <summary>
        /// Creates a simple linear fallback graph: start → combat nodes → boss.
        /// Used by the [Obsolete] constructor overload for backward compatibility.
        /// </summary>
        private static MapGraph MakeLinearGraph(int totalCombats)
        {
            var nodes = new List<MapNode>();
            int idCounter = 0;

            var start = new MapNode($"start_{idCounter++}", MapNodeType.Combat, 0, 0);
            nodes.Add(start);
            string prevId = start.Id;

            // Create combat nodes for each combat except the last, which becomes the boss
            for (int i = 0; i < totalCombats - 1; i++)
            {
                var node = new MapNode($"n_{idCounter++}", MapNodeType.Combat, i + 1, 0);
                node.ConnectedNodeIds.Add(prevId); // reverse — not used
                // Connect forward from the previous node
                nodes.Add(node);
                // Update the previous node's connections
                var prevNode = nodes.Find(n => n.Id == prevId);
                prevNode.ConnectedNodeIds.Clear();
                prevNode.ConnectedNodeIds.Add(node.Id);
                prevId = node.Id;
            }

            // Last node is the boss
            var boss = new MapNode($"boss_{idCounter++}", MapNodeType.Boss, totalCombats, 0);
            nodes.Add(boss);
            var lastCombatNode = nodes.Find(n => n.Id == prevId);
            lastCombatNode.ConnectedNodeIds.Clear();
            lastCombatNode.ConnectedNodeIds.Add(boss.Id);

            return new MapGraph(nodes, start.Id, boss.Id);
        }
    }
}
