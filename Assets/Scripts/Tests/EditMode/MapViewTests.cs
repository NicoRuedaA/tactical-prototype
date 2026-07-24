using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class MapViewTests
    {
        private static MapGraph BuildGraph()
        {
            var start = new MapNode("start", MapNodeType.Rest, 0, 0);
            var combat = new MapNode("combat", MapNodeType.Combat, 1, 0);
            var elite = new MapNode("elite", MapNodeType.Elite, 1, 1);
            var boss = new MapNode("boss", MapNodeType.Boss, 2, 0);

            start.ConnectedNodeIds.Add("combat");
            start.ConnectedNodeIds.Add("elite");
            combat.ConnectedNodeIds.Add("boss");
            elite.ConnectedNodeIds.Add("boss");
            return new MapGraph(new[] { start, combat, elite, boss }, "start", "boss");
        }

        [Test]
        public void BuildNodeStates_ExposesAvailableAndBlockedNodesBeforeStart()
        {
            var graph = BuildGraph();
            var states = MapView.BuildNodeStates(graph, graph.GetAvailableNodes());

            Assert.AreEqual(MapNodeState.Available, states["combat"]);
            Assert.AreEqual(MapNodeState.Available, states["elite"]);
            Assert.AreEqual(MapNodeState.Current, states["start"]);
            Assert.AreEqual(MapNodeState.Blocked, states["boss"]);
        }

        [Test]
        public void BuildNodeStates_DistinguishesCurrentVisitedAndNextChoices()
        {
            var graph = BuildGraph();
            graph.VisitNode("combat");
            graph.VisitNode("boss");
            var states = MapView.BuildNodeStates(graph, graph.GetAvailableNodes());

            Assert.AreEqual(MapNodeState.Current, states["boss"]);
            Assert.AreEqual(MapNodeState.Visited, states["combat"]);
            Assert.AreEqual(MapNodeState.Visited, states["start"]);
            Assert.AreEqual(MapNodeState.Blocked, states["elite"]);
        }

        [Test]
        public void GetConnectionState_HighlightsOnlyCurrentToAvailableRoute()
        {
            var graph = BuildGraph();
            graph.VisitNode("combat");
            var states = MapView.BuildNodeStates(graph, graph.GetAvailableNodes());

            Assert.AreEqual(MapConnectionState.Available,
                MapView.GetConnectionState(graph.Nodes["combat"], graph.Nodes["boss"], states));
            Assert.AreEqual(MapConnectionState.Visited,
                MapView.GetConnectionState(graph.Nodes["start"], graph.Nodes["combat"], states));
        }

        [Test]
        public void MapNodeState_DoesNotChangeNavigationRules()
        {
            var graph = BuildGraph();

            Assert.DoesNotThrow(() => graph.VisitNode("elite"));
            Assert.AreEqual("elite", graph.LastVisitedNodeId);
            Assert.AreEqual(MapNodeState.Current, MapView.GetNodeState(
                graph.Nodes["elite"], graph.LastVisitedNodeId,
                graph.GetAvailableNodes(), graph.StartNodeId));
            Assert.IsFalse(graph.Nodes["combat"].IsVisited);

            // Re-derive presentation after navigation; stale snapshots must
            // not be used to infer current/visited route state.
            var states = MapView.BuildNodeStates(graph, graph.GetAvailableNodes());
            Assert.AreEqual(MapNodeState.Visited, states["start"]);
            Assert.AreEqual(MapNodeState.Blocked, states["combat"]);
        }

        [Test]
        public void FormatRestHealResult_IncludesConfiguredPercentTotalAndClampedPerPieceValues()
        {
            var result = new RestHealResult(30, new[]
            {
                new RestHealPieceResult("a", "Alpha", 4, 7),
                new RestHealPieceResult("b", "Beta", 10, 10),
            });

            var text = MapView.FormatRestHealResult(result);

            StringAssert.Contains("30%", text);
            StringAssert.Contains("healed 3 HP", text);
            StringAssert.Contains("Alpha 4→7 (+3)", text);
            StringAssert.Contains("Beta 10→10 (+0)", text);
        }

        [Test]
        public void FormatRestHealResult_ReturnsEmptyForMissingResult()
        {
            Assert.AreEqual(string.Empty, MapView.FormatRestHealResult(null));
        }
    }
}
