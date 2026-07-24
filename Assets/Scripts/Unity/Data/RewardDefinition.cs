using System;
using Game.Core;
using UnityEngine;

/// <summary>Authored reward definition used by reward pools.</summary>
[CreateAssetMenu(fileName = "NewReward", menuName = "TacticalRogue/Reward Definition")]
public sealed class RewardDefinition : ScriptableObject
{
    [Header("Presentation")]
    public string description = "New Reward";

    [Header("Effect")]
    public RewardEffectKind effect = RewardEffectKind.StatBoost;
    public StatType stat = StatType.Damage;
    public int amount = 1;

    [Header("Ability Reward")]
    public AbilityData ability;

    [Tooltip("Ability definitions that prevent this reward from being offered to a piece.")]
    public AbilityData[] incompatibleAbilities = Array.Empty<AbilityData>();

    public RewardOption ToOption()
    {
        return new RewardOption(description, effect, effect == RewardEffectKind.StatBoost ? stat : (StatType?)null, amount, ability);
    }

    public bool IsCompatibleWith(Piece recipient)
    {
        if (recipient == null || incompatibleAbilities == null || incompatibleAbilities.Length == 0)
            return true;

        foreach (var existing in recipient.Abilities)
        {
            if (existing == null)
                continue;
            foreach (var incompatible in incompatibleAbilities)
            {
                if (incompatible != null && string.Equals(existing.DisplayName, incompatible.displayName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }
}
