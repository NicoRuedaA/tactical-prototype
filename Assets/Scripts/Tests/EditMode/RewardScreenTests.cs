using System.Reflection;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RewardScreenTests
{
    [Test]
    public void RewardOption_IsOwnedByCoreAssembly()
    {
        Assert.That(typeof(RewardOption).Assembly.GetName().Name, Is.EqualTo("Game.Core"));
    }

    [Test]
    public void RewardDefinition_ToOption_PreservesAuthoredEffectData()
    {
        var definition = ScriptableObject.CreateInstance<RewardDefinition>();

        try
        {
            definition.description = "Vitality";
            definition.effect = RewardEffectKind.MaxHpBoost;
            definition.stat = StatType.MoveRange;
            definition.amount = 2;

            RewardOption option = definition.ToOption();

            Assert.That(option.Description, Is.EqualTo("Vitality"));
            Assert.That(option.Effect, Is.EqualTo(RewardEffectKind.MaxHpBoost));
            Assert.That(option.Stat, Is.Null);
            Assert.That(option.Amount, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void RewardPoolData_PickOptions_IsDeterministicAndDistinct()
    {
        var pool = ScriptableObject.CreateInstance<RewardPoolData>();
        var definitions = Enumerable.Range(0, 5)
            .Select(index =>
            {
                var definition = ScriptableObject.CreateInstance<RewardDefinition>();
                definition.description = $"Reward {index}";
                return definition;
            })
            .ToArray();

        try
        {
            pool.optionsPerReward = 3;
            pool.rewards = definitions;

            string[] first = pool.PickOptions(9123).Select(option => option.Description).ToArray();
            string[] second = pool.PickOptions(9123).Select(option => option.Description).ToArray();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(3));
            Assert.That(first.Distinct().Count(), Is.EqualTo(3));
        }
        finally
        {
            foreach (var definition in definitions)
                Object.DestroyImmediate(definition);
            Object.DestroyImmediate(pool);
        }
    }

    [Test]
    public void RewardPoolData_PickOptions_ExcludesIncompatibleAbilitiesAndFillsWithoutReplacement()
    {
        var pool = ScriptableObject.CreateInstance<RewardPoolData>();
        var knownAbility = CreateAbility("Known", 3);
        var sameNameDifferentAbility = CreateAbility("Known", 7);
        var recipient = new Piece("recipient", Team.Player, 10, 2, 1, 2, 5);
        recipient.AddAbility(knownAbility);
        var definitions = Enumerable.Range(0, 4)
            .Select(index => ScriptableObject.CreateInstance<RewardDefinition>())
            .ToArray();

        try
        {
            for (int i = 0; i < definitions.Length; i++)
                definitions[i].description = $"Reward {i}";
            definitions[0].incompatibleAbilities = new[] { knownAbility };
            definitions[1].incompatibleAbilities = new[] { sameNameDifferentAbility };
            pool.optionsPerReward = 3;
            pool.rewards = definitions;

            string[] first = pool.PickOptions(9123, recipient).Select(option => option.Description).ToArray();
            string[] second = pool.PickOptions(9123, recipient).Select(option => option.Description).ToArray();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(3));
            Assert.That(first, Does.Not.Contain("Reward 0"));
            Assert.That(first, Does.Contain("Reward 1"), "Canonical identity must not collapse same-name variants.");
            Assert.That(first.Distinct().Count(), Is.EqualTo(3));
        }
        finally
        {
            foreach (var definition in definitions)
                Object.DestroyImmediate(definition);
            Object.DestroyImmediate(knownAbility);
            Object.DestroyImmediate(sameNameDifferentAbility);
            Object.DestroyImmediate(pool);
        }
    }

    [Test]
    public void RewardPoolData_PickOptions_ReturnsFewerCardsWhenCompatiblePoolIsExhausted()
    {
        var pool = ScriptableObject.CreateInstance<RewardPoolData>();
        var knownAbility = CreateAbility("Known", 3);
        var recipient = new Piece("recipient", Team.Player, 10, 2, 1, 2, 5);
        recipient.AddAbility(knownAbility);
        var excluded = ScriptableObject.CreateInstance<RewardDefinition>();
        var compatible = ScriptableObject.CreateInstance<RewardDefinition>();

        try
        {
            excluded.description = "Excluded";
            excluded.incompatibleAbilities = new[] { knownAbility };
            compatible.description = "Compatible";
            pool.optionsPerReward = 3;
            pool.rewards = new[] { excluded, compatible };

            RewardOption[] options = pool.PickOptions(44, recipient);

            Assert.That(options.Select(option => option.Description), Is.EqualTo(new[] { "Compatible" }));
        }
        finally
        {
            Object.DestroyImmediate(excluded);
            Object.DestroyImmediate(compatible);
            Object.DestroyImmediate(knownAbility);
            Object.DestroyImmediate(pool);
        }
    }

    [TestCase(MapNodeType.Combat, 0)]
    [TestCase(MapNodeType.Elite, 1)]
    [TestCase(MapNodeType.Boss, 2)]
    public void SelectRewardPool_UsesEncounterTier(MapNodeType nodeType, int expectedPoolIndex)
    {
        var pools = Enumerable.Range(0, 3)
            .Select(_ => ScriptableObject.CreateInstance<RewardPoolData>())
            .ToArray();

        try
        {
            RewardPoolData selected = RewardScreen.SelectRewardPool(
                nodeType,
                pools[0],
                pools[1],
                pools[2]);

            Assert.That(selected, Is.SameAs(pools[expectedPoolIndex]));
        }
        finally
        {
            foreach (var pool in pools)
                Object.DestroyImmediate(pool);
        }
    }

    [Test]
    public void GenerateRewardOptions_EmptyAuthoredPool_UsesDeterministicFallback()
    {
        var pool = ScriptableObject.CreateInstance<RewardPoolData>();

        try
        {
            string[] expected = RewardScreen.GenerateRewardOptions(4421)
                .Select(option => option.Description)
                .ToArray();
            string[] actual = RewardScreen.GenerateRewardOptions(4421, pool)
                .Select(option => option.Description)
                .ToArray();

            Assert.That(actual, Is.EqualTo(expected));
        }
        finally
        {
            Object.DestroyImmediate(pool);
        }
    }

    [Test]
    public void GenerateRewardOptions_IncompatibleAuthoredPool_DoesNotUseFallbackReplacement()
    {
        var pool = ScriptableObject.CreateInstance<RewardPoolData>();
        var knownAbility = CreateAbility("Known", 3);
        var recipient = new Piece("recipient", Team.Player, 10, 2, 1, 2, 5);
        recipient.AddAbility(knownAbility);
        var excluded = ScriptableObject.CreateInstance<RewardDefinition>();

        try
        {
            excluded.incompatibleAbilities = new[] { knownAbility };
            pool.rewards = new[] { excluded };

            Assert.That(RewardScreen.GenerateRewardOptions(4421, pool, recipient), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(excluded);
            Object.DestroyImmediate(knownAbility);
            Object.DestroyImmediate(pool);
        }
    }

    [Test]
    public void AuthoredEncounterTiers_GenerateDistinctDeterministicOffers()
    {
        const int seed = 4421;
        string[][] offers =
        {
            GenerateAuthoredOfferSignatures("Assets/Data/Rewards/RP_Normal.asset", seed),
            GenerateAuthoredOfferSignatures("Assets/Data/Rewards/RP_Elite.asset", seed),
            GenerateAuthoredOfferSignatures("Assets/Data/Rewards/RP_Boss.asset", seed),
        };

        Assert.That(offers[0], Has.Length.EqualTo(3));
        Assert.That(offers[1], Has.Length.EqualTo(3));
        Assert.That(offers[2], Has.Length.EqualTo(3));
        Assert.That(offers[0], Is.Not.EqualTo(offers[1]));
        Assert.That(offers[0], Is.Not.EqualTo(offers[2]));
        Assert.That(offers[1], Is.Not.EqualTo(offers[2]));

        Assert.That(GenerateAuthoredOfferSignatures("Assets/Data/Rewards/RP_Normal.asset", seed), Is.EqualTo(offers[0]));
        Assert.That(GenerateAuthoredOfferSignatures("Assets/Data/Rewards/RP_Elite.asset", seed), Is.EqualTo(offers[1]));
        Assert.That(GenerateAuthoredOfferSignatures("Assets/Data/Rewards/RP_Boss.asset", seed), Is.EqualTo(offers[2]));
    }

    [TestCase("Power Strike", "Learn: Fireball")]
    [TestCase("Fireball", "Learn: Power Strike")]
    [TestCase("Mend", "Learn: Regeneration")]
    [TestCase("Regeneration", "Learn: Mend")]
    public void NormalRewardPool_OwningCounterpartExcludesApprovedReward(
        string ownedAbilityName,
        string excludedRewardDescription)
    {
        const int seed = 4421;
        var pool = LoadNormalRewardPool();
        var ownedAbility = LoadAbilityByName(pool, ownedAbilityName);
        var recipient = new Piece("recipient", Team.Player, 10, 2, 1, 2, 5);
        recipient.AddAbility(ownedAbility);

        string[] first = pool.PickOptions(seed, recipient).Select(option => option.Description).ToArray();
        string[] second = pool.PickOptions(seed, recipient).Select(option => option.Description).ToArray();

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first, Does.Not.Contain(excludedRewardDescription));
        Assert.That(first, Has.Length.EqualTo(3));
    }

    [Test]
    public void NormalRewardPool_CompatibleAndUnaffectedRecipientsKeepExpectedDeterministicOffers()
    {
        const int seed = 4421;
        var pool = LoadNormalRewardPool();
        var compatibleRecipient = new Piece("compatible", Team.Player, 10, 2, 1, 2, 5);
        var unaffectedRecipient = new Piece("unaffected", Team.Player, 10, 2, 1, 2, 5);

        string[] baseline = pool.PickOptions(seed).Select(option => option.Description).ToArray();
        string[] compatible = pool.PickOptions(seed, compatibleRecipient).Select(option => option.Description).ToArray();
        string[] unaffected = pool.PickOptions(seed, unaffectedRecipient).Select(option => option.Description).ToArray();

        Assert.That(baseline, Is.EqualTo(new[]
        {
            "Learn: Power Strike",
            "Learn: Regeneration",
            "Learn: Fireball",
        }));
        Assert.That(compatible, Is.EqualTo(baseline));
        Assert.That(unaffected, Is.EqualTo(baseline));
    }

    [Test]
    public void ProductionRewardDefinitions_AuthorOnlyApprovedReciprocalExclusions()
    {
        var pool = LoadNormalRewardPool();
        var expected = new[]
        {
            "Fireball->Power Strike",
            "Mend->Regeneration",
            "Power Strike->Fireball",
            "Regeneration->Mend",
        };

        string[] authored = pool.rewards
            .Where(definition => definition.effect == RewardEffectKind.NewAbility)
            .SelectMany(definition => definition.incompatibleAbilities.Select(incompatible =>
                $"{definition.ability.displayName}->{incompatible.displayName}"))
            .OrderBy(value => value)
            .ToArray();

        Assert.That(authored, Is.EqualTo(expected));
        Assert.That(authored, Has.None.Contains("Thorns"));
        Assert.That(authored, Has.None.Contains("War Aura"));
    }

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
    public void FormatRewardPreview_IsRecipientSpecificWithoutMutation()
    {
        var first = new Piece("first", Team.Player, 10, 2, 1, 2, 5, name: "Alpha");
        var second = new Piece("second", Team.Player, 10, 2, 1, 2, 5, name: "Bravo");
        second.AddBonusDamage(3);
        var option = new RewardOption("+2 Damage", RewardEffectKind.StatBoost, StatType.Damage, 2);

        string firstPreview = RewardScreen.FormatRewardPreview(first, option);
        string secondPreview = RewardScreen.FormatRewardPreview(second, option);

        Assert.That(firstPreview, Is.EqualTo("+2 Damage: Damage 2→4"));
        Assert.That(secondPreview, Is.EqualTo("+2 Damage: Damage 5→7"));
        Assert.That(first.EffectiveDamage, Is.EqualTo(2));
        Assert.That(second.EffectiveDamage, Is.EqualTo(5));
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

    private static AbilityData CreateAbility(string displayName, int effectValue)
    {
        var ability = ScriptableObject.CreateInstance<AbilityData>();
        ability.displayName = displayName;
        ability.abilityType = AbilityType.Active;
        ability.manaCost = 2;
        ability.activeRange = 2;
        ability.effectType = EffectType.Damage;
        ability.effectValue = effectValue;
        ability.affectsTeam = AffectsTeam.Enemies;
        return ability;
    }

    private static string[] GenerateAuthoredOfferSignatures(string assetPath, int seed)
    {
        var pool = AssetDatabase.LoadAssetAtPath<RewardPoolData>(assetPath);
        Assert.That(pool, Is.Not.Null, $"Expected an authored reward pool at {assetPath}.");

        return RewardScreen.GenerateRewardOptions(seed, pool)
            .Select(option => $"{option.Description}|{option.Effect}|{option.Stat}|{option.Amount}|{option.Ability?.DisplayName}")
            .ToArray();
    }

    private static RewardPoolData LoadNormalRewardPool()
    {
        const string path = "Assets/Data/Rewards/RP_Normal.asset";
        var pool = AssetDatabase.LoadAssetAtPath<RewardPoolData>(path);
        Assert.That(pool, Is.Not.Null, $"Expected an authored reward pool at {path}.");
        return pool;
    }

    private static AbilityData LoadAbilityByName(RewardPoolData pool, string displayName)
    {
        var ability = pool.rewards
            .Where(definition => definition != null)
            .Select(definition => definition.ability)
            .FirstOrDefault(candidate => candidate != null && candidate.displayName == displayName);
        Assert.That(ability, Is.Not.Null, $"Expected a normal reward definition for {displayName}.");
        return ability;
    }
}
