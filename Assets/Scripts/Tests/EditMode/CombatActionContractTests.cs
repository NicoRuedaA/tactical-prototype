using System.Linq;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class CombatActionContractTests
    {
        [Test]
        public void EvaluateAction_NullRequest_IsTypedAndDoesNotMutate()
        {
            var setup = CreateDuel();

            CombatActionResult result = setup.Engine.EvaluateAction(null);

            AssertRejected(result, CombatActionRejection.InvalidRequest);
            Assert.AreSame(setup.Player, setup.Engine.Current);
            Assert.AreEqual(0, setup.Engine.TurnCount);
        }

        [Test]
        public void EvaluateAction_RejectsInvalidAndWrongTurnActors()
        {
            var setup = CreateDuel();
            var outsider = NewPiece("Outsider", Team.Player, 20);

            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.Pass(outsider)),
                CombatActionRejection.InvalidActor);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.Pass(setup.Enemy)),
                CombatActionRejection.WrongTurn);

            Assert.AreEqual(0, setup.Engine.TurnCount);
        }

        [Test]
        public void PendingDeath_PreviewAndExecutionRejectEquallyWithoutMutation()
        {
            var setup = CreateDuel();
            var request = CombatActionRequest.Pass(setup.Player);
            int pieceDiedCount = 0;
            setup.Engine.PieceDied += _ => pieceDiedCount++;
            int enemyHpBefore = setup.Enemy.Hp;
            Axial playerPositionBefore = setup.Player.Coords;
            Piece currentBefore = setup.Engine.Current;

            setup.Player.TakeDamage(setup.Player.MaxHp);
            CombatActionResult preview = setup.Engine.EvaluateAction(request);
            CombatActionResult execution = setup.Engine.ExecuteAction(request);

            AssertRejected(preview, CombatActionRejection.PendingDeaths);
            AssertRejected(execution, CombatActionRejection.PendingDeaths);
            Assert.IsTrue(setup.Engine.HasPendingDeaths);
            Assert.AreEqual(0, setup.Player.Hp);
            Assert.AreEqual(enemyHpBefore, setup.Enemy.Hp);
            Assert.AreEqual(playerPositionBefore, setup.Player.Coords);
            Assert.AreSame(currentBefore, setup.Engine.Current);
            Assert.AreEqual(0, setup.Engine.TurnCount);
            Assert.AreEqual(0, pieceDiedCount);
            Assert.IsFalse(setup.Engine.IsOver);
        }

        [Test]
        public void EvaluateAction_AfterCombatEnds_ReturnsCombatOver()
        {
            var board = Board.CreateRectangle(3, 1);
            var player = NewPiece("P", Team.Player, 10, hp: 10, damage: 10, queen: true, at: new Axial(0, 0));
            var enemy = NewPiece("E", Team.Enemy, 1, hp: 1, queen: true, at: new Axial(1, 0));
            var engine = new CombatEngine(board, new[] { player, enemy });
            engine.SelectPiece(player);
            Assert.IsTrue(engine.Attack(player, enemy));

            CombatActionResult result = engine.EvaluateAction(CombatActionRequest.Pass(player));

            AssertRejected(result, CombatActionRejection.CombatOver);
        }

        [Test]
        public void MovePreview_ReturnsSpecificDestinationRejectionsWithoutMutation()
        {
            var setup = CreateDuel(playerAt: new Axial(0, 0), enemyAt: new Axial(2, 0), moveRange: 1);
            setup.Engine.Board.GetTile(new Axial(0, 1)).Walkable = false;
            Axial origin = setup.Player.Coords;

            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.Move(setup.Player, new Axial(9, 9))),
                CombatActionRejection.InvalidDestination);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.Move(setup.Player, new Axial(0, 1))),
                CombatActionRejection.DestinationBlocked);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.Move(setup.Player, setup.Enemy.Coords)),
                CombatActionRejection.DestinationOccupied);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.Move(setup.Player, new Axial(1, 1))),
                CombatActionRejection.Unreachable);

            Assert.AreEqual(origin, setup.Player.Coords);
            Assert.AreSame(setup.Player, setup.Engine.Current);
            Assert.AreEqual(0, setup.Engine.TurnCount);
        }

        [Test]
        public void MovePreviewAndExecution_ShareCoreLegalityAndConsumeOneTurn()
        {
            var setup = CreateDuel(enemyAt: new Axial(3, 0));
            var request = CombatActionRequest.Move(setup.Player, new Axial(0, 1));

            CombatActionResult preview = setup.Engine.EvaluateAction(request);
            Assert.IsTrue(preview.IsAllowed);
            Assert.IsFalse(preview.WasExecuted);
            Assert.AreEqual(new Axial(0, 0), setup.Player.Coords);

            CombatActionResult executed = setup.Engine.ExecuteAction(request);
            Assert.IsTrue(executed.IsAllowed);
            Assert.IsTrue(executed.WasExecuted);
            Assert.AreEqual(new Axial(0, 1), setup.Player.Coords);
            Assert.IsNull(setup.Engine.Current);
            Assert.AreEqual(1, setup.Engine.TurnCount);
        }

        [Test]
        public void AttackPreview_ReturnsSpecificTargetRejectionsWithoutMutation()
        {
            var setup = CreateDuel(enemyAt: new Axial(3, 0), attackRange: 1);
            var ally = NewPiece("A", Team.Player, 5, at: new Axial(0, 1));
            var foreignEnemy = NewPiece("F", Team.Enemy, 5, at: new Axial(1, 0));
            var enemyBackup = NewPiece("EB", Team.Enemy, 2, at: new Axial(4, 0));
            var engine = new CombatEngine(
                setup.Engine.Board, new[] { setup.Player, setup.Enemy, ally, enemyBackup });
            engine.SelectPiece(setup.Player);
            int enemyHpBefore = setup.Enemy.Hp;
            int playerManaBefore = setup.Player.Mana;
            int turnBefore = engine.TurnCount;
            Axial playerPositionBefore = setup.Player.Coords;
            Piece currentBefore = engine.Current;

            AssertRejected(
                engine.EvaluateAction(CombatActionRequest.Attack(setup.Player, null)),
                CombatActionRejection.InvalidTarget);
            AssertRejected(
                engine.EvaluateAction(CombatActionRequest.Attack(setup.Player, foreignEnemy)),
                CombatActionRejection.InvalidTarget);
            AssertRejected(
                engine.EvaluateAction(CombatActionRequest.Attack(setup.Player, ally)),
                CombatActionRejection.FriendlyTarget);
            AssertRejected(
                engine.EvaluateAction(CombatActionRequest.Attack(setup.Player, setup.Enemy)),
                CombatActionRejection.OutOfRange);

            Assert.AreEqual(enemyHpBefore, setup.Enemy.Hp);
            Assert.AreEqual(playerManaBefore, setup.Player.Mana);
            Assert.AreEqual(turnBefore, engine.TurnCount);
            Assert.AreEqual(playerPositionBefore, setup.Player.Coords);
            Assert.AreSame(currentBefore, engine.Current);

            setup.Enemy.TakeDamage(setup.Enemy.MaxHp);
            AssertRejected(
                engine.EvaluateAction(CombatActionRequest.Attack(setup.Player, setup.Enemy)),
                CombatActionRejection.PendingDeaths);
            Assert.AreEqual(1, engine.ResolvePendingDeaths());
            AssertRejected(
                engine.EvaluateAction(CombatActionRequest.Attack(setup.Player, setup.Enemy)),
                CombatActionRejection.TargetDead);
            Assert.AreSame(setup.Player, engine.Current);
            Assert.AreEqual(0, engine.TurnCount);
        }

        [Test]
        public void AbilityPreview_ReturnsSpecificSetupRejections()
        {
            var owned = DamageAbility(manaCost: 6, activeRange: 1);
            var passive = DamageAbility();
            passive.AbilityType = AbilityType.Passive;
            var unowned = DamageAbility();
            var setup = CreateDuel(
                enemyAt: new Axial(3, 0), maxMana: 5, abilities: new[] { owned, passive });

            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.UseAbility(setup.Player, null, setup.Enemy.Coords)),
                CombatActionRejection.InvalidAbility);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.UseAbility(setup.Player, passive, setup.Enemy.Coords)),
                CombatActionRejection.InvalidAbility);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.UseAbility(setup.Player, unowned, setup.Enemy.Coords)),
                CombatActionRejection.AbilityNotOwned);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.UseAbility(setup.Player, owned, setup.Enemy.Coords)),
                CombatActionRejection.InsufficientMana);

            var ranged = DamageAbility(activeRange: 1);
            setup.Player.AddAbility(ranged);
            AssertRejected(
                setup.Engine.EvaluateAction(CombatActionRequest.UseAbility(setup.Player, ranged, setup.Enemy.Coords)),
                CombatActionRejection.OutOfRange);
            Assert.AreEqual(5, setup.Player.Mana);
            Assert.AreSame(setup.Player, setup.Engine.Current);
        }

        [Test]
        public void AbilityWithNoLegalTargets_SpendsNeitherManaNorTurn()
        {
            var ability = DamageAbility(manaCost: 2, activeRange: 4);
            var setup = CreateDuel(
                enemyAt: new Axial(3, 0), maxMana: 5, abilities: new[] { ability });
            int feedbackCount = 0;
            setup.Engine.AbilityResolved += _ => feedbackCount++;
            var request = CombatActionRequest.UseAbility(
                setup.Player, ability, new Axial(1, 0));

            CombatActionResult preview = setup.Engine.EvaluateAction(request);
            CombatActionResult execution = setup.Engine.ExecuteAction(request);
            bool legacyResult = setup.Engine.UseAbility(
                setup.Player, ability, new Axial(1, 0));

            AssertRejected(preview, CombatActionRejection.NoLegalTargets);
            AssertRejected(execution, CombatActionRejection.NoLegalTargets);
            Assert.IsFalse(legacyResult);
            Assert.AreEqual(5, setup.Player.Mana);
            Assert.AreSame(setup.Player, setup.Engine.Current);
            Assert.AreEqual(0, setup.Engine.TurnCount);
            Assert.AreEqual(0, feedbackCount);
        }

        [TestCase(0)]
        [TestCase(2)]
        public void AbilityOutsideBoard_IsRejectedBeforeDirectOrAreaTargetResolution(
            int areaRadius)
        {
            var ability = DamageAbility(
                manaCost: 2, activeRange: 5, effectValue: 3, areaRadius: areaRadius);
            var setup = CreateDuel(
                enemyAt: new Axial(0, 1), maxMana: 5, abilities: new[] { ability });
            var request = CombatActionRequest.UseAbility(
                setup.Player, ability, new Axial(-1, 0));
            int playerHpBefore = setup.Player.Hp;
            int enemyHpBefore = setup.Enemy.Hp;
            int manaBefore = setup.Player.Mana;
            int turnBefore = setup.Engine.TurnCount;
            Axial positionBefore = setup.Player.Coords;
            Piece currentBefore = setup.Engine.Current;

            CombatActionResult preview = setup.Engine.EvaluateAction(request);
            CombatActionResult execution = setup.Engine.ExecuteAction(request);

            AssertRejected(preview, CombatActionRejection.InvalidDestination);
            AssertRejected(execution, CombatActionRejection.InvalidDestination);
            Assert.AreEqual(playerHpBefore, setup.Player.Hp);
            Assert.AreEqual(enemyHpBefore, setup.Enemy.Hp);
            Assert.AreEqual(manaBefore, setup.Player.Mana);
            Assert.AreEqual(turnBefore, setup.Engine.TurnCount);
            Assert.AreEqual(positionBefore, setup.Player.Coords);
            Assert.AreSame(currentBefore, setup.Engine.Current);
        }

        [Test]
        public void AbilityPreview_ExposesOnlyLegalResolvedTargets()
        {
            var ability = DamageAbility(activeRange: 3, areaRadius: 1);
            var board = Board.CreateRectangle(5, 2);
            var player = NewPiece("P", Team.Player, 10, maxMana: 5,
                abilities: new[] { ability }, at: new Axial(0, 0));
            var ally = NewPiece("A", Team.Player, 2, at: new Axial(2, 1));
            var enemy1 = NewPiece("E1", Team.Enemy, 1, at: new Axial(2, 0));
            var enemy2 = NewPiece("E2", Team.Enemy, 0, at: new Axial(3, 0));
            var engine = new CombatEngine(board, new[] { player, ally, enemy1, enemy2 });
            engine.SelectPiece(player);

            CombatActionResult preview = engine.EvaluateAction(
                CombatActionRequest.UseAbility(player, ability, enemy1.Coords));

            Assert.IsTrue(preview.IsAllowed);
            CollectionAssert.AreEquivalent(new[] { enemy1, enemy2 }, preview.LegalTargets);
            CollectionAssert.DoesNotContain(preview.LegalTargets, ally);
            Assert.AreEqual(5, player.Mana);
            Assert.AreEqual(10, enemy1.Hp);
        }

        [Test]
        public void ActiveAbilityFeedback_ContainsSourceTargetsAndStateChanges()
        {
            var ability = DamageAbility(manaCost: 2, activeRange: 3, effectValue: 3);
            var setup = CreateDuel(
                enemyAt: new Axial(2, 0), maxMana: 5, abilities: new[] { ability });
            AbilityResolution feedback = null;
            setup.Engine.AbilityResolved += resolution => feedback = resolution;

            CombatActionResult result = setup.Engine.ExecuteAction(
                CombatActionRequest.UseAbility(setup.Player, ability, setup.Enemy.Coords));

            Assert.IsTrue(result.WasExecuted);
            Assert.NotNull(feedback);
            Assert.AreSame(setup.Player, feedback.Source);
            Assert.AreSame(ability, feedback.Ability);
            Assert.IsFalse(feedback.IsPassive);
            Assert.IsNull(feedback.Trigger);
            CollectionAssert.AreEqual(new[] { setup.Enemy }, feedback.Targets);
            Assert.AreEqual(-3, feedback.Changes.Single().HpDelta);
            Assert.AreEqual(3, setup.Player.Mana);
        }

        [Test]
        public void TriggeredPassiveFeedback_ContainsTriggerAndChanges()
        {
            var passive = DamageAbility(effectValue: 2, areaRadius: 1);
            passive.AbilityType = AbilityType.Passive;
            passive.Trigger = PassiveTrigger.OnHit;
            var setup = CreateDuel(enemyAt: new Axial(1, 0), abilities: new[] { passive });
            AbilityResolution feedback = null;
            setup.Engine.AbilityResolved += resolution =>
            {
                if (resolution.IsPassive)
                    feedback = resolution;
            };

            Assert.IsTrue(setup.Engine.Attack(setup.Player, setup.Enemy));

            Assert.NotNull(feedback);
            Assert.AreSame(setup.Player, feedback.Source);
            Assert.AreSame(passive, feedback.Ability);
            Assert.AreEqual(PassiveTrigger.OnHit, feedback.Trigger);
            Assert.AreEqual(-2, feedback.Changes.Single().HpDelta);
        }

        [Test]
        public void AuraPassiveFeedback_ReportsAppliedBuffWithoutTrigger()
        {
            var aura = DamageAbility(areaRadius: 1);
            aura.AbilityType = AbilityType.Passive;
            aura.EffectType = EffectType.Buff;
            aura.AffectsTeam = AffectsTeam.Allies;
            aura.DurationType = DurationType.WhileInArea;
            var setup = CreateDuel(enemyAt: new Axial(3, 0), abilities: new[] { aura });
            AbilityResolution feedback = null;
            setup.Engine.AbilityResolved += resolution =>
            {
                if (resolution.Ability == aura)
                    feedback = resolution;
            };

            setup.Engine.Pass();
            setup.Engine.SelectPiece(setup.Enemy);

            Assert.NotNull(feedback);
            Assert.IsTrue(feedback.IsPassive);
            Assert.IsNull(feedback.Trigger);
            Assert.AreEqual(1, feedback.Changes.Single().BuffDelta);
        }

        [Test]
        public void BossPhaseTransition_EventIsEmittedExactlyOnce()
        {
            var phaseAbility = DamageAbility(activeRange: 5, effectValue: 2);
            var board = Board.CreateRectangle(4, 1);
            var boss = NewPiece("Boss", Team.Enemy, 10, hp: 20, damage: 3,
                maxMana: 5, queen: true, at: new Axial(0, 0));
            boss.TakeDamage(11);
            var player = NewPiece("P", Team.Player, 1, hp: 30, at: new Axial(1, 0));
            var engine = new CombatEngine(board, new[] { boss, player });
            engine.SelectPiece(boss);
            var ai = new BossEnemyAI(boss, phaseAbility, damageBuff: 4);
            int eventCount = 0;
            BossPhaseTransition transition = null;
            engine.BossPhaseTransitioned += payload =>
            {
                eventCount++;
                transition = payload;
            };

            ai.TakeTurn(engine);
            engine.Pass();
            engine.SelectPiece(player);
            engine.Pass();
            engine.SelectPiece(boss);
            ai.TakeTurn(engine);

            Assert.AreEqual(1, eventCount);
            Assert.NotNull(transition);
            Assert.AreSame(boss, transition.Boss);
            Assert.AreEqual(2, transition.Phase);
            Assert.AreSame(phaseAbility, transition.GrantedAbility);
            Assert.AreEqual(4, transition.DamageBonus);
        }

        [Test]
        public void TerminalAction_EndsOpportunityWithoutStartingAnotherTurn()
        {
            var board = Board.CreateRectangle(3, 1);
            var player = NewPiece("P", Team.Player, 10, damage: 10,
                queen: true, at: new Axial(0, 0));
            var enemy = NewPiece("E", Team.Enemy, 1, hp: 1,
                queen: true, at: new Axial(1, 0));
            var engine = new CombatEngine(board, new[] { player, enemy });
            engine.SelectPiece(player);

            CombatActionResult result = engine.ExecuteAction(
                CombatActionRequest.Attack(player, enemy));

            Assert.IsTrue(result.WasExecuted);
            Assert.IsTrue(engine.IsOver);
            Assert.AreEqual(0, engine.TurnCount);
        }

        [Test]
        public void BasicAttackFeedback_ReportsActualOverkillDeltaAndPreservesLegacyDamage()
        {
            var board = Board.CreateRectangle(3, 1);
            var player = NewPiece("P", Team.Player, 10, damage: 5,
                queen: true, at: new Axial(0, 0));
            var enemy = NewPiece("E", Team.Enemy, 1, hp: 1,
                queen: true, at: new Axial(1, 0));
            var engine = new CombatEngine(board, new[] { player, enemy });
            engine.SelectPiece(player);
            AttackResolution rich = null;
            int richCount = 0;
            int legacyCount = 0;
            int legacyDamage = 0;
            engine.AttackResolved += resolution =>
            {
                richCount++;
                rich = resolution;
            };
            engine.PieceAttacked += (_, _, damage) =>
            {
                legacyCount++;
                legacyDamage = damage;
            };

            Assert.IsTrue(engine.Attack(player, enemy));

            Assert.AreEqual(1, richCount);
            Assert.NotNull(rich);
            Assert.AreSame(player, rich.Attacker);
            Assert.AreSame(enemy, rich.Target);
            Assert.AreEqual(5, rich.RequestedDamage);
            Assert.AreEqual(1, rich.HpBefore);
            Assert.AreEqual(0, rich.HpAfter);
            Assert.AreEqual(-1, rich.HpDelta);
            Assert.AreEqual(1, rich.AppliedDamage);
            Assert.AreEqual(1, legacyCount);
            Assert.AreEqual(5, legacyDamage,
                "The legacy PieceAttacked contract continues to report requested damage.");
        }

        [Test]
        public void PendingEnemyQueenDeath_IsResolvedExplicitlyAndEmitsVictoryOnce()
        {
            var board = Board.CreateRectangle(3, 1);
            var player = NewPiece("P", Team.Player, 10, queen: true,
                at: new Axial(0, 0));
            var enemy = NewPiece("E", Team.Enemy, 1, queen: true,
                at: new Axial(1, 0));
            var engine = new CombatEngine(board, new[] { player, enemy });
            engine.SelectPiece(player);
            int combatEndedCount = 0;
            Team? winnerFromEvent = null;
            engine.CombatEnded += winner =>
            {
                combatEndedCount++;
                winnerFromEvent = winner;
            };
            int playerHpBefore = player.Hp;
            Axial playerPositionBefore = player.Coords;

            enemy.TakeDamage(enemy.MaxHp);
            int firstResolved = engine.ResolvePendingDeaths();
            int secondResolved = engine.ResolvePendingDeaths();

            Assert.AreEqual(1, firstResolved);
            Assert.AreEqual(0, secondResolved);
            Assert.IsTrue(engine.IsOver);
            Assert.AreEqual(Team.Player, engine.Winner);
            Assert.AreEqual(Team.Player, winnerFromEvent);
            Assert.AreEqual(1, combatEndedCount);
            Assert.AreEqual(0, engine.TurnCount);
            Assert.AreEqual(playerHpBefore, player.Hp);
            Assert.AreEqual(playerPositionBefore, player.Coords);
            Assert.IsNull(board.OccupantAt(enemy.Coords));
        }

        [Test]
        public void PendingCurrentQueenDeath_IsResolvedExplicitlyAndEmitsDefeatOnce()
        {
            var board = Board.CreateRectangle(3, 1);
            var player = NewPiece("P", Team.Player, 10, queen: true,
                at: new Axial(0, 0));
            var enemy = NewPiece("E", Team.Enemy, 1, queen: true,
                at: new Axial(1, 0));
            var engine = new CombatEngine(board, new[] { player, enemy });
            engine.SelectPiece(player);
            int combatEndedCount = 0;
            Team? winnerFromEvent = null;
            engine.CombatEnded += winner =>
            {
                combatEndedCount++;
                winnerFromEvent = winner;
            };
            int enemyHpBefore = enemy.Hp;
            Axial enemyPositionBefore = enemy.Coords;

            Assert.AreSame(player, engine.Current);
            player.TakeDamage(player.MaxHp);
            int firstResolved = engine.ResolvePendingDeaths();
            int secondResolved = engine.ResolvePendingDeaths();

            Assert.AreEqual(1, firstResolved);
            Assert.AreEqual(0, secondResolved);
            Assert.IsTrue(engine.IsOver);
            Assert.AreEqual(Team.Enemy, engine.Winner);
            Assert.AreEqual(Team.Enemy, winnerFromEvent);
            Assert.AreEqual(1, combatEndedCount);
            Assert.AreEqual(0, engine.TurnCount);
            Assert.AreEqual(enemyHpBefore, enemy.Hp);
            Assert.AreEqual(enemyPositionBefore, enemy.Coords);
            Assert.IsNull(board.OccupantAt(player.Coords));
        }

        [Test]
        public void ResolvePendingDeaths_ProcessesAllDeathsBeforeChoosingWinner()
        {
            var board = Board.CreateRectangle(5, 1);
            var playerQueen = NewPiece("PQ", Team.Player, 10, queen: true,
                at: new Axial(0, 0));
            var playerPawn = NewPiece("PP", Team.Player, 8, at: new Axial(1, 0));
            var enemyQueen = NewPiece("EQ", Team.Enemy, 2, queen: true,
                at: new Axial(3, 0));
            var enemyPawn = NewPiece("EP", Team.Enemy, 1, at: new Axial(4, 0));
            var engine = new CombatEngine(
                board, new[] { playerQueen, playerPawn, enemyQueen, enemyPawn });
            var died = new System.Collections.Generic.List<Piece>();
            int combatEndedCount = 0;
            engine.PieceDied += piece => died.Add(piece);
            engine.CombatEnded += _ => combatEndedCount++;

            enemyQueen.TakeDamage(enemyQueen.MaxHp);
            enemyPawn.TakeDamage(enemyPawn.MaxHp);
            int resolved = engine.ResolvePendingDeaths();
            int repeated = engine.ResolvePendingDeaths();

            Assert.AreEqual(2, resolved);
            Assert.AreEqual(0, repeated);
            CollectionAssert.AreEqual(new[] { enemyQueen, enemyPawn }, died);
            Assert.AreEqual(1, died.Count(piece => piece == enemyQueen));
            Assert.AreEqual(1, died.Count(piece => piece == enemyPawn));
            Assert.IsNull(board.OccupantAt(enemyQueen.Coords));
            Assert.IsNull(board.OccupantAt(enemyPawn.Coords));
            CollectionAssert.DoesNotContain(engine.Turns.Order, enemyQueen);
            CollectionAssert.DoesNotContain(engine.Turns.Order, enemyPawn);
            Assert.AreEqual(Team.Player, engine.Winner);
            Assert.AreEqual(1, combatEndedCount);
        }

        [Test]
        public void ResolvePendingDeaths_BlocksReentrantActionFromPieceDied()
        {
            var board = Board.CreateRectangle(4, 1);
            var player = NewPiece("P", Team.Player, 10, queen: true,
                at: new Axial(0, 0));
            var enemyPawn = NewPiece("EP", Team.Enemy, 2, at: new Axial(2, 0));
            var enemyQueen = NewPiece("EQ", Team.Enemy, 1, queen: true,
                at: new Axial(3, 0));
            var engine = new CombatEngine(board, new[] { player, enemyPawn, enemyQueen });
            engine.SelectPiece(player);
            CombatActionResult reentrantResult = null;
            int pieceDiedCount = 0;
            engine.PieceDied += _ =>
            {
                pieceDiedCount++;
                reentrantResult = engine.ExecuteAction(
                    CombatActionRequest.Pass(engine.Current));
            };

            enemyPawn.TakeDamage(enemyPawn.MaxHp);
            int resolved = engine.ResolvePendingDeaths();

            Assert.AreEqual(1, resolved);
            AssertRejected(
                reentrantResult, CombatActionRejection.StateResolutionInProgress);
            Assert.AreEqual(1, pieceDiedCount);
            Assert.AreEqual(0, engine.TurnCount);
            Assert.AreSame(player, engine.Current);
            Assert.IsFalse(engine.IsOver);
            Assert.AreEqual(0, engine.ResolvePendingDeaths());
            Assert.AreEqual(1, pieceDiedCount);
        }

        [Test]
        public void TypedPassPreviewAndExecution_PreserveMoveOrActEconomy()
        {
            var setup = CreateDuel();
            var request = CombatActionRequest.Pass(setup.Player);

            Assert.IsTrue(setup.Engine.EvaluateAction(request).IsAllowed);
            CombatActionResult result = setup.Engine.ExecuteAction(request);

            Assert.IsTrue(result.WasExecuted);
            Assert.IsNull(setup.Engine.Current);
            Assert.AreEqual(1, setup.Engine.TurnCount);
            AssertRejected(
                setup.Engine.ExecuteAction(CombatActionRequest.Move(setup.Player, new Axial(0, 1))),
                CombatActionRejection.WrongTurn);
        }

        [Test]
        public void Pass_RestoresDefaultManaAndReportsFeedback()
        {
            var setup = CreateDuel(maxMana: 5);
            setup.Player.SpendMana(3);

            CombatActionResult result = setup.Engine.ExecuteAction(
                CombatActionRequest.Pass(setup.Player));

            Assert.AreEqual(2, result.ManaBefore);
            Assert.AreEqual(3, result.ManaAfter);
            Assert.AreEqual(1, result.ManaDelta);
            Assert.AreEqual(3, setup.Player.Mana);
            Assert.IsNull(setup.Engine.Current);
            Assert.AreEqual(1, setup.Engine.TurnCount);
        }

        [Test]
        public void Pass_ClampsRecoveryAtMaxMana()
        {
            var setup = CreateDuel(maxMana: 3);
            setup.Player.SpendMana(1);

            CombatActionResult result = setup.Engine.ExecuteAction(
                CombatActionRequest.Pass(setup.Player));

            Assert.AreEqual(2, result.ManaBefore);
            Assert.AreEqual(3, result.ManaAfter);
            Assert.AreEqual(1, result.ManaDelta);
            Assert.AreEqual(3, setup.Player.Mana);
        }

        [Test]
        public void Pass_WithZeroMaxManaDoesNotCreateManaButStillAdvancesTurn()
        {
            var setup = CreateDuel(maxMana: 0);

            CombatActionResult result = setup.Engine.ExecuteAction(
                CombatActionRequest.Pass(setup.Player));

            Assert.AreEqual(0, result.ManaBefore);
            Assert.AreEqual(0, result.ManaAfter);
            Assert.AreEqual(0, result.ManaDelta);
            Assert.AreEqual(0, setup.Player.Mana);
            Assert.IsNull(setup.Engine.Current);
            Assert.AreEqual(1, setup.Engine.TurnCount);
        }

        [Test]
        public void Pass_UsesConfigurableCoreRecoveryValue()
        {
            var board = Board.CreateRectangle(6, 6);
            var player = NewPiece("P", Team.Player, 10, maxMana: 5);
            var enemy = NewPiece("E", Team.Enemy, 1, at: new Axial(2, 0));
            player.SpendMana(4);
            var engine = new CombatEngine(board, new[] { player, enemy }, passManaRecovery: 2);
            engine.SelectPiece(player);

            CombatActionResult result = engine.ExecuteAction(
                CombatActionRequest.Pass(player));

            Assert.AreEqual(1, result.ManaBefore);
            Assert.AreEqual(3, result.ManaAfter);
            Assert.AreEqual(2, result.ManaDelta);
            Assert.AreEqual(2, engine.PassManaRecovery);
        }        [Test]
        public void Pass_WithMaxIntRecoveryClampsWithoutOverflow()
        {
            var board = Board.CreateRectangle(6, 6);
            var player = NewPiece("P", Team.Player, 10, maxMana: int.MaxValue);
            var enemy = NewPiece("E", Team.Enemy, 1, at: new Axial(2, 0));
            player.SpendMana(int.MaxValue - 1);
            var engine = new CombatEngine(board, new[] { player, enemy }, int.MaxValue);
            engine.SelectPiece(player);

            CombatActionResult result = engine.ExecuteAction(
                CombatActionRequest.Pass(player));

            Assert.AreEqual(1, result.ManaBefore);
            Assert.AreEqual(int.MaxValue, result.ManaAfter);
            Assert.AreEqual(int.MaxValue - 1, result.ManaDelta);
            Assert.AreEqual(int.MaxValue, player.Mana);
        }



        [Test]
        public void LegacyBooleanApis_RemainCompatible()
        {
            var move = CreateDuel(enemyAt: new Axial(3, 0));
            Assert.IsTrue(move.Engine.Move(move.Player, new Axial(0, 1)));

            var attack = CreateDuel(enemyAt: new Axial(1, 0));
            Assert.IsTrue(attack.Engine.Attack(attack.Player, attack.Enemy));

            var ability = DamageAbility(activeRange: 2);
            var cast = CreateDuel(enemyAt: new Axial(2, 0), abilities: new[] { ability });
            Assert.IsTrue(cast.Engine.UseAbility(cast.Player, ability, cast.Enemy.Coords));
        }

        private static void AssertRejected(
            CombatActionResult result,
            CombatActionRejection expected)
        {
            Assert.NotNull(result);
            Assert.IsFalse(result.IsAllowed);
            Assert.IsFalse(result.WasExecuted);
            Assert.AreEqual(expected, result.Rejection);
        }

        private static Duel CreateDuel(
            Axial? playerAt = null,
            Axial? enemyAt = null,
            int moveRange = 3,
            int attackRange = 1,
            int maxMana = 0,
            IAbilityData[] abilities = null)
        {
            var board = Board.CreateRectangle(6, 6);
            var player = NewPiece("P", Team.Player, 10, moveRange: moveRange,
                attackRange: attackRange, maxMana: maxMana,
                abilities: abilities, at: playerAt ?? new Axial(0, 0));
            var enemy = NewPiece("E", Team.Enemy, 1,
                at: enemyAt ?? new Axial(2, 0));
            return new Duel(board, player, enemy);
        }

        private static Piece NewPiece(
            string id,
            Team team,
            int initiative,
            int hp = 10,
            int damage = 1,
            int attackRange = 1,
            int moveRange = 3,
            bool queen = false,
            int maxMana = 0,
            IAbilityData[] abilities = null,
            Axial? at = null) =>
            new Piece(id, team, hp, damage, attackRange, moveRange, initiative,
                isQueen: queen, maxMana: maxMana, abilities: abilities)
            {
                Coords = at ?? new Axial(0, 0),
            };

        private static TestAbility DamageAbility(
            int manaCost = 0,
            int activeRange = 2,
            int effectValue = 1,
            int areaRadius = 0) =>
            new TestAbility
            {
                ManaCost = manaCost,
                ActiveRange = activeRange,
                EffectValue = effectValue,
                AreaRadius = areaRadius,
            };

        private sealed class Duel
        {
            public Duel(Board board, Piece player, Piece enemy)
            {
                Player = player;
                Enemy = enemy;
                Engine = new CombatEngine(board, new[] { player, enemy });
                Engine.SelectPiece(player);
            }

            public CombatEngine Engine { get; }
            public Piece Player { get; }
            public Piece Enemy { get; }
        }

        private sealed class TestAbility : IAbilityData
        {
            public string DisplayName { get; set; } = "Test";
            public AbilityType AbilityType { get; set; } = AbilityType.Active;
            public int ManaCost { get; set; }
            public int ActiveRange { get; set; } = 2;
            public PassiveTrigger Trigger { get; set; } = PassiveTrigger.OnHit;
            public EffectType EffectType { get; set; } = EffectType.Damage;
            public int EffectValue { get; set; } = 1;
            public StatType StatToModify { get; set; } = StatType.Damage;
            public int AreaRadius { get; set; }
            public AffectsTeam AffectsTeam { get; set; } = AffectsTeam.Enemies;
            public DurationType DurationType { get; set; } = DurationType.FixedTurns;
            public int DurationTurns { get; set; } = 1;
        }
    }
}
