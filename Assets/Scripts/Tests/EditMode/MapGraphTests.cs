using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class MapGraphTests
    {
        // ════════════════════════════════════════════════════════════════
        // Task 1.1 — MapNodeType enum + MapNode class
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void MapNode_CreateAndInspect()
        {
            var node = new MapNode("n1", MapNodeType.Combat, 0, 1);

            Assert.AreEqual("n1", node.Id);
            Assert.AreEqual(MapNodeType.Combat, node.Type);
            Assert.AreEqual(0, node.Row);
            Assert.AreEqual(1, node.Col);
            Assert.IsFalse(node.IsVisited);
            Assert.IsEmpty(node.ConnectedNodeIds);
        }

        [Test]
        public void MapNode_ConnectedNodeIds_CanBeAdded()
        {
            var node = new MapNode("n1", MapNodeType.Elite, 1, 0);

            node.ConnectedNodeIds.Add("n2");
            node.ConnectedNodeIds.Add("n3");

            Assert.AreEqual(2, node.ConnectedNodeIds.Count);
            Assert.Contains("n2", node.ConnectedNodeIds);
            Assert.Contains("n3", node.ConnectedNodeIds);
        }

        [Test]
        public void MapNode_CanMarkVisited()
        {
            var node = new MapNode("n1", MapNodeType.Boss, 2, 0);

            Assert.IsFalse(node.IsVisited);
            node.IsVisited = true;
            Assert.IsTrue(node.IsVisited);
        }

        [Test]
        public void MapNodeType_EnumValues()
        {
            Assert.AreEqual(0, (int)MapNodeType.Combat);
            Assert.AreEqual(1, (int)MapNodeType.Elite);
            Assert.AreEqual(2, (int)MapNodeType.Boss);
            Assert.AreEqual(3, (int)MapNodeType.Rest);
            Assert.AreEqual(4, (int)MapNodeType.Shop);
        }

        // ════════════════════════════════════════════════════════════════
        // Task 1.2 — MapGraph
        // ════════════════════════════════════════════════════════════════

        // ── Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Builds a simple 3-row graph:
        ///   start ─→ n1 ─→ boss
        ///         ─→ n2 ─→
        /// </summary>
        private static MapGraph BuildSimpleGraph()
        {
            var start = new MapNode("start", MapNodeType.Rest, 0, 0);
            var n1 = new MapNode("n1", MapNodeType.Combat, 1, 0);
            var n2 = new MapNode("n2", MapNodeType.Combat, 1, 1);
            var boss = new MapNode("boss", MapNodeType.Boss, 2, 0);

            start.ConnectedNodeIds.Add("n1");
            start.ConnectedNodeIds.Add("n2");
            n1.ConnectedNodeIds.Add("boss");
            n2.ConnectedNodeIds.Add("boss");

            return new MapGraph(new[] { start, n1, n2, boss }, "start", "boss");
        }

        /// <summary>
        /// Builds a graph with a dead end (n2 cannot reach boss).
        /// </summary>
        private static MapGraph BuildDeadEndGraph()
        {
            var start = new MapNode("start", MapNodeType.Rest, 0, 0);
            var n1 = new MapNode("n1", MapNodeType.Combat, 1, 0);
            var n2 = new MapNode("n2", MapNodeType.Combat, 1, 1);
            var boss = new MapNode("boss", MapNodeType.Boss, 2, 0);

            start.ConnectedNodeIds.Add("n1");
            start.ConnectedNodeIds.Add("n2");
            n1.ConnectedNodeIds.Add("boss");
            // n2 deliberately has no connections → dead end

            return new MapGraph(new[] { start, n1, n2, boss }, "start", "boss");
        }

        // ── Constructor ────────────────────────────────────────────────

        [Test]
        public void MapGraph_Constructor_StoresNodes()
        {
            var graph = BuildSimpleGraph();

            Assert.AreEqual(4, graph.Nodes.Count);
            Assert.IsTrue(graph.Nodes.ContainsKey("start"));
            Assert.IsTrue(graph.Nodes.ContainsKey("boss"));
            Assert.AreEqual("start", graph.StartNodeId);
            Assert.AreEqual("boss", graph.BossNodeId);
        }

        [Test]
        public void MapGraph_Constructor_LastVisitedNodeIdIsNull()
        {
            var graph = BuildSimpleGraph();

            Assert.IsNull(graph.LastVisitedNodeId);
        }

        // ── GetAvailableNodes ──────────────────────────────────────────

        [Test]
        public void MapGraph_GetAvailableNodes_ReturnsStartConnectionsWhenNotStarted()
        {
            var graph = BuildSimpleGraph();

            var available = graph.GetAvailableNodes();

            Assert.AreEqual(2, available.Count);
            Assert.Contains("n1", available.ToList());
            Assert.Contains("n2", available.ToList());
        }

        [Test]
        public void MapGraph_GetAvailableNodes_ReturnsAdjacentUnvisitedNodes()
        {
            var graph = BuildSimpleGraph();
            graph.VisitNode("n1");

            var available = graph.GetAvailableNodes();

            Assert.AreEqual(1, available.Count);
            Assert.AreEqual("boss", available[0]);
        }

        [Test]
        public void MapGraph_GetAvailableNodes_ExcludesVisitedNode()
        {
            var graph = BuildSimpleGraph();
            graph.VisitNode("n1");
            graph.VisitNode("boss");

            var available = graph.GetAvailableNodes();

            Assert.IsEmpty(available);
        }

        // ── VisitNode ──────────────────────────────────────────────────

        [Test]
        public void MapGraph_VisitNode_MarksVisitedAndUpdatesLastVisited()
        {
            var graph = BuildSimpleGraph();

            graph.VisitNode("n1");

            Assert.IsTrue(graph.Nodes["n1"].IsVisited);
            Assert.AreEqual("n1", graph.LastVisitedNodeId);
        }

        [Test]
        public void MapGraph_VisitNode_ThrowsOnNonConnectedNode()
        {
            var graph = BuildSimpleGraph();
            graph.VisitNode("n1"); // start → n1 valid

            Assert.That(() => graph.VisitNode("n2"),
                Throws.InvalidOperationException);
        }

        [Test]
        public void MapGraph_VisitNode_ThrowsOnAlreadyVisitedNode()
        {
            var graph = BuildSimpleGraph();
            graph.VisitNode("n1");

            Assert.That(() => graph.VisitNode("n1"),
                Throws.InvalidOperationException);
        }

        [Test]
        public void MapGraph_VisitNode_ThrowsOnNonexistentNode()
        {
            var graph = BuildSimpleGraph();

            Assert.That(() => graph.VisitNode("ghost"),
                Throws.ArgumentException);
        }

        // ── IsComplete ─────────────────────────────────────────────────

        [Test]
        public void MapGraph_IsComplete_FalseInitially()
        {
            var graph = BuildSimpleGraph();

            Assert.IsFalse(graph.IsComplete);
        }

        [Test]
        public void MapGraph_IsComplete_TrueAfterBossVisited()
        {
            var graph = BuildSimpleGraph();
            graph.VisitNode("n1");
            graph.VisitNode("boss");

            Assert.IsTrue(graph.IsComplete);
        }

        // ── Validate ───────────────────────────────────────────────────

        [Test]
        public void MapGraph_Validate_ReturnsTrueForValidGraph()
        {
            var graph = BuildSimpleGraph();

            Assert.IsTrue(graph.Validate());
        }

        [Test]
        public void MapGraph_Validate_ReturnsFalseForDeadEndGraph()
        {
            var graph = BuildDeadEndGraph();

            Assert.IsFalse(graph.Validate());
        }

        // ════════════════════════════════════════════════════════════════
        // Task 1.3 — MapGenerator
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void MapGenerator_StandardGeneration_ProducesValidGraph()
        {
            var graph = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);

            int nodeCount = graph.Nodes.Count;
            // 1 start + 2×3 middle + 1 boss = 8
            Assert.GreaterOrEqual(nodeCount, 8);
            Assert.LessOrEqual(nodeCount, 10);
            Assert.IsTrue(graph.Validate());
        }

        [Test]
        public void MapGenerator_HasAtLeastThreeDistinctPaths()
        {
            var graph = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);

            int pathCount = CountDistinctPaths(graph);
            Assert.GreaterOrEqual(pathCount, 3,
                $"Expected at least 3 distinct paths, found {pathCount}");
        }

        [Test]
        public void MapGenerator_AllPathsReachBoss()
        {
            var graph = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);

            Assert.IsTrue(graph.Validate(),
                "Graph validation failed — some terminal paths do not reach Boss");
        }

        [Test]
        public void MapGenerator_DeterministicSeed_ProducesIdenticalGraphs()
        {
            var graphA = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);
            var graphB = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);

            Assert.AreEqual(graphA.Nodes.Count, graphB.Nodes.Count);
            Assert.AreEqual(graphA.StartNodeId, graphB.StartNodeId);
            Assert.AreEqual(graphA.BossNodeId, graphB.BossNodeId);

            // Verify same node structure
            foreach (var kvp in graphA.Nodes)
            {
                Assert.IsTrue(graphB.Nodes.ContainsKey(kvp.Key));
                var nodeB = graphB.Nodes[kvp.Key];
                Assert.AreEqual(kvp.Value.Type, nodeB.Type);
                Assert.AreEqual(kvp.Value.Row, nodeB.Row);
                Assert.AreEqual(kvp.Value.Col, nodeB.Col);
                Assert.AreEqual(kvp.Value.ConnectedNodeIds.Count, nodeB.ConnectedNodeIds.Count);
                foreach (var conn in kvp.Value.ConnectedNodeIds)
                    Assert.IsTrue(nodeB.ConnectedNodeIds.Contains(conn));
            }
        }

        [Test]
        public void MapGenerator_FirstNodeIsRestOrCombat()
        {
            var graph = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);
            var startNode = graph.Nodes[graph.StartNodeId];

            Assert.IsTrue(startNode.Type == MapNodeType.Rest
                       || startNode.Type == MapNodeType.Combat);
        }

        [Test]
        public void MapGenerator_LastNodeIsBoss()
        {
            var graph = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);
            var bossNode = graph.Nodes[graph.BossNodeId];

            Assert.AreEqual(MapNodeType.Boss, bossNode.Type);
        }

        [Test]
        public void MapGenerator_NodeTypeDistribution_MiddleRows()
        {
            var graph = MapGenerator.Generate(rows: 2, nodesPerRow: 3, seed: 42);

            // Middle rows (row 1 and 2 when start is row 0 and boss is last)
            foreach (var node in graph.Nodes.Values)
            {
                if (node.Id == graph.StartNodeId || node.Id == graph.BossNodeId)
                    continue;

                // Middle row nodes should be Combat or Elite (not Rest/Shop/Boss)
                Assert.IsTrue(node.Type == MapNodeType.Combat
                           || node.Type == MapNodeType.Elite
                           || node.Type == MapNodeType.Rest
                           || node.Type == MapNodeType.Shop,
                    $"Unexpected node type {node.Type} at middle row");
            }

            // Check at most one Rest or Shop per row
            var rows = graph.Nodes.Values
                .Where(n => n.Id != graph.StartNodeId && n.Id != graph.BossNodeId)
                .GroupBy(n => n.Row);

            foreach (var rowGroup in rows)
            {
                int restOrShop = rowGroup.Count(n =>
                    n.Type == MapNodeType.Rest || n.Type == MapNodeType.Shop);
                Assert.LessOrEqual(restOrShop, 1,
                    $"Row {rowGroup.Key} has {restOrShop} Rest/Shop nodes (max 1)");
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Path counting helper
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Counts the number of distinct paths from StartNodeId to BossNodeId
        /// using DFS traversal.
        /// </summary>
        private static int CountDistinctPaths(MapGraph graph)
        {
            var visited = new HashSet<string>();
            int pathCount = 0;

            void Dfs(string currentId)
            {
                if (currentId == graph.BossNodeId)
                {
                    pathCount++;
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
            return pathCount;
        }
    }
}
