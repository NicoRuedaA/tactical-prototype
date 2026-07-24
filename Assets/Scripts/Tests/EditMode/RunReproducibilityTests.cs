using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

public sealed class RunReproducibilityTests
{
    [Test]
    public void SameSeedAndProgress_ProducesSameRunSnapshot()
    {
        string first = BuildRunSnapshot(812734, progressIndex: 2);
        string second = BuildRunSnapshot(812734, progressIndex: 2);

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void DifferentSeeds_CanProduceDifferentRunSnapshots()
    {
        var snapshots = new HashSet<string>();
        for (int seed = 1; seed <= 12; seed++)
            snapshots.Add(BuildRunSnapshot(seed, progressIndex: 1));

        Assert.That(snapshots.Count, Is.GreaterThan(1));
    }

    [Test]
    public void RewardStreams_AreStableAndIndependent()
    {
        const int runSeed = 14021989;
        const int progress = 3;

        int optionsSeedA = RunSeedStreams.Derive(runSeed, RunRandomStream.RewardOptions, progress);
        int optionsSeedB = RunSeedStreams.Derive(runSeed, RunRandomStream.RewardOptions, progress);
        int recipientSeed = RunSeedStreams.Derive(runSeed, RunRandomStream.RewardRecipient, progress);

        Assert.That(optionsSeedB, Is.EqualTo(optionsSeedA));
        Assert.That(recipientSeed, Is.Not.EqualTo(optionsSeedA));

        string optionsA = RewardSignature(RewardScreen.GenerateRewardOptions(optionsSeedA));
        string optionsB = RewardSignature(RewardScreen.GenerateRewardOptions(optionsSeedB));
        Assert.That(optionsB, Is.EqualTo(optionsA));
    }

    [Test]
    public void RunManager_GeneratesStoresAndUsesCurrentRunSeed()
    {
        var managerObject = new GameObject("Generated Seed RunManager");
        var playerData = CreateCharacter("Player");

        try
        {
            var manager = managerObject.AddComponent<RunManager>();
            manager.PlayerTeam = new[] { playerData };

            InitializeWithoutSceneLoad(manager, seed: null);

            Assert.That(manager.CurrentRunSeed, Is.Not.Zero);
            int storedSeed = manager.CurrentRunSeed;
            string actualMap = MapSignature(manager.CurrentRun.Graph);
            string expectedMap = MapSignature(MapGenerator.Generate(storedSeed, rows: 2, nodesPerRow: 3));

            Assert.That(actualMap, Is.EqualTo(expectedMap));
            manager.GetStreamSeed(RunRandomStream.RewardOptions);
            Assert.That(manager.CurrentRunSeed, Is.EqualTo(storedSeed));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(playerData);
        }
    }

    [Test]
    public void RunManager_AcceptsExplicitSeedAndProgressesRewardStreams()
    {
        var managerObject = new GameObject("Explicit Seed RunManager");
        var playerData = CreateCharacter("Player");

        try
        {
            var manager = managerObject.AddComponent<RunManager>();
            manager.PlayerTeam = new[] { playerData };

            InitializeWithoutSceneLoad(manager, 424242);
            SetCombatIndex(manager, 2);

            Assert.That(manager.CurrentRunSeed, Is.EqualTo(424242));
            Assert.That(manager.CurrentCombatIndex, Is.EqualTo(2));
            Assert.That(
                manager.GetStreamSeed(RunRandomStream.RewardOptions),
                Is.EqualTo(RunSeedStreams.Derive(424242, RunRandomStream.RewardOptions, 2)));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(playerData);
        }
    }

    [Test]
    public void RunManager_CapturesPendingRestResult_ForAlivePiecesAndClampedDeltas()
    {
        var managerObject = new GameObject("Rest Result RunManager");
        var playerA = CreateCharacter("Player A");
        var playerB = CreateCharacter("Player B");

        try
        {
            var manager = managerObject.AddComponent<RunManager>();
            manager.PlayerTeam = new[] { playerA, playerB };
            manager.RestHealPercent = 50;
            InitializeWithoutSceneLoad(manager, 919191);

            var alive = manager.CurrentRun.Pieces[0];
            var dead = manager.CurrentRun.Pieces[1];
            alive.TakeDamage(8); // 2 -> 7 after a 50% heal, clamped by MaxHp.
            dead.TakeDamage(10);

            typeof(RunManager)
                .GetMethod("ApplyRestHeal", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(manager, null);

            var result = manager.PendingRestHealResult;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ConfiguredPercent, Is.EqualTo(50));
            Assert.That(result.TotalDelta, Is.EqualTo(5));
            Assert.That(result.Pieces.Count, Is.EqualTo(1));
            Assert.That(result.Pieces[0].PieceId, Is.EqualTo(alive.Id));
            Assert.That(result.Pieces[0].BeforeHp, Is.EqualTo(2));
            Assert.That(result.Pieces[0].AfterHp, Is.EqualTo(7));
            Assert.That(result.Pieces[0].Delta, Is.EqualTo(5));

            Assert.That(manager.ConsumePendingRestHealResult(), Is.SameAs(result));
            Assert.That(manager.ConsumePendingRestHealResult(), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(playerA);
            Object.DestroyImmediate(playerB);
        }
    }

    private static string BuildRunSnapshot(int seed, int progressIndex)
    {
        var managerObject = new GameObject($"Snapshot RunManager {seed}");
        var assets = new List<CharacterData>
        {
            CreateCharacter("Player A"),
            CreateCharacter("Player B"),
            CreateCharacter("Enemy A"),
            CreateCharacter("Enemy B"),
        };

        try
        {
            var manager = managerObject.AddComponent<RunManager>();
            manager.PlayerTeam = new[] { assets[0], assets[1] };
            manager.enemyTeamPools = new[]
            {
                new TeamRoster { nodeType = MapNodeType.Combat, enemies = new[] { assets[2] } },
                new TeamRoster { nodeType = MapNodeType.Combat, enemies = new[] { assets[3] } },
            };

            InitializeWithoutSceneLoad(manager, seed);
            SetCombatIndex(manager, progressIndex);

            var rewards = RewardScreen.GenerateRewardOptions(
                manager.GetStreamSeed(RunRandomStream.RewardOptions));
            var recipient = RewardScreen.SelectRewardRecipient(
                manager.CurrentRun,
                manager.GetStreamSeed(RunRandomStream.RewardRecipient));
            var encounter = manager.GetEnemyTeam(MapNodeType.Combat);

            return string.Join("|",
                MapSignature(manager.CurrentRun.Graph),
                string.Join(",", encounter.Select(character => character.displayName)),
                RewardSignature(rewards),
                recipient?.Id ?? "none");
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            foreach (var asset in assets)
                Object.DestroyImmediate(asset);
        }
    }

    private static CharacterData CreateCharacter(string displayName)
    {
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.displayName = displayName;
        data.maxHp = 10;
        data.damage = 2;
        data.attackRange = 1;
        data.moveRange = 2;
        data.initiative = 5;
        return data;
    }

    private static void InitializeWithoutSceneLoad(RunManager manager, int? seed)
    {
        typeof(RunManager)
            .GetMethod("StartNewRunInternal", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(manager, new object[] { seed, false });
    }

    private static void SetCombatIndex(RunManager manager, int value)
    {
        typeof(RunManager)
            .GetField("_currentCombatIndex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(manager, value);
    }

    private static string RewardSignature(IEnumerable<RewardOption> rewards)
    {
        return string.Join(",", rewards.Select(reward => reward.Description));
    }

    private static string MapSignature(MapGraph graph)
    {
        return string.Join(";", graph.Nodes.Values
            .OrderBy(node => node.Id)
            .Select(node => $"{node.Id}:{node.Type}:{string.Join(",", node.ConnectedNodeIds)}"));
    }
}
