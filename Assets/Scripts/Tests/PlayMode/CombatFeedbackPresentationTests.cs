using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.PlayMode.Tests
{
    public sealed class CombatFeedbackPresentationTests
    {
        private CombatRunner _runner;
        private CombatView _view;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            if (RunManager.Instance != null)
                Object.DestroyImmediate(RunManager.Instance.gameObject);

            AsyncOperation load = SceneManager.LoadSceneAsync("Combat", LoadSceneMode.Single);
            while (load != null && !load.isDone)
                yield return null;

            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _runner = Object.FindFirstObjectByType<CombatRunner>();
                if (_runner != null && _runner.HasCombatStarted)
                    break;
                yield return null;
            }

            Assert.That(_runner, Is.Not.Null);
            Assert.That(_runner.HasCombatStarted, Is.True);
            _runner.TurnDelay = 60f;
            _runner.CancelInvoke();
            _view = _runner.CombatView;
            Assert.That(_view, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_runner != null)
                _runner.CancelInvoke();
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator PieceVitalsAndMove_UseEffectiveStatsAndCompleteAtDestination()
        {
            Piece actor = _runner.Engine.Current;
            PieceView actorView = _view.GetPieceView(actor);
            actor.AddBonusMaxHp(5);
            actor.TakeDamage(3);
            if (actor.MaxMana > 0)
                actor.SpendMana(1);

            actorView.RefreshVitals();

            Assert.That(actorView.HasVitalReferences, Is.True);
            Assert.That(actorView.HpFillRatio,
                Is.EqualTo((float)actor.Hp / actor.EffectiveMaxHp).Within(0.0001f));
            float expectedMana = actor.MaxMana > 0 ? (float)actor.Mana / actor.MaxMana : 0f;
            Assert.That(actorView.ManaFillRatio, Is.EqualTo(expectedMana).Within(0.0001f));

            Axial destination = _runner.Engine.Board.Tiles
                .Select(tile => tile.Coords)
                .First(coord => _runner.Engine.EvaluateAction(
                    CombatActionRequest.Move(actor, coord)).IsAllowed);
            Vector3 expectedPosition = HexLayout.AxialToWorld(destination);
            actorView.MoveDuration = 0.03f;
            actorView.SetCompleteAnimationsImmediately(false);

            Assert.That(_runner.Engine.Move(actor, destination), Is.True);
            _runner.CancelInvoke();
            Assert.That(actorView.IsMoving, Is.True);

            float deadline = Time.realtimeSinceStartup + 1f;
            while (actorView != null && actorView.IsMoving && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(actorView, Is.Not.Null);
            Assert.That(actorView.IsMoving, Is.False);
            Assert.That(Vector3.Distance(actorView.transform.position, expectedPosition),
                Is.LessThan(0.0001f));
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator EngineAbilityEvents_PresentEveryActualDeltaExactlyOnce()
        {
            Piece caster = _runner.Engine.AliveOf(Team.Player)
                .First(piece => piece.MaxMana > 0);
            Piece ally = _runner.Engine.AliveOf(Team.Player)
                .First(piece => piece != caster);
            AdvanceTo(caster);
            _view.SetCompleteAnimationsImmediately(true);
            var feedback = new List<CombatFeedbackRecord>();
            void Capture(CombatFeedbackRecord record) => feedback.Add(record);
            _view.FeedbackPresented += Capture;

            var damageAbility = new StubAbility(
                EffectType.Damage,
                manaCost: 1,
                effectValue: 1,
                activeRange: 100,
                areaRadius: 100,
                affectsTeam: AffectsTeam.Enemies);
            caster.AddAbility(damageAbility);
            Piece damageCenter = _runner.Engine.AliveOf(Team.Enemy).First();
            CombatActionResult damagePreview = _runner.Engine.EvaluateAction(
                CombatActionRequest.UseAbility(caster, damageAbility, damageCenter.Coords));
            Assert.That(damagePreview.IsAllowed, Is.True);
            Piece[] damagedTargets = damagePreview.LegalTargets.ToArray();
            Assert.That(damagedTargets.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(_runner.Engine.UseAbility(
                caster, damageAbility, damageCenter.Coords), Is.True);
            _runner.CancelInvoke();
            foreach (Piece target in damagedTargets)
            {
                PieceView targetView = _view.GetPieceView(target);
                Assert.That(targetView.HpFillRatio,
                    Is.EqualTo((float)target.Hp / target.EffectiveMaxHp).Within(0.0001f));
            }

            AdvanceTo(caster);
            ally.TakeDamage(2);
            var healAbility = new StubAbility(
                EffectType.Heal,
                effectValue: 2,
                activeRange: 100,
                areaRadius: 0,
                affectsTeam: AffectsTeam.Allies);
            caster.AddAbility(healAbility);
            Assert.That(_runner.Engine.UseAbility(
                caster, healAbility, ally.Coords), Is.True);
            _runner.CancelInvoke();

            AdvanceTo(caster);
            var manaAbility = new StubAbility(
                EffectType.ManaRestore,
                effectValue: 1,
                affectsTeam: AffectsTeam.Self);
            caster.AddAbility(manaAbility);
            Assert.That(_runner.Engine.UseAbility(
                caster, manaAbility, caster.Coords), Is.True);
            _runner.CancelInvoke();

            AdvanceTo(caster);
            var buffAbility = new StubAbility(
                EffectType.Buff,
                effectValue: 1,
                activeRange: 100,
                areaRadius: 0,
                affectsTeam: AffectsTeam.Allies);
            caster.AddAbility(buffAbility);
            Assert.That(_runner.Engine.UseAbility(
                caster, buffAbility, ally.Coords), Is.True);
            _runner.CancelInvoke();
            _view.FeedbackPresented -= Capture;

            CombatFeedbackRecord[] damage = feedback
                .Where(record => record.Kind == CombatFeedbackKind.Damage)
                .ToArray();
            Assert.That(damage, Has.Length.EqualTo(damagedTargets.Length));
            CollectionAssert.AreEquivalent(
                damagedTargets,
                damage.Select(record => record.Piece).ToArray());
            Assert.That(damage.All(record => record.Amount == 1 && record.Label == "-1"),
                Is.True);
            Assert.That(damage.GroupBy(record => record.Piece).All(group => group.Count() == 1),
                Is.True, "Every ability target must receive exactly one damage presentation.");

            CombatFeedbackRecord heal = feedback.Single(record =>
                record.Kind == CombatFeedbackKind.Heal);
            Assert.That(heal.Piece, Is.SameAs(ally));
            Assert.That(heal.Amount, Is.EqualTo(2));

            CombatFeedbackRecord[] mana = feedback
                .Where(record => record.Kind == CombatFeedbackKind.Mana)
                .ToArray();
            Assert.That(mana, Has.Length.EqualTo(2));
            CollectionAssert.AreEquivalent(new[] { -1, 1 }, mana.Select(record => record.Amount));
            Assert.That(mana.All(record => record.Piece == caster), Is.True);

            CombatFeedbackRecord buff = feedback.Single(record =>
                record.Kind == CombatFeedbackKind.Buff);
            Assert.That(buff.Piece, Is.SameAs(ally));
            Assert.That(buff.Amount, Is.EqualTo(1));
            Assert.That(feedback, Has.Count.EqualTo(damagedTargets.Length + 4));

            Assert.That(_view.GetPieceView(caster).ManaFillRatio,
                Is.EqualTo((float)caster.Mana / caster.MaxMana).Within(0.0001f));
            Assert.That(_view.ActivePopupCount, Is.Zero,
                "Immediate test mode must return every popup to the pool synchronously.");
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator BasicAttackOverkill_PresentsOnlyActualAppliedDamage()
        {
            Piece attacker = _runner.Engine.Current;
            Piece target = _runner.Engine.AliveOf(Team.Enemy)
                .First(piece => !piece.IsQueen);
            attacker.AddBonusAttackRange(100);
            target.TakeDamage(target.Hp - 1);
            Assert.That(target.Hp, Is.EqualTo(1));
            _view.SetCompleteAnimationsImmediately(true);
            var feedback = new List<CombatFeedbackRecord>();
            void Capture(CombatFeedbackRecord record) => feedback.Add(record);
            _view.FeedbackPresented += Capture;

            Assert.That(_runner.Engine.Attack(attacker, target), Is.True);
            _runner.CancelInvoke();
            _view.FeedbackPresented -= Capture;

            CombatFeedbackRecord damage = feedback.Single(record =>
                record.Kind == CombatFeedbackKind.Damage);
            Assert.That(damage.Piece, Is.SameAs(target));
            Assert.That(damage.Amount, Is.EqualTo(1));
            Assert.That(damage.Label, Is.EqualTo("-1"));
            Assert.That(feedback, Has.Count.EqualTo(1),
                "CombatView must consume only the rich attack event, never duplicate legacy feedback.");
            Assert.That(_view.ActivePopupCount, Is.Zero);
            yield return null;
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator DeathAndPopupSkip_CleanUpExactlyOnce()
        {
            Piece doomed = _runner.Engine.AliveOf(Team.Enemy)
                .First(piece => !piece.IsQueen);
            PieceView doomedView = _view.GetPieceView(doomed);
            doomedView.HitDuration = 0f;
            doomedView.DeathDuration = 0.03f;
            doomedView.SetCompleteAnimationsImmediately(false);

            LogAssert.Expect(LogType.Log, $"<color=red>{doomed.Name} died</color>");
            doomed.TakeDamage(doomed.Hp);
            Assert.That(_runner.Engine.ResolvePendingDeaths(), Is.EqualTo(1));
            Assert.That(doomedView.IsDying, Is.True);
            Assert.That(_runner.Engine.ResolvePendingDeaths(), Is.Zero,
                "Core and presentation must not process the same death twice.");
            Assert.That(_view.HasActiveFeedback, Is.True);
            _view.CompleteActiveFeedbackImmediately();
            _view.CompleteActiveFeedbackImmediately();
            Assert.That(_view.GetPieceView(doomed), Is.Null);
            Assert.That(_view.HasActiveFeedback, Is.False);
            yield return null;
            Assert.That(doomedView == null, Is.True,
                "Skipping death feedback must destroy the dying GameObject once.");

            Piece survivor = _runner.Engine.AliveOf(Team.Enemy).First();
            _view.FloatingTextDuration = 0.03f;
            _view.SetCompleteAnimationsImmediately(false);
            _view.PresentAbilityResolution(new AbilityResolution(
                _runner.Engine.AliveOf(Team.Player).First(),
                new StubAbility(EffectType.Damage),
                false,
                null,
                new[] { survivor },
                new[] { Change(survivor, survivor.Hp + 1, survivor.Hp) }));
            Assert.That(_view.ActivePopupCount, Is.EqualTo(1));
            Assert.That(_view.HasActiveFeedback, Is.True);
            _view.CompleteActiveFeedbackImmediately();
            _view.CompleteActiveFeedbackImmediately();
            Assert.That(_view.ActivePopupCount, Is.Zero);
            Assert.That(_view.PooledPopupCount, Is.EqualTo(1));
            Assert.That(_view.HasActiveFeedback, Is.False);

            _view.FloatingTextDuration = 10f;
            _view.PresentAbilityResolution(new AbilityResolution(
                _runner.Engine.AliveOf(Team.Player).First(),
                new StubAbility(EffectType.Damage),
                false,
                null,
                new[] { survivor },
                new[] { Change(survivor, survivor.Hp + 1, survivor.Hp) }));
            Assert.That(_view.ActivePopupCount, Is.EqualTo(1));
            Assert.That(_view.PooledPopupCount, Is.Zero,
                "The second popup must reuse the pooled instance.");

            Object.Destroy(_view.gameObject);
            yield return null;
            yield return null;

            int leakedPopups = Resources.FindObjectsOfTypeAll<GameObject>()
                .Count(gameObject => gameObject.scene.IsValid()
                                     && gameObject.name == "Combat Feedback Popup");
            Assert.That(leakedPopups, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private static AbilityEffectChange Change(Piece target, int hpBefore, int hpAfter)
        {
            return new AbilityEffectChange(
                target, hpBefore, hpAfter,
                target.Mana, target.Mana,
                target.ActiveBuffs.Count, target.ActiveBuffs.Count);
        }

        private void AdvanceTo(Piece piece)
        {
            _runner.CancelInvoke();
            int remaining = _runner.Engine.Turns.Count + 1;
            while (!_runner.Engine.IsOver
                   && _runner.Engine.Current != piece
                   && remaining-- > 0)
            {
                _runner.Engine.Pass();
                _runner.CancelInvoke();
            }

            Assert.That(_runner.Engine.IsOver, Is.False);
            Assert.That(_runner.Engine.Current, Is.SameAs(piece));
        }

        private sealed class StubAbility : IAbilityData
        {
            public StubAbility(
                EffectType effectType,
                int manaCost = 0,
                int effectValue = 1,
                int activeRange = 5,
                int areaRadius = 1,
                AffectsTeam affectsTeam = AffectsTeam.Enemies)
            {
                EffectType = effectType;
                ManaCost = manaCost;
                EffectValue = effectValue;
                ActiveRange = activeRange;
                AreaRadius = areaRadius;
                AffectsTeam = affectsTeam;
            }

            public string DisplayName => "Feedback Test";
            public AbilityType AbilityType => AbilityType.Active;
            public int ManaCost { get; }
            public int ActiveRange { get; }
            public PassiveTrigger Trigger => PassiveTrigger.OnHit;
            public EffectType EffectType { get; }
            public int EffectValue { get; }
            public StatType StatToModify => StatType.Damage;
            public int AreaRadius { get; }
            public AffectsTeam AffectsTeam { get; }
            public DurationType DurationType => DurationType.FixedTurns;
            public int DurationTurns => 1;
        }
    }

    public sealed class CombatFeedbackSchedulingTests
    {
        [UnityTest, Timeout(15000)]
        public IEnumerator AiTurn_WaitsForRealMovementFeedbackBeforeActing()
        {
            if (RunManager.Instance != null)
                Object.DestroyImmediate(RunManager.Instance.gameObject);

            AsyncOperation load = SceneManager.LoadSceneAsync("Combat", LoadSceneMode.Single);
            while (load != null && !load.isDone)
                yield return null;

            CombatRunner runner = null;
            float startupDeadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < startupDeadline)
            {
                runner = Object.FindFirstObjectByType<CombatRunner>();
                if (runner != null && runner.HasCombatStarted)
                    break;
                yield return null;
            }

            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.HasCombatStarted, Is.True);
            Assert.That(runner.AutoPlayBothSides, Is.False);
            runner.TurnDelay = 0.05f;

            Piece lastPlayer = runner.Engine.AliveOf(Team.Player).Last();
            while (runner.Engine.Current != lastPlayer)
            {
                Assert.That(runner.Engine.Current.Team, Is.EqualTo(Team.Player));
                runner.Engine.Pass();
            }

            Piece actor = runner.Engine.Current;
            Axial destination = runner.Engine.Board.Tiles
                .Select(tile => tile.Coords)
                .First(coord => runner.Engine.EvaluateAction(
                    CombatActionRequest.Move(actor, coord)).IsAllowed);
            PieceView actorView = runner.CombatView.GetPieceView(actor);
            actorView.MoveDuration = 0.35f;
            actorView.SetCompleteAnimationsImmediately(false);
            int turnBeforeMove = runner.Engine.TurnCount;

            Assert.That(runner.Engine.Move(actor, destination), Is.True);
            Piece scheduledEnemy = runner.Engine.Current;
            int turnAfterMove = runner.Engine.TurnCount;
            Assert.That(scheduledEnemy.Team, Is.EqualTo(Team.Enemy));
            Assert.That(turnAfterMove, Is.EqualTo(turnBeforeMove + 1));
            Assert.That(runner.CombatView.HasActiveFeedback, Is.True);

            yield return new WaitForSecondsRealtime(0.12f);

            Assert.That(runner.Engine.Current, Is.SameAs(scheduledEnemy));
            Assert.That(runner.Engine.TurnCount, Is.EqualTo(turnAfterMove),
                "The scheduled AI turn must wait while movement feedback remains active.");
            Assert.That(runner.CombatView.HasActiveFeedback, Is.True);

            float aiDeadline = Time.realtimeSinceStartup + 2f;
            while (runner.Engine.TurnCount == turnAfterMove
                   && Time.realtimeSinceStartup < aiDeadline)
                yield return null;

            Assert.That(runner.Engine.TurnCount, Is.EqualTo(turnAfterMove + 1),
                "Exactly one deferred AI action must run after feedback completes.");
        }
    }
}
