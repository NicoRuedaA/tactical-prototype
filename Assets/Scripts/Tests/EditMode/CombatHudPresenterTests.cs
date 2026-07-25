using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.Core.Tests
{
    public sealed class CombatHudPresenterTests
    {
        [Test]
        public void Build_ReportsCurrentResourcesTurnOrderAndMoveOrActRule()
        {
            var board = Board.CreateRectangle(3, 1);
            var player = new Piece(
                "P", Team.Player, 10, 2, 1, 2, 10,
                name: "Vanguard", maxMana: 5)
            {
                Coords = new Axial(0, 0),
            };
            player.AddBonusMaxHp(3);
            player.TakeDamage(2);
            player.SpendMana(1);
            var enemy = new Piece("E", Team.Enemy, 8, 1, 1, 1, 1, name: "Raider")
            {
                Coords = new Axial(2, 0),
            };
            var engine = new CombatEngine(board, new[] { player, enemy });

            engine.SelectPiece(player);
            CombatHudState state = new CombatHudPresenter().Build(engine, false, player);

            Assert.That(state.ActiveUnit, Is.EqualTo("Active: Vanguard (Player)"));
            Assert.That(state.Resources, Is.EqualTo("HP 11/13  |  Mana 4/5"));
            Assert.That(state.TurnOrder, Is.EqualTo("Turn order: ▶ Vanguard  ›  Raider"));
            Assert.That(state.ActionRule, Is.EqualTo("ONE ACTION: Move, Attack, Ability, or Pass."));
            Assert.That(state.Controls, Is.EqualTo(
                "Mouse: left-click move/attack/target. 1-9: ability. Right-click/Esc: cancel. Space: pass. Enter: activate focused UI, or pass with no focus."));
            Assert.That(state.CanPass, Is.True);
        }

        [Test]
        public void Build_KeepsActiveAbilityIndicesStableWhenManaOrTargetsAreMissing()
        {
            var expensive = new TestAbility("Meteor", 9, 3);
            var unavailableTarget = new TestAbility("Jab", 0, 1);
            var usable = new TestAbility("Bolt", 2, 3);
            var board = Board.CreateRectangle(3, 1);
            var player = new Piece(
                "P", Team.Player, 10, 2, 1, 2, 10,
                name: "Mage", maxMana: 5,
                abilities: new IAbilityData[] { expensive, unavailableTarget, usable })
            {
                Coords = new Axial(0, 0),
            };
            var enemy = new Piece("E", Team.Enemy, 8, 1, 1, 1, 1)
            {
                Coords = new Axial(2, 0),
            };
            var engine = new CombatEngine(board, new[] { player, enemy });

            engine.SelectPiece(player);
            CombatHudState state = new CombatHudPresenter().Build(engine, false, player);

            Assert.That(state.Abilities.Select(item => item.Hotkey), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(state.Abilities.Select(item => item.Name),
                Is.EqualTo(new[] { "Meteor", "Jab", "Bolt" }));
            Assert.That(state.Abilities.Select(item => item.IsEnabled),
                Is.EqualTo(new[] { false, false, true }));
            Assert.That(state.Abilities.Select(item => item.CanAttempt),
                Is.EqualTo(new[] { true, true, true }));
            Assert.That(state.Abilities[0].UnavailableReason,
                Is.EqualTo(CombatActionRejection.InsufficientMana));
            Assert.That(state.Abilities[1].UnavailableReason,
                Is.EqualTo(CombatActionRejection.NoLegalTargets));
            Assert.That(state.Abilities[0].Label, Is.EqualTo("[1] Meteor — 9 Mana"));
            Assert.That(state.Abilities[2].Label, Is.EqualTo("[3] Bolt — 2 Mana"));
        }

        [Test]
        public void Build_LabelsOnlyNineKeyboardAbilitiesAndMarksLaterAbilitiesForClick()
        {
            IAbilityData[] abilities = Enumerable.Range(1, 10)
                .Select(index => (IAbilityData)new TestAbility($"Ability {index}", 0, 5))
                .ToArray();
            var board = Board.CreateRectangle(2, 1);
            var player = new Piece(
                "P", Team.Player, 10, 1, 1, 1, 10,
                abilities: abilities)
            {
                Coords = new Axial(0, 0),
            };
            var enemy = new Piece("E", Team.Enemy, 10, 1, 1, 1, 1)
            {
                Coords = new Axial(1, 0),
            };
            var engine = new CombatEngine(board, new[] { player, enemy });

            engine.SelectPiece(player);
            CombatHudState state = new CombatHudPresenter().Build(engine, false, player);

            Assert.That(state.Abilities, Has.Count.EqualTo(10));
            Assert.That(state.Abilities[8].HasHotkey, Is.True);
            Assert.That(state.Abilities[8].Label, Does.StartWith("[9]"));
            Assert.That(state.Abilities[9].HasHotkey, Is.False);
            Assert.That(state.Abilities[9].Label, Is.EqualTo("Click: Ability 10 — 0 Mana"));
        }

        [Test]
        public void TileAndPieceViews_ReuseSharedMaterialsAndUsePropertyBlocksForTint()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            var normal = new Material(shader);
            var reachable = new Material(shader);
            var attackable = new Material(shader);
            var selected = new Material(shader);
            var ability = new Material(shader);
            var hpMaterial = new Material(shader);
            var tileObject = new GameObject("Inactive Tile Fixture");
            var pieceObject = new GameObject("Inactive Piece Fixture");
            var hpObject = new GameObject("Inactive HP Fixture");
            tileObject.SetActive(false);
            pieceObject.SetActive(false);
            hpObject.SetActive(false);
            try
            {
                Renderer tileRenderer = tileObject.AddComponent<MeshRenderer>();
                var tileView = tileObject.AddComponent<TileView>();
                SetPrivateField(tileView, "_renderer", tileRenderer);
                SetPrivateField(tileView, "_propertyBlock", new MaterialPropertyBlock());
                tileView.AssignMaterials(normal, reachable, attackable, selected, ability);
                tileView.SetHighlight(TileHighlight.Invalid);
                Assert.That(tileRenderer.sharedMaterial,
                    Is.SameAs(selected), "TileView must assign the selected asset as sharedMaterial.");

                Renderer pieceRenderer = pieceObject.AddComponent<MeshRenderer>();
                var pieceView = pieceObject.AddComponent<PieceView>();
                SetPrivateField(pieceView, "_renderer", pieceRenderer);
                SetPrivateField(pieceView, "_hpPropertyBlock", new MaterialPropertyBlock());
                SetPrivateField(pieceView, "_indicatorPropertyBlock", new MaterialPropertyBlock());
                pieceView.Piece = new Piece("P", Team.Player, 10, 1, 1, 1, 1);
                pieceView.AssignMaterial(normal);
                Assert.That(pieceRenderer.sharedMaterial,
                    Is.SameAs(normal), "PieceView base material must remain the shared team asset.");

                hpObject.transform.SetParent(pieceObject.transform, false);
                Renderer hpRenderer = hpObject.AddComponent<MeshRenderer>();
                hpRenderer.sharedMaterial = hpMaterial;
                pieceView.SetHpBarReferences(hpObject, hpObject.transform, hpRenderer);
                pieceView.RefreshVitals();
                Assert.That(hpRenderer.sharedMaterial, Is.SameAs(hpMaterial),
                    "HP tint must not instantiate or replace the fill material.");
                var hpProperties = new MaterialPropertyBlock();
                hpRenderer.GetPropertyBlock(hpProperties);
                Assert.That(hpProperties.isEmpty, Is.False);

            }
            finally
            {
                Object.DestroyImmediate(tileObject);
                Object.DestroyImmediate(pieceObject);
                Object.DestroyImmediate(normal);
                Object.DestroyImmediate(reachable);
                Object.DestroyImmediate(attackable);
                Object.DestroyImmediate(selected);
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(hpMaterial);
            }
        }

        [Test]
        public void LegalMoveAndAttackPreviews_MatchCoreEvaluationExactly()
        {
            var board = Board.CreateRectangle(4, 2);
            var player = new Piece("P", Team.Player, 10, 2, 2, 2, 10)
            {
                Coords = new Axial(0, 0),
            };
            var ally = new Piece("A", Team.Player, 10, 1, 1, 1, 5)
            {
                Coords = new Axial(1, 0),
            };
            var enemy = new Piece("E", Team.Enemy, 8, 1, 1, 1, 1)
            {
                Coords = new Axial(2, 0),
            };
            var engine = new CombatEngine(board, new[] { player, ally, enemy });
            engine.SelectPiece(player);
            var presenter = new CombatHudPresenter();

            var expectedMoves = board.Tiles
                .Select(tile => tile.Coords)
                .Where(coord => engine.EvaluateAction(
                    CombatActionRequest.Move(player, coord)).IsAllowed);
            var expectedAttacks = engine.Pieces
                .Where(target => engine.EvaluateAction(
                    CombatActionRequest.Attack(player, target)).IsAllowed);

            Assert.That(presenter.GetLegalMoveCoords(engine, player),
                Is.EquivalentTo(expectedMoves));
            Assert.That(presenter.GetLegalAttackTargets(engine, player),
                Is.EquivalentTo(expectedAttacks));
        }

        [Test]
        public void RejectionMessages_CoverEveryTypedCoreRejection()
        {
            var presenter = new CombatHudPresenter();

            foreach (CombatActionRejection rejection in
                     System.Enum.GetValues(typeof(CombatActionRejection)))
            {
                string message = presenter.GetRejectionMessage(rejection);
                if (rejection == CombatActionRejection.None)
                    Assert.That(message, Is.Empty);
                else
                    Assert.That(message, Is.Not.Empty, rejection.ToString());
            }

            Assert.That(presenter.GetRejectionMessage(CombatActionRejection.WrongTurn),
                Is.EqualTo("Not your turn."));
            Assert.That(presenter.GetRejectionMessage(CombatActionRejection.InsufficientMana),
                Is.EqualTo("Not enough mana."));
            Assert.That(presenter.GetRejectionMessage(CombatActionRejection.NoLegalTargets),
                Is.EqualTo("No legal targets."));
        }

        [Test]
        public void Build_RotatesTurnOrderSoCurrentActorIsFirst()
        {
            var board = Board.CreateRectangle(2, 1);
            var first = new Piece("P", Team.Player, 10, 1, 1, 1, 10, name: "First")
            {
                Coords = new Axial(0, 0),
            };
            var second = new Piece("E", Team.Enemy, 10, 1, 1, 1, 1, name: "Second")
            {
                Coords = new Axial(1, 0),
            };
            var engine = new CombatEngine(board, new[] { first, second });
            engine.SelectPiece(first);
            engine.Pass();
            engine.SelectPiece(second);

            CombatHudState state = new CombatHudPresenter().Build(engine, false, second);

            Assert.That(engine.Current, Is.SameAs(second));
            Assert.That(state.TurnOrder, Is.EqualTo("Turn order: ▶ Second  ›  First"));
        }

        [Test]
        public void LegalAbilityTargets_UseCorePreviewAndIncludeCasterForAllies()
        {
            var ability = new TestAbility("Mend", 0, 2, AffectsTeam.Allies);
            var board = Board.CreateRectangle(3, 1);
            var caster = new Piece(
                "Caster", Team.Player, 10, 1, 1, 1, 10,
                abilities: new[] { ability })
            {
                Coords = new Axial(0, 0),
            };
            var ally = new Piece("Ally", Team.Player, 10, 1, 1, 1, 5)
            {
                Coords = new Axial(1, 0),
            };
            var enemy = new Piece("Enemy", Team.Enemy, 10, 1, 1, 1, 1)
            {
                Coords = new Axial(2, 0),
            };
            var engine = new CombatEngine(board, new[] { caster, ally, enemy });
            engine.SelectPiece(caster);

            var targets = new CombatHudPresenter()
                .GetLegalAbilityTargetCoords(engine, caster, ability);

            Assert.That(targets, Is.EquivalentTo(new[] { caster.Coords, ally.Coords }));
            foreach (Axial target in targets)
            {
                Assert.That(engine.EvaluateAction(
                    CombatActionRequest.UseAbility(caster, ability, target)).IsAllowed, Is.True);
            }
        }

        [Test]
        public void CombatScene_HasOneSerializedHudAndExplicitWiring()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Combat.unity", OpenSceneMode.Single);

            var huds = Object.FindObjectsByType<CombatHudView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var eventSystems = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var inputModules = Object.FindObjectsByType<InputSystemUIInputModule>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var runner = Object.FindFirstObjectByType<CombatRunner>();
            var input = Object.FindFirstObjectByType<PlayerInputController>();

            Assert.That(scene.name, Is.EqualTo("Combat"));
            Assert.That(huds, Has.Length.EqualTo(1));
            Assert.That(huds[0].IsConfigured, Is.True);
            Assert.That(eventSystems, Has.Length.EqualTo(1));
            Assert.That(inputModules, Has.Length.EqualTo(1));
            Assert.That(runner.CombatView, Is.SameAs(Object.FindFirstObjectByType<CombatView>()));
            Assert.That(runner.PlayerInput, Is.SameAs(input));
            Assert.That(input.Runner, Is.SameAs(runner));
            Assert.That(input.CombatView, Is.SameAs(runner.CombatView));
            Assert.That(input.TargetCamera, Is.SameAs(Camera.main));
            Assert.That(input.CombatHud, Is.SameAs(huds[0]));
        }

        [Test]
        public void HudActionPanel_HidesAllLowerControlsUntilPieceIsSelected()
        {
            var panel = new GameObject("Action Panel");
            var rule = new GameObject("Action Rule");
            rule.transform.SetParent(panel.transform);
            var abilities = new GameObject("Abilities").AddComponent<RectTransform>();
            abilities.SetParent(panel.transform);
            var pass = new GameObject("Pass").AddComponent<UnityEngine.UI.Button>();
            pass.transform.SetParent(panel.transform);
            var hudObject = new GameObject("HUD");
            var hud = hudObject.AddComponent<CombatHudView>();
            hud.ActionRuleText = rule.AddComponent<UnityEngine.UI.Text>();
            hud.AbilitiesContainer = abilities;
            hud.PassButton = pass;
            try
            {
                hud.SetSelectionVisible(false);
                Assert.That(panel.activeSelf, Is.False);
                hud.SetSelectionVisible(true);
                Assert.That(panel.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(panel);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing fixture field {fieldName}.");
            field.SetValue(target, value);
        }

        private sealed class TestAbility : IAbilityData
        {
            public TestAbility(
                string name,
                int manaCost,
                int activeRange,
                AffectsTeam affectsTeam = AffectsTeam.Enemies)
            {
                DisplayName = name;
                ManaCost = manaCost;
                ActiveRange = activeRange;
                AffectsTeam = affectsTeam;
            }

            public string DisplayName { get; }
            public AbilityType AbilityType => AbilityType.Active;
            public int ManaCost { get; }
            public int ActiveRange { get; }
            public PassiveTrigger Trigger => PassiveTrigger.OnHit;
            public EffectType EffectType => EffectType.Damage;
            public int EffectValue => 1;
            public StatType StatToModify => StatType.Damage;
            public int AreaRadius => 0;
            public AffectsTeam AffectsTeam { get; }
            public DurationType DurationType => DurationType.FixedTurns;
            public int DurationTurns => 1;
        }
    }
}
