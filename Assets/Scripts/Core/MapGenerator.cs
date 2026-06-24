using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Procedural StS-style map graph generator.
    /// Produces a layered graph: start node → interior rows → single Boss node.
    /// Each node connects to 1–3 random nodes in the next row. The generator
    /// retries with an incremented seed on invalid graphs (dead ends, <3 paths).
    /// Pure C# — no Unity dependencies, fully unit-testable.
    /// </summary>
    public static class MapGenerator
    {
        private const int MaxRetries = 100;
        private const int MinPaths = 3;

        /// <summary>
        /// Generate a validated MapGraph.
        /// </summary>
        /// <param name="seed">
        /// Random seed (optional — defaults to Environment.TickCount).
        /// </param>
        /// <param name="rows">
        /// Number of interior node rows between start and boss (default 2).
        /// Total layers = 2 + rows (start + rows + boss).
        /// </param>
        /// <param name="nodesPerRow">
        /// Number of nodes per interior row (default 3).
        /// </param>
        /// <returns>A validated MapGraph guaranteed to have at least 3 distinct paths.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when generation fails after MaxRetries attempts.
        /// </exception>
        public static MapGraph Generate(int? seed = null, int rows = 2, int nodesPerRow = 3)
        {
            int actualSeed = seed ?? Environment.TickCount;
            int attempt = 0;

            while (attempt < MaxRetries)
            {
                int currentSeed = actualSeed + attempt;
                try
                {
                    var graph = TryGenerate(currentSeed, rows, nodesPerRow);
                    if (graph.Validate() && CountDistinctPaths(graph) >= MinPaths)
                        return graph;
                }
                catch
                {
                    // Swallow and retry with next seed
                }

                attempt++;
            }

            throw new InvalidOperationException(
                $"MapGenerator failed after {MaxRetries} attempts (seed={seed}, rows={rows}, nodesPerRow={nodesPerRow}).");
        }

        private static MapGraph TryGenerate(int seed, int rows, int nodesPerRow)
        {
            var rng = new Random(seed);
            var nodes = new Dictionary<string, MapNode>();
            int nodeCounter = 0;

            string NextId() => $"node_{nodeCounter++}";

            // ── Build layers ────────────────────────────────────────────
            // Layer 0: start node (single node, Rest or Combat)
            var startType = rng.Next(2) == 0 ? MapNodeType.Rest : MapNodeType.Combat;
            var startNode = new MapNode(NextId(), startType, 0, 0);
            nodes[startNode.Id] = startNode;

            // Layer 1..rows: middle rows (nodesPerRow nodes each)
            var middleLayers = new List<List<MapNode>>();
            for (int row = 1; row <= rows; row++)
            {
                var layer = new List<MapNode>();
                for (int col = 0; col < nodesPerRow; col++)
                {
                    var type = ChooseMiddleNodeType(rng, row, rows);
                    var node = new MapNode(NextId(), type, row, col);
                    layer.Add(node);
                    nodes[node.Id] = node;
                }
                middleLayers.Add(layer);
            }

            // Last layer: boss node (single node)
            var bossNode = new MapNode(NextId(), MapNodeType.Boss, rows + 1, 0);
            nodes[bossNode.Id] = bossNode;

            // ── Connect layers ──────────────────────────────────────────
            // Start → first middle layer
            ConnectToNextLayer(rng, new[] { startNode }, middleLayers[0]);

            // Middle layer → next middle layer or boss
            for (int i = 0; i < middleLayers.Count; i++)
            {
                var currentLayer = middleLayers[i];
                var nextLayer = i < middleLayers.Count - 1
                    ? middleLayers[i + 1]
                    : new List<MapNode> { bossNode };

                ConnectToNextLayer(rng, currentLayer, nextLayer);
            }

            return new MapGraph(nodes.Values, startNode.Id, bossNode.Id);
        }

        /// <summary>
        /// Connects each node in <paramref name="currentLayer"/> to 1–3 random
        /// nodes in <paramref name="nextLayer"/>. Shuffles nextLayer indices using
        /// Fisher-Yates to avoid positional bias.
        /// </summary>
        private static void ConnectToNextLayer(Random rng, IReadOnlyList<MapNode> currentLayer, IReadOnlyList<MapNode> nextLayer)
        {
            var nextIndices = Enumerable.Range(0, nextLayer.Count).ToList();

            foreach (var node in currentLayer)
            {
                // Fisher-Yates shuffle the next layer indices
                for (int i = nextIndices.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (nextIndices[i], nextIndices[j]) = (nextIndices[j], nextIndices[i]);
                }

                // Connect to 1 to min(3, nextLayer.Count) nodes
                int connectionCount = Math.Max(1, rng.Next(1, Math.Min(4, nextLayer.Count + 1)));
                for (int c = 0; c < connectionCount; c++)
                {
                    node.ConnectedNodeIds.Add(nextLayer[nextIndices[c]].Id);
                }
            }
        }

        /// <summary>
        /// Chooses a node type for a middle-row node.
        /// At most one Rest or Shop per row; others are Combat or Elite.
        /// </summary>
        private static MapNodeType ChooseMiddleNodeType(Random rng, int row, int totalRows)
        {
            // Roll: 70% Combat, 15% Elite, 7% Rest, 8% Shop
            // Rest and Shop are limited per row — we handle this via post-generation validation
            // but for simplicity we add random distribution and rely on the count check.
            int roll = rng.Next(100);
            if (roll < 70) return MapNodeType.Combat;
            if (roll < 85) return MapNodeType.Elite;
            if (roll < 92) return MapNodeType.Rest;
            return MapNodeType.Shop;
        }

        /// <summary>
        /// Counts distinct paths from StartNodeId to BossNodeId via DFS enumeration.
        /// </summary>
        private static int CountDistinctPaths(MapGraph graph)
        {
            var visited = new HashSet<string>();
            int count = 0;

            void Dfs(string currentId)
            {
                if (currentId == graph.BossNodeId)
                {
                    count++;
                    return;
                }

                visited.Add(currentId);
                foreach (var nextId in graph.Nodes[currentId].ConnectedNodeIds)
                {
                    if (!visited.Contains(nextId))
                        Dfs(nextId);
                }
                visited.Remove(currentId);
            }

            Dfs(graph.StartNodeId);
            return count;
        }
    }
}
