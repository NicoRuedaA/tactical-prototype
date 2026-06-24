using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class RunStateTests
    {
        private static Piece MakePiece(string id, int hp = 5, bool isQueen = false)
        {
            return new Piece(id, Team.Player, hp, 1, 1, 1, 10, isQueen: isQueen, name: id);
        }

        private static Piece MakeEnemy(string id, int hp = 5)
        {
            return new Piece(id, Team.Enemy, hp, 1, 1, 1, 5, name: id);
        }

        /// <summary>
        /// Builds a simple 2-row linear graph: start → n1 → boss
        /// </summary>
        private static MapGraph MakeTwoStepGraph()
        {
            var start = new MapNode("start", MapNodeType.Combat, 0, 0);
            var n1 = new MapNode("n1", MapNodeType.Combat, 1, 0);
            var boss = new MapNode("boss", MapNodeType.Boss, 2, 0);
            start.ConnectedNodeIds.Add("n1");
            n1.ConnectedNodeIds.Add("boss");
            return new MapGraph(new[] { start, n1, boss }, "start", "boss");
        }

        // ── Constructor ───────────────────────────────────────────────────────

        [Test]
        public void Constructor_StoresPieces()
        {
            var pieces = new[] { MakePiece("p1"), MakePiece("p2") };
            var state = new RunState(pieces, MakeTwoStepGraph());

            Assert.AreEqual(2, state.Pieces.Count);
            Assert.AreEqual("p1", state.Pieces[0].Id);
            Assert.AreEqual("p2", state.Pieces[1].Id);
        }

        [Test]
        public void Constructor_ThrowsOnNullPieces()
        {
            Assert.That(() => new RunState(null, MakeTwoStepGraph()), Throws.ArgumentNullException);
        }

        [Test]
        public void Constructor_ThrowsOnEmptyPieces()
        {
            Assert.That(() => new RunState(new List<Piece>(), MakeTwoStepGraph()), Throws.ArgumentException);
        }

        [Test]
        public void Constructor_StoresGraph()
        {
            var graph = MakeTwoStepGraph();
            var state = new RunState(new[] { MakePiece("p1") }, graph);

            Assert.AreSame(graph, state.Graph);
        }

        [Test]
        public void Constructor_LastVisitedNodeIdIsNull()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            Assert.IsNull(state.LastVisitedNodeId);
        }

        [Test]
        public void Constructor_ThrowsOnNullGraph()
        {
            Assert.That(() => new RunState(new[] { MakePiece("p1") }, null),
                Throws.ArgumentNullException);
        }

        // ── AdvanceCombat ─────────────────────────────────────────────────────

        [Test]
        public void AdvanceCombat_ReturnsTrueBeforeBossVisited()
        {
            var graph = MakeTwoStepGraph();
            var state = new RunState(new[] { MakePiece("p1") }, graph);

            Assert.IsTrue(state.AdvanceCombat()); // Boss not yet visited
        }

        [Test]
        public void AdvanceCombat_ReturnsFalseAfterBossVisited()
        {
            var graph = MakeTwoStepGraph();
            var state = new RunState(new[] { MakePiece("p1") }, graph);

            state.VisitNode("n1");
            state.VisitNode("boss");

            Assert.IsFalse(state.AdvanceCombat()); // Boss visited → IsComplete
        }

        [Test]
        public void AdvanceCombat_TransitionsFromTrueToFalse()
        {
            var graph = MakeTwoStepGraph();
            var state = new RunState(new[] { MakePiece("p1") }, graph);

            Assert.IsTrue(state.AdvanceCombat()); // before start
            state.VisitNode("n1");
            Assert.IsTrue(state.AdvanceCombat()); // mid-way
            state.VisitNode("boss");
            Assert.IsFalse(state.AdvanceCombat()); // boss visited → complete
        }

        // ── VisitNode ─────────────────────────────────────────────────────────

        [Test]
        public void VisitNode_UpdatesLastVisitedNodeId()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            state.VisitNode("n1");

            Assert.AreEqual("n1", state.LastVisitedNodeId);
        }

        [Test]
        public void VisitNode_DelegatesToGraph()
        {
            var graph = MakeTwoStepGraph();
            var state = new RunState(new[] { MakePiece("p1") }, graph);

            state.VisitNode("n1");

            Assert.IsTrue(graph.Nodes["n1"].IsVisited);
        }

        [Test]
        public void VisitNode_ThrowsOnNonExistentNode()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            Assert.That(() => state.VisitNode("ghost"), Throws.ArgumentException);
        }

        // ── GetAvailableNodes ─────────────────────────────────────────────────

        [Test]
        public void GetAvailableNodes_DelegatesToGraph()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            var available = state.GetAvailableNodes();

            Assert.AreEqual(1, available.Count);
            Assert.AreEqual("n1", available[0]);
        }

        [Test]
        public void GetAvailableNodes_UpdatesAfterVisit()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            state.VisitNode("n1");
            var available = state.GetAvailableNodes();

            Assert.AreEqual(1, available.Count);
            Assert.AreEqual("boss", available[0]);
        }

        // ── IsPlayerDead ──────────────────────────────────────────────────────

        [Test]
        public void IsPlayerDead_FalseWhenAllAlive()
        {
            var pieces = new[] { MakePiece("p1", hp: 5), MakePiece("p2", hp: 5) };
            var state = new RunState(pieces, MakeTwoStepGraph());

            Assert.IsFalse(state.IsPlayerDead);
        }

        [Test]
        public void IsPlayerDead_TrueWhenAllDead()
        {
            var pieces = new[] { MakePiece("p1", hp: 5), MakePiece("p2", hp: 5) };
            var state = new RunState(pieces, MakeTwoStepGraph());

            foreach (var p in state.Pieces)
                p.TakeDamage(99);

            Assert.IsTrue(state.IsPlayerDead);
        }

        [Test]
        public void IsPlayerDead_TrueWhenSinglePieceDead()
        {
            var piece = MakePiece("p1", hp: 5);
            var state = new RunState(new[] { piece }, MakeTwoStepGraph());

            piece.TakeDamage(99);

            Assert.IsTrue(state.IsPlayerDead);
        }

        [Test]
        public void IsPlayerDead_FalseWhenSomeAlive()
        {
            var pieces = new[] { MakePiece("p1", hp: 5), MakePiece("p2", hp: 5) };
            var state = new RunState(pieces, MakeTwoStepGraph());

            state.Pieces[0].TakeDamage(99); // p1 dead, p2 alive

            Assert.IsFalse(state.IsPlayerDead);
        }

        // ── GetAlivePlayerPieces ──────────────────────────────────────────────

        [Test]
        public void GetAlivePlayerPieces_ReturnsAllWhenNoneDead()
        {
            var pieces = new[] { MakePiece("p1"), MakePiece("p2") };
            var state = new RunState(pieces, MakeTwoStepGraph());

            var alive = state.GetAlivePlayerPieces().ToList();

            Assert.AreEqual(2, alive.Count);
        }

        [Test]
        public void GetAlivePlayerPieces_FiltersDeadPieces()
        {
            var pieces = new[] { MakePiece("p1", hp: 5), MakePiece("p2", hp: 5) };
            var state = new RunState(pieces, MakeTwoStepGraph());

            state.Pieces[0].TakeDamage(99); // p1 dead

            var alive = state.GetAlivePlayerPieces().ToList();

            Assert.AreEqual(1, alive.Count);
            Assert.AreEqual("p2", alive[0].Id);
        }

        [Test]
        public void GetAlivePlayerPieces_ReturnsEmptyWhenAllDead()
        {
            var pieces = new[] { MakePiece("p1", hp: 5) };
            var state = new RunState(pieces, MakeTwoStepGraph());

            state.Pieces[0].TakeDamage(99);

            Assert.IsEmpty(state.GetAlivePlayerPieces());
        }

        // ── AddAbility ────────────────────────────────────────────────────────

        [Test]
        public void AddAbility_StoresAbilityOnPiece()
        {
            var pieces = new[] { MakePiece("p1") };
            var state = new RunState(pieces, MakeTwoStepGraph());
            var ability = new TestAbilityStub { DisplayName = "Fireball" };

            state.AddAbility("p1", ability);

            Assert.AreEqual(1, state.Pieces[0].Abilities.Count);
            Assert.AreEqual("Fireball", state.Pieces[0].Abilities[0].DisplayName);
        }

        [Test]
        public void AddAbility_NullIsNoOp()
        {
            var pieces = new[] { MakePiece("p1") };
            var state = new RunState(pieces, MakeTwoStepGraph());

            state.AddAbility("p1", null);

            Assert.AreEqual(0, state.Pieces[0].Abilities.Count);
        }

        [Test]
        public void AddAbility_ThrowsOnMissingPiece()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());
            var ability = new TestAbilityStub();

            Assert.That(() => state.AddAbility("nonexistent", ability), Throws.ArgumentException);
        }

        // ── ApplyStatBoost ────────────────────────────────────────────────────

        [Test]
        public void ApplyStatBoost_Damage_IncreasesEffectiveDamage()
        {
            var pieces = new[] { MakePiece("p1") };
            var state = new RunState(pieces, MakeTwoStepGraph());

            state.ApplyStatBoost("p1", StatType.Damage, 3);

            Assert.AreEqual(4, state.Pieces[0].EffectiveDamage); // 1 + 3
        }

        [Test]
        public void ApplyStatBoost_AttackRange_IncreasesEffectiveAttackRange()
        {
            var piece = new Piece("p1", Team.Player, 5, 1, 1, 1, 10);
            var state = new RunState(new[] { piece }, MakeTwoStepGraph());

            state.ApplyStatBoost("p1", StatType.AttackRange, 2);

            Assert.AreEqual(3, piece.EffectiveAttackRange); // 1 + 2
        }

        [Test]
        public void ApplyStatBoost_MoveRange_IncreasesEffectiveMoveRange()
        {
            var piece = new Piece("p1", Team.Player, 5, 1, 1, 1, 10);
            var state = new RunState(new[] { piece }, MakeTwoStepGraph());

            state.ApplyStatBoost("p1", StatType.MoveRange, 1);

            Assert.AreEqual(2, piece.EffectiveMoveRange); // 1 + 1
        }

        // ── ApplyMaxHpBoost ───────────────────────────────────────────────────

        [Test]
        public void ApplyMaxHpBoost_IncreasesMaxHpAndHeals()
        {
            var piece = new Piece("p1", Team.Player, 10, 1, 1, 1, 10);
            piece.TakeDamage(4);
            var state = new RunState(new[] { piece }, MakeTwoStepGraph());

            state.ApplyMaxHpBoost("p1", 3);

            Assert.AreEqual(13, piece.EffectiveMaxHp); // 10 + 3
            Assert.AreEqual(9, piece.Hp);              // (10-4) + 3
        }

        [Test]
        public void ApplyMaxHpBoost_WithFullHp_GrantsExtraHp()
        {
            var piece = new Piece("p1", Team.Player, 10, 1, 1, 1, 10);
            var state = new RunState(new[] { piece }, MakeTwoStepGraph());

            state.ApplyMaxHpBoost("p1", 5);

            Assert.AreEqual(15, piece.EffectiveMaxHp);
            Assert.AreEqual(15, piece.Hp); // 10 + 5 (healed by same amount)
        }

        [Test]
        public void ApplyMaxHpBoost_ThrowsOnMissingPiece()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            Assert.That(() => state.ApplyMaxHpBoost("nonexistent", 5), Throws.ArgumentException);
        }

        // ── PlacePiece ────────────────────────────────────────────────────────

        [Test]
        public void PlacePiece_UpdatesCoords()
        {
            var pieces = new[] { MakePiece("p1") };
            var state = new RunState(pieces, MakeTwoStepGraph());

            state.PlacePiece("p1", new Axial(2, 3));

            Assert.AreEqual(new Axial(2, 3), state.Pieces[0].Coords);
        }

        [Test]
        public void PlacePiece_ThrowsOnMissingPiece()
        {
            var state = new RunState(new[] { MakePiece("p1") }, MakeTwoStepGraph());

            Assert.That(() => state.PlacePiece("nonexistent", new Axial(0, 0)), Throws.ArgumentException);
        }

        /// <summary>
        /// Minimal IAbilityData stub for RunState tests.
        /// </summary>
        private class TestAbilityStub : IAbilityData
        {
            public string DisplayName { get; set; } = "Test";
            public AbilityType AbilityType { get; set; } = AbilityType.Active;
            public int ManaCost { get; set; } = 0;
            public int ActiveRange { get; set; } = 1;
            public PassiveTrigger Trigger { get; set; } = PassiveTrigger.OnHit;
            public EffectType EffectType { get; set; } = EffectType.Damage;
            public int EffectValue { get; set; } = 1;
            public StatType StatToModify { get; set; } = StatType.Damage;
            public int AreaRadius { get; set; } = 0;
            public AffectsTeam AffectsTeam { get; set; } = AffectsTeam.Enemies;
            public DurationType DurationType { get; set; } = DurationType.FixedTurns;
            public int DurationTurns { get; set; } = 1;
        }
    }
}
