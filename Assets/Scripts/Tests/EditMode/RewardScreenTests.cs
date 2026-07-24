using System.Reflection;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class RewardScreenTests
{
    [Test]
    public void GetDeterministicAliveRecipients_PreservesRosterOrderAndFiltersDeadPieces()
    {
        var first = new Piece("first", Team.Player, 10, 2, 1, 2, 5, name: "Alpha");
        var fallen = new Piece("fallen", Team.Player, 10, 2, 1, 2, 5, name: "Bravo");
        fallen.TakeDamage(99);
        var last = new Piece("last", Team.Player, 10, 2, 1, 2, 5, name: "Charlie");
        var graph = new MapGraph(
            new[]
            {
                new MapNode("start", MapNodeType.Combat, 0, 0),
                new MapNode("boss", MapNodeType.Boss, 1, 0),
            },
            "start",
            "boss");
        var runState = new RunState(new[] { first, fallen, last }, graph);

        var recipients = RewardScreen.GetDeterministicAliveRecipients(runState);

        Assert.That(recipients.Select(piece => piece.Id), Is.EqualTo(new[] { "first", "last" }));
    }

    [Test]
    public void GenerateRewardOptions_EncodesMaxHpAsExplicitEffect()
    {
        RewardOption maxHp = default;
        bool found = false;

        for (int seed = 0; seed < 100 && !found; seed++)
        {
            foreach (var option in RewardScreen.GenerateRewardOptions(seed))
            {
                if (option.Effect != RewardEffectKind.MaxHpBoost)
                    continue;

                maxHp = option;
                found = true;
                break;
            }
        }

        Assert.That(found, Is.True, "The deterministic reward pool should include a max HP option.");
        Assert.That(maxHp.Effect, Is.EqualTo(RewardEffectKind.MaxHpBoost));
        Assert.That(maxHp.Stat, Is.Null);
        Assert.That(maxHp.Type, Is.EqualTo(RewardType.StatBoost), "Type remains the legacy compatibility value.");
    }

    [Test]
    public void ApplyReward_UsesExplicitMaxHpEffect_WhenDescriptionChanges()
    {
        var piece = new Piece("p1", Team.Player, 10, 2, 1, 2, 5);
        var graph = new MapGraph(
            new[]
            {
                new MapNode("start", MapNodeType.Combat, 0, 0),
                new MapNode("boss", MapNodeType.Boss, 1, 0),
            },
            "start",
            "boss");
        graph.Nodes["start"].ConnectedNodeIds.Add("boss");
        var runState = new RunState(new[] { piece }, graph);
        var option = new RewardOption("Vitality", RewardEffectKind.MaxHpBoost, null, 2);

        var screenObject = new GameObject("RewardScreen Test");
        screenObject.SetActive(false);
        var screen = screenObject.AddComponent<RewardScreen>();

        try
        {
            typeof(RewardScreen)
                .GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(screen, runState);
            typeof(RewardScreen)
                .GetMethod("ApplyReward", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(screen, new object[] { piece, option });

            Assert.That(piece.EffectiveMaxHp, Is.EqualTo(12));
            Assert.That(piece.Hp, Is.EqualTo(12));
        }
        finally
        {
            Object.DestroyImmediate(screenObject);
        }
    }

    [Test]
    public void ApplyReward_AppliesExplicitSelectionOnlyToChosenPiece()
    {
        var selected = new Piece("selected", Team.Player, 10, 2, 1, 2, 5);
        var untouched = new Piece("untouched", Team.Player, 10, 2, 1, 2, 5);
        var graph = new MapGraph(
            new[]
            {
                new MapNode("start", MapNodeType.Combat, 0, 0),
                new MapNode("boss", MapNodeType.Boss, 1, 0),
            },
            "start",
            "boss");
        var runState = new RunState(new[] { selected, untouched }, graph);
        var option = new RewardOption("Precision", RewardEffectKind.StatBoost, StatType.Damage, 2);
        var screenObject = new GameObject("RewardScreen Selection Test");
        screenObject.SetActive(false);
        var screen = screenObject.AddComponent<RewardScreen>();

        try
        {
            typeof(RewardScreen)
                .GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(screen, runState);
            typeof(RewardScreen)
                .GetMethod("ApplyReward", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(screen, new object[] { selected, option });

            Assert.That(selected.EffectiveDamage, Is.EqualTo(4));
            Assert.That(untouched.EffectiveDamage, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(screenObject);
        }
    }

    [Test]
    public void FormatRewardPreview_DamageShowsEffectiveCurrentAndAfterWithoutMutation()
    {
        var piece = new Piece("p1", Team.Player, 10, 2, 1, 2, 5, name: "Alpha");
        var option = new RewardOption("+2 Damage", RewardEffectKind.StatBoost, StatType.Damage, 2);

        string preview = RewardScreen.FormatRewardPreview(piece, option);

        Assert.That(preview, Is.EqualTo("+2 Damage: Damage 2→4"));
        Assert.That(piece.EffectiveDamage, Is.EqualTo(2));
        Assert.That(piece.Hp, Is.EqualTo(10));
    }

    [Test]
    public void FormatRewardPreview_MaxHpShowsHpAndMaxHpCurrentAndAfterWithoutMutation()
    {
        var piece = new Piece("p1", Team.Player, 10, 2, 1, 2, 5, name: "Alpha");
        piece.TakeDamage(3);
        var option = new RewardOption("+2 Max HP", RewardEffectKind.MaxHpBoost, null, 2);

        string preview = RewardScreen.FormatRewardPreview(piece, option);

        Assert.That(preview, Is.EqualTo("+2 Max HP: HP 7/10→9/12"));
        Assert.That(piece.Hp, Is.EqualTo(7));
        Assert.That(piece.EffectiveMaxHp, Is.EqualTo(10));
    }

    [Test]
    public void FormatRewardPreview_AbilityShowsExplicitLearnPreviewWithoutMutation()
    {
        var piece = new Piece("p1", Team.Player, 10, 2, 1, 2, 5, name: "Alpha");
        var ability = new TestAbility("Fireball");
        var option = new RewardOption("Learn: Fireball", RewardEffectKind.NewAbility, null, 0, ability);

        string preview = RewardScreen.FormatRewardPreview(piece, option);

        Assert.That(preview, Is.EqualTo("Learn: Fireball: learn ability Fireball"));
        Assert.That(piece.Abilities, Is.Empty);
        Assert.That(piece.EffectiveDamage, Is.EqualTo(2));
    }

    private sealed class TestAbility : IAbilityData
    {
        public TestAbility(string displayName)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; }
        public AbilityType AbilityType => AbilityType.Active;
        public int ManaCost => 2;
        public int ActiveRange => 2;
        public PassiveTrigger Trigger => PassiveTrigger.OnHit;
        public EffectType EffectType => EffectType.Damage;
        public int EffectValue => 3;
        public StatType StatToModify => StatType.Damage;
        public int AreaRadius => 0;
        public AffectsTeam AffectsTeam => AffectsTeam.Enemies;
        public DurationType DurationType => DurationType.FixedTurns;
        public int DurationTurns => 1;
    }
}
