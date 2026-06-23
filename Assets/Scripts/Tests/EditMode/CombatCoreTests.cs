using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class CombatCoreTests
    {
        // ── Hex math ─────────────────────────────────────────────────────────

        [Test]
        public void HexDistance_IsCubeDistance()
        {
            Assert.AreEqual(0, Axial.Distance(new Axial(0, 0), new Axial(0, 0)));
            Assert.AreEqual(1, Axial.Distance(new Axial(0, 0), new Axial(1, 0)));
            Assert.AreEqual(2, Axial.Distance(new Axial(0, 0), new Axial(2, 0)));
            Assert.AreEqual(3, Axial.Distance(new Axial(0, 0), new Axial(-1, -2)));
        }

        // ── BFS ──────────────────────────────────────────────────────────────

        [Test]
        public void Bfs_ReturnsSixNeighborsWithinOneMovePoint()
        {
            var board  = Board.CreateRectangle(5, 5);
            var result = Pathfinding.GetReachable(board, new Axial(2, 2), 1);
            Assert.AreEqual(6, result.ReachableTiles.Count());
        }

        [Test]
        public void Bfs_DoesNotPathThroughOccupiedTiles()
        {
            var board = Board.CreateRectangle(5, 5);
            board.Place(new Piece("blocker", Team.Enemy, 1, 1, 1, 0, 1), new Axial(3, 2));
            var result = Pathfinding.GetReachable(board, new Axial(2, 2), 1);
            Assert.IsFalse(result.CanReach(new Axial(3, 2)));
        }

        // ── Turn order ───────────────────────────────────────────────────────

        [Test]
        public void TurnOrder_FollowsInitiativeDescendingAndWraps()
        {
            var slow  = new Piece("slow", Team.Player, 5, 1, 1, 1, 5);
            var fast  = new Piece("fast", Team.Enemy,  5, 1, 1, 1, 9);
            var turns = new TurnSystem(new[] { slow, fast });

            Assert.AreEqual(fast, turns.Current);
            turns.Advance();
            Assert.AreEqual(slow, turns.Current);
            turns.Advance();
            Assert.AreEqual(fast, turns.Current);
        }

        // ── One action per turn ───────────────────────────────────────────────

        [Test]
        public void Move_ConsumesTheTurn()
        {
            var (engine, player, enemy) = TwoPieces(new Axial(0, 0), new Axial(3, 3));
            Assert.AreEqual(player, engine.Current);
            engine.Move(player, new Axial(0, 1));
            Assert.AreEqual(enemy, engine.Current);
        }

        [Test]
        public void Attack_ConsumesTheTurnAndDealsDamage()
        {
            var (engine, player, enemy) = TwoPieces(new Axial(0, 0), new Axial(1, 0), damage: 2);
            engine.Attack(player, enemy);
            Assert.AreEqual(enemy.MaxHp - 2, enemy.Hp);
            Assert.AreEqual(enemy, engine.Current);
        }

        // ── Win condition ─────────────────────────────────────────────────────

        [Test]
        public void QueenDeath_EndsCombatAndCrownsTheOtherTeam()
        {
            var board       = Board.CreateRectangle(3, 1);
            var playerQueen = new Piece("PQ", Team.Player, 10, 3, 1, 1, 10, isQueen: true) { Coords = new Axial(0, 0) };
            var enemyQueen  = new Piece("EQ", Team.Enemy,  3,  3, 1, 1, 5,  isQueen: true) { Coords = new Axial(1, 0) };
            var engine      = new CombatEngine(board, new[] { playerQueen, enemyQueen });

            engine.Attack(playerQueen, enemyQueen);

            Assert.IsTrue(engine.IsOver);
            Assert.AreEqual(Team.Player, engine.Winner);
        }

        // ── AI ────────────────────────────────────────────────────────────────

        [Test]
        public void EnemyAI_AttacksWhenATargetIsInRange()
        {
            var board       = Board.CreateRectangle(3, 1);
            var enemy       = new Piece("E", Team.Enemy,  5, 2, 1, 1, 10) { Coords = new Axial(0, 0) };
            var playerQueen = new Piece("P", Team.Player, 5, 2, 1, 1, 1,  isQueen: true) { Coords = new Axial(1, 0) };
            var engine      = new CombatEngine(board, new[] { enemy, playerQueen });
            Assert.AreEqual(enemy, engine.Current);

            EnemyTurnAI.TakeTurn(engine);

            Assert.AreEqual(playerQueen.MaxHp - enemy.Damage, playerQueen.Hp);
        }

        [Test]
        public void EnemyAI_StepsCloserWhenOutOfRange()
        {
            var board  = Board.CreateRectangle(6, 1);
            var enemy  = new Piece("E", Team.Enemy,  5, 2, 1, 2, 10) { Coords = new Axial(0, 0) };
            var player = new Piece("P", Team.Player, 5, 2, 1, 1, 1)  { Coords = new Axial(5, 0) };
            var engine = new CombatEngine(board, new[] { enemy, player });

            int before = Axial.Distance(enemy.Coords, player.Coords);
            EnemyTurnAI.TakeTurn(engine);
            int after  = Axial.Distance(enemy.Coords, player.Coords);

            Assert.Less(after, before);
        }

        // ── Abilities: active ─────────────────────────────────────────────────

        [Test]
        public void UseAbility_SpendsManaAndDealsDamage()
        {
            var ability = new TestAbility
            {
                AbilityType = AbilityType.Active,
                ManaCost    = 2,
                ActiveRange = 2,
                EffectType  = EffectType.Damage,
                EffectValue = 3,
                AreaRadius  = 0,
                AffectsTeam = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(4, 1);
            var player = new Piece("P", Team.Player, 5, 1, 1, 1, 10,
                maxMana: 4, abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var enemy  = new Piece("E", Team.Enemy,  5, 1, 1, 1, 1) { Coords = new Axial(2, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy });

            bool ok = engine.UseAbility(player, ability, enemy.Coords);

            Assert.IsTrue(ok);
            Assert.AreEqual(5 - 3, enemy.Hp);
            Assert.AreEqual(4 - 2, player.Mana);
        }

        [Test]
        public void UseAbility_ConsumesTheTurn()
        {
            var ability = new TestAbility
            {
                AbilityType = AbilityType.Active,
                ManaCost    = 0,
                ActiveRange = 2,
                EffectType  = EffectType.Damage,
                EffectValue = 1,
                AreaRadius  = 0,
                AffectsTeam = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(4, 1);
            var player = new Piece("P", Team.Player, 5, 1, 1, 1, 10,
                abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var enemy  = new Piece("E", Team.Enemy, 5, 1, 1, 1, 1) { Coords = new Axial(2, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy });

            engine.UseAbility(player, ability, enemy.Coords);

            Assert.AreEqual(enemy, engine.Current);
        }

        [Test]
        public void UseAbility_FailsWhenNotEnoughMana()
        {
            var ability = new TestAbility
            {
                AbilityType = AbilityType.Active,
                ManaCost    = 10,
                ActiveRange = 2,
                EffectType  = EffectType.Damage,
                EffectValue = 1,
                AreaRadius  = 0,
                AffectsTeam = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(4, 1);
            var player = new Piece("P", Team.Player, 5, 1, 1, 1, 10,
                maxMana: 3, abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var enemy  = new Piece("E", Team.Enemy, 5, 1, 1, 1, 1) { Coords = new Axial(2, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy });

            bool ok = engine.UseAbility(player, ability, enemy.Coords);

            Assert.IsFalse(ok);
            Assert.AreEqual(player, engine.Current); // turn not consumed
        }

        // ── Abilities: passive triggers ───────────────────────────────────────

        [Test]
        public void Passive_OnHit_DamagesTriggerTargetOnAttack()
        {
            // Player has an OnHit passive that deals 2 splash damage to all enemies
            // within radius 1 of the player. When player attacks, splash fires.
            var splash = new TestAbility
            {
                AbilityType  = AbilityType.Passive,
                Trigger      = PassiveTrigger.OnHit,
                DurationType = DurationType.FixedTurns,
                EffectType   = EffectType.Damage,
                EffectValue  = 2,
                AreaRadius   = 1,
                AffectsTeam  = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(4, 1);
            var player = new Piece("P", Team.Player, 5, 1, 1, 1, 10,
                abilities: new[] { splash }) { Coords = new Axial(0, 0) };
            var enemy1 = new Piece("E1", Team.Enemy, 5, 1, 1, 1, 1) { Coords = new Axial(1, 0) };
            var enemy2 = new Piece("E2", Team.Enemy, 5, 1, 1, 1, 2) { Coords = new Axial(2, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy1, enemy2 });

            engine.Attack(player, enemy1); // triggers OnHit splash

            // enemy1 took base attack (1) + splash (2) = -3 HP
            Assert.AreEqual(5 - 1 - 2, enemy1.Hp);
            // enemy2 is within radius 1 of player (Axial(0,0) → Axial(2,0) = dist 2, NOT in radius 1)
            // so enemy2 only takes splash if within 1 tile of PLAYER, not of enemy1
            // Axial(2,0) distance from Axial(0,0) = 2 → outside radius 1 → no splash
            Assert.AreEqual(5, enemy2.Hp);
        }

        [Test]
        public void Passive_OnTurnStart_FiresAtStartOfTurn()
        {
            // Player has OnTurnStart passive: heal self for 2 each turn start.
            var regen = new TestAbility
            {
                AbilityType  = AbilityType.Passive,
                Trigger      = PassiveTrigger.OnTurnStart,
                DurationType = DurationType.FixedTurns,
                EffectType   = EffectType.Heal,
                EffectValue  = 2,
                AreaRadius   = 0,
                AffectsTeam  = AffectsTeam.Self,
            };

            var board  = Board.CreateRectangle(4, 1);
            var player = new Piece("P", Team.Player, 10, 1, 1, 1, 10,
                abilities: new[] { regen }) { Coords = new Axial(0, 0) };
            var enemy  = new Piece("E", Team.Enemy, 10, 1, 1, 1, 1) { Coords = new Axial(3, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy });

            // Damage the player first so healing is visible.
            player.TakeDamage(4);
            Assert.AreEqual(6, player.Hp);

            // Enemy passes → player's turn starts → OnTurnStart fires.
            engine.Pass(); // enemy passes (engine.Current = enemy after begin)

            // Actually, engine hasn't called Begin() yet, so Current = player (highest initiative).
            // Let's set up so player goes first and then a full cycle happens.
            // Re-create with proper ordering:
            var board2  = Board.CreateRectangle(4, 1);
            var player2 = new Piece("P2", Team.Player, 10, 1, 1, 1, 5,
                abilities: new[] { regen }) { Coords = new Axial(0, 0) };
            var enemy2  = new Piece("E2", Team.Enemy, 10, 1, 1, 1, 10) { Coords = new Axial(3, 0) };
            var engine2 = new CombatEngine(board2, new[] { player2, enemy2 });

            player2.TakeDamage(4); // player2 at 6 HP

            // enemy2 goes first (initiative 10), passes
            Assert.AreEqual(enemy2, engine2.Current);
            engine2.Pass(); // now player2's turn starts → OnTurnStart fires

            Assert.AreEqual(6 + 2, player2.Hp); // healed by 2
        }

        // ── Buffs ─────────────────────────────────────────────────────────────

        [Test]
        public void Buff_IncreasesEffectiveDamage()
        {
            var damageBuff = new TestAbility
            {
                AbilityType  = AbilityType.Active,
                ManaCost     = 0,
                ActiveRange  = 0,
                EffectType   = EffectType.Buff,
                EffectValue  = 3,
                StatToModify = StatType.Damage,
                AreaRadius   = 0,
                AffectsTeam  = AffectsTeam.Self,
                DurationType = DurationType.FixedTurns,
                DurationTurns = 2,
            };

            var board  = Board.CreateRectangle(4, 1);
            var player = new Piece("P", Team.Player, 5, 2, 1, 1, 10,
                abilities: new[] { damageBuff }) { Coords = new Axial(0, 0) };
            var enemy  = new Piece("E", Team.Enemy, 5, 1, 1, 1, 1) { Coords = new Axial(3, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy });

            Assert.AreEqual(2, player.EffectiveDamage);
            engine.UseAbility(player, damageBuff, player.Coords);
            Assert.AreEqual(2 + 3, player.EffectiveDamage);
        }

        [Test]
        public void Buff_ExpiresAfterDurationTurns()
        {
            var damageBuff = new TestAbility
            {
                AbilityType   = AbilityType.Active,
                ManaCost      = 0,
                ActiveRange   = 0,
                EffectType    = EffectType.Buff,
                EffectValue   = 3,
                StatToModify  = StatType.Damage,
                AreaRadius    = 0,
                AffectsTeam   = AffectsTeam.Self,
                DurationType  = DurationType.FixedTurns,
                DurationTurns = 1,
            };

            var board  = Board.CreateRectangle(6, 1);
            var player = new Piece("P", Team.Player, 5, 2, 1, 1, 10,
                abilities: new[] { damageBuff }) { Coords = new Axial(0, 0) };
            var enemy  = new Piece("E", Team.Enemy, 5, 1, 1, 1, 1) { Coords = new Axial(5, 0) };
            var engine = new CombatEngine(board, new[] { player, enemy });

            engine.UseAbility(player, damageBuff, player.Coords); // uses turn, buff applied
            // enemy passes
            engine.Pass();
            // now player's turn again — buff had 1 turn, ticked at end of player's last turn → expired
            Assert.AreEqual(2, player.EffectiveDamage); // back to base
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static (CombatEngine engine, Piece player, Piece enemy) TwoPieces(
            Axial playerAt, Axial enemyAt, int damage = 1)
        {
            var board  = Board.CreateRectangle(6, 6);
            var player = new Piece("P", Team.Player, 5, damage, 1, 3, 10) { Coords = playerAt };
            var enemy  = new Piece("E", Team.Enemy,  5, damage, 1, 3, 1)  { Coords = enemyAt };
            var engine = new CombatEngine(board, new[] { player, enemy });
            return (engine, player, enemy);
        }

        /// <summary>
        /// Pure C# IAbilityData stub for tests — no ScriptableObject needed.
        /// </summary>
        private class TestAbility : IAbilityData
        {
            public string         DisplayName   { get; set; } = "Test";
            public AbilityType    AbilityType   { get; set; } = AbilityType.Active;
            public int            ManaCost      { get; set; } = 0;
            public int            ActiveRange   { get; set; } = 1;
            public PassiveTrigger Trigger       { get; set; } = PassiveTrigger.OnHit;
            public EffectType     EffectType    { get; set; } = EffectType.Damage;
            public int            EffectValue   { get; set; } = 1;
            public StatType       StatToModify  { get; set; } = StatType.Damage;
            public int            AreaRadius    { get; set; } = 0;
            public AffectsTeam    AffectsTeam   { get; set; } = AffectsTeam.Enemies;
            public DurationType   DurationType  { get; set; } = DurationType.FixedTurns;
            public int            DurationTurns { get; set; } = 1;
        }
    }
}
