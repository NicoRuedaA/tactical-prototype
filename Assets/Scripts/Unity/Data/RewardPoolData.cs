using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEngine;

/// <summary>Authored pool of rewards with deterministic, compatibility-aware picks.</summary>
[CreateAssetMenu(fileName = "NewRewardPool", menuName = "TacticalRogue/Reward Pool")]
public sealed class RewardPoolData : ScriptableObject
{
    [Min(1)]
    public int optionsPerReward = 3;

    public RewardDefinition[] rewards = Array.Empty<RewardDefinition>();

    public IReadOnlyList<RewardDefinition> GetCompatibleDefinitions(Piece recipient = null)
    {
        return (rewards ?? Array.Empty<RewardDefinition>())
            .Where(definition => definition != null && definition.IsCompatibleWith(recipient))
            .ToArray();
    }

    public RewardOption[] PickOptions(int streamSeed, Piece recipient = null)
    {
        var definitions = GetCompatibleDefinitions(recipient);
        int count = Math.Min(Math.Max(0, optionsPerReward), definitions.Count);
        if (count == 0)
            return Array.Empty<RewardOption>();

        var rng = new DeterministicRandom(streamSeed);
        var picks = rng.PickDistinctIndices(definitions.Count, count);
        return picks.Select(index => definitions[index].ToOption()).ToArray();
    }
}
