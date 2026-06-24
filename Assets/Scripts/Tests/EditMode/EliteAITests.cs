using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// Tests for EliteEnemyAI behavior.
    /// Elite AI prioritizes ability use over basic attack,
    /// then falls back to DefaultEnemyAI.
    /// No phase behavior — simpler than boss AI.
    /// </summary>
    public class EliteAITests
    {
        [Test]
        public void EliteAI_UsesAbilityWhenAvailable()
        {
            var ability = new TestAbility
            {
                AbilityType = AbilityType.Active,
                ManaCost    = 1,
                ActiveRange = 2,
                EffectType  = EffectType.Damage,
                EffectValue = 4,
                AreaRadius  = 0,
                AffectsTeam = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(4, 1);
            var elite  = new Piece("E", Team.Enemy, 10, 2, 1, 2, 10,
                maxMana: 5, abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var player = new Piece("P", Team.Player, 10, 1, 1, 1, 1) { Coords = new Axial(2, 0) };
            var engine = new CombatEngine(board, new[] { elite, player });

            var ai = new EliteEnemyAI();
            ai.TakeTurn(engine);

            // Ability deals 4 damage (not basic attack of 2)
            Assert.AreEqual(10 - 4, player.Hp);
        }

        [Test]
        public void EliteAI_FallsBackToMeleeWhenNoMana()
        {
            var ability = new TestAbility
            {
                AbilityType = AbilityType.Active,
                ManaCost    = 10,
                ActiveRange = 2,
                EffectType  = EffectType.Damage,
                EffectValue = 4,
                AreaRadius  = 0,
                AffectsTeam = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(3, 1);
            var elite  = new Piece("E", Team.Enemy, 10, 2, 1, 2, 10,
                maxMana: 3, abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var player = new Piece("P", Team.Player, 10, 1, 1, 1, 1) { Coords = new Axial(1, 0) };
            var engine = new CombatEngine(board, new[] { elite, player });

            var ai = new EliteEnemyAI();
            ai.TakeTurn(engine);

            // Not enough mana for ability → basic attack for 2 damage
            Assert.AreEqual(10 - 2, player.Hp);
        }

        [Test]
        public void EliteAI_StepsCloserWhenOutOfRange()
        {
            var board  = Board.CreateRectangle(6, 1);
            var elite  = new Piece("E", Team.Enemy, 10, 2, 1, 2, 10) { Coords = new Axial(0, 0) };
            var player = new Piece("P", Team.Player, 10, 2, 1, 1, 1) { Coords = new Axial(5, 0) };
            var engine = new CombatEngine(board, new[] { elite, player });

            int before = Axial.Distance(elite.Coords, player.Coords);
            var ai = new EliteEnemyAI();
            ai.TakeTurn(engine);
            int after  = Axial.Distance(elite.Coords, player.Coords);

            // Elite moved closer (Default AI behavior) — proves delegation
            Assert.Less(after, before);
        }

        [Test]
        public void EliteAI_AbilitiesPriorityOverMovement()
        {
            // Elite should use an ability rather than move toward target
            // when ability is in range
            var ability = new TestAbility
            {
                AbilityType = AbilityType.Active,
                ManaCost    = 1,
                ActiveRange = 3,
                EffectType  = EffectType.Damage,
                EffectValue = 5,
                AreaRadius  = 0,
                AffectsTeam = AffectsTeam.Enemies,
            };

            var board  = Board.CreateRectangle(5, 1);
            var elite  = new Piece("E", Team.Enemy, 10, 2, 1, 2, 10,
                maxMana: 5, abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var player = new Piece("P", Team.Player, 10, 1, 1, 1, 1) { Coords = new Axial(4, 0) };
            var engine = new CombatEngine(board, new[] { elite, player });

            // Elite can't attack (range 1, target at 4) but ability reaches (range 3, target at 4? distance = 4 > 3)
            // Wait, distance is 4, active range is 3. So ability can't reach either.
            // Let me adjust: target at (3,0), distance = 3 = active range, works.

            // Let me redo this test setup:
            var board2  = Board.CreateRectangle(4, 1);
            var elite2  = new Piece("E2", Team.Enemy, 10, 2, 1, 2, 10,
                maxMana: 5, abilities: new[] { ability }) { Coords = new Axial(0, 0) };
            var player2 = new Piece("P2", Team.Player, 10, 1, 1, 1, 1) { Coords = new Axial(3, 0) };
            var engine2 = new CombatEngine(board2, new[] { elite2, player2 });

            var ai2 = new EliteEnemyAI();
            ai2.TakeTurn(engine2);

            // Should have used ability (5 damage) instead of moving
            Assert.AreEqual(10 - 5, player2.Hp);
        }

        [Test]
        public void EliteAI_NoPhaseBehavior()
        {
            // Elite should not throw, have no phase state — simple delegation
            var board  = Board.CreateRectangle(3, 1);
            var elite  = new Piece("E", Team.Enemy, 10, 2, 1, 2, 10) { Coords = new Axial(0, 0) };
            var player = new Piece("P", Team.Player, 10, 2, 1, 1, 1) { Coords = new Axial(1, 0) };
            var engine = new CombatEngine(board, new[] { elite, player });

            var ai = new EliteEnemyAI();
            Assert.DoesNotThrow(() => ai.TakeTurn(engine));
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
