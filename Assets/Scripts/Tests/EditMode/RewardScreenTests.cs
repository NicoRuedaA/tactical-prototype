using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class RewardScreenTests
{
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
}
