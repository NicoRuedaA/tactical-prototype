using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Directed graph of MapNodes forming an StS-style map.
    /// Tracks navigation state (visited nodes, last visited position),
    /// validates moves, and can verify graph integrity via DFS.
    /// Pure C# — no Unity dependencies, fully unit-testable.
    /// </summary>
    public sealed class MapGraph
    {
        private readonly Dictionary<string, MapNode> _nodes;

        /// <summary>All nodes in the graph, keyed by Id.</summary>
        public IReadOnlyDictionary<string, MapNode> Nodes => _nodes;

        /// <summary>Id of the start node (first row).</summary>
        public string StartNodeId { get; }

        /// <summary>Id of the boss node (last row).</summary>
        public string BossNodeId { get; }

        /// <summary>
        /// Id of the last visited node. Null when no node has been visited yet
        /// (player has not started the map).
        /// </summary>
        public string LastVisitedNodeId { get; private set; }

        /// <summary>True once the Boss node has been visited.</summary>
        public bool IsComplete =>
            _nodes.TryGetValue(BossNodeId, out var boss) && boss.IsVisited;

        public MapGraph(IEnumerable<MapNode> nodes, string startNodeId, string bossNodeId)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (startNodeId == null)
                throw new ArgumentNullException(nameof(startNodeId));
            if (bossNodeId == null)
                throw new ArgumentNullException(nameof(bossNodeId));

            _nodes = new Dictionary<string, MapNode>();
            foreach (var node in nodes)
            {
                if (node == null)
                    throw new ArgumentException("Node collection contains null entry.", nameof(nodes));
                _nodes.Add(node.Id, node);
            }

            if (!_nodes.ContainsKey(startNodeId))
                throw new ArgumentException($"StartNodeId '{startNodeId}' not found in nodes.", nameof(startNodeId));
            if (!_nodes.ContainsKey(bossNodeId))
                throw new ArgumentException($"BossNodeId '{bossNodeId}' not found in nodes.", nameof(bossNodeId));

            StartNodeId = startNodeId;
            BossNodeId = bossNodeId;
            LastVisitedNodeId = null;
        }

        /// <summary>
        /// Returns unvisited nodes that are directly connected (adjacent) to the
        /// last visited node. When the run has not started (LastVisitedNodeId is null),
        /// returns the start node's connections.
        /// </summary>
        public IReadOnlyList<string> GetAvailableNodes()
        {
            if (LastVisitedNodeId == null)
            {
                // First step: return start node's connections
                return _nodes[StartNodeId].ConnectedNodeIds
                    .Where(id => !_nodes[id].IsVisited)
                    .ToList();
            }

            var current = _nodes[LastVisitedNodeId];
            return current.ConnectedNodeIds
                .Where(id => !_nodes[id].IsVisited)
                .ToList();
        }

        /// <summary>
        /// Visits the node with the given id. Marks it as visited and updates
        /// LastVisitedNodeId.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when nodeId does not exist.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when nodeId is not connected to the last visited node,
        /// or when the node has already been visited.
        /// </exception>
        public void VisitNode(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
                throw new ArgumentException($"Node '{nodeId}' not found in graph.", nameof(nodeId));

            if (node.IsVisited)
                throw new InvalidOperationException($"Node '{nodeId}' has already been visited.");

            if (LastVisitedNodeId != null)
            {
                var current = _nodes[LastVisitedNodeId];
                if (!current.ConnectedNodeIds.Contains(nodeId))
                    throw new InvalidOperationException(
                        $"Node '{nodeId}' is not connected to last visited node '{LastVisitedNodeId}'.");
            }

            node.IsVisited = true;
            LastVisitedNodeId = nodeId;
        }

        /// <summary>
        /// Validates the graph by running DFS from StartNodeId. Verifies that every
        /// leaf node (terminal path) can reach BossNodeId. Returns true if all
        /// paths converge on Boss — i.e., no dead-end branches exist.
        /// </summary>
        public bool Validate()
        {
            var visited = new HashSet<string>();
            return DfsValidate(StartNodeId, visited);
        }

        /// <summary>
        /// DFS traversal that checks every leaf node can reach Boss.
        /// A leaf is a node with zero outgoing connections that is NOT the Boss.
        /// </summary>
        private bool DfsValidate(string nodeId, HashSet<string> visited)
        {
            if (nodeId == BossNodeId)
                return true;

            if (!visited.Add(nodeId))
                return true; // Already checked in this path — cycles are OK

            var node = _nodes[nodeId];
            if (node.ConnectedNodeIds.Count == 0)
                return false; // Leaf that is NOT the Boss → dead end

            foreach (var nextId in node.ConnectedNodeIds)
            {
                if (!DfsValidate(nextId, visited))
                    return false;
            }

            return true;
        }
    }
}
