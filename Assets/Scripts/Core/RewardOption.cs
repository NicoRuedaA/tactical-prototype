namespace Game.Core
{
    /// <summary>
    /// Reward types available for post-combat selection.
    /// </summary>
    public enum RewardType { StatBoost, NewAbility }

    /// <summary>
    /// Explicit gameplay effect carried by a reward option.
    /// </summary>
    public enum RewardEffectKind { StatBoost, MaxHpBoost, NewAbility }

    /// <summary>
    /// Describes a single reward option the player can pick.
    /// </summary>
    public readonly struct RewardOption
    {
        public readonly string Description;
        public readonly RewardType Type;
        public readonly RewardEffectKind Effect;
        public readonly StatType? Stat;
        public readonly int Amount;
        public readonly IAbilityData Ability;

        public RewardOption(string description, RewardType type, StatType? stat, int amount, IAbilityData ability = null)
            : this(description, ToEffectKind(type), type, stat, amount, ability)
        {
        }

        public RewardOption(string description, RewardEffectKind effect, StatType? stat, int amount, IAbilityData ability = null)
            : this(description, effect, ToRewardType(effect), stat, amount, ability)
        {
        }

        private RewardOption(
            string description,
            RewardEffectKind effect,
            RewardType type,
            StatType? stat,
            int amount,
            IAbilityData ability)
        {
            Description = description;
            Type = type;
            Effect = effect;
            Stat = stat;
            Amount = amount;
            Ability = ability;
        }

        private static RewardEffectKind ToEffectKind(RewardType type)
        {
            return type == RewardType.NewAbility
                ? RewardEffectKind.NewAbility
                : RewardEffectKind.StatBoost;
        }

        private static RewardType ToRewardType(RewardEffectKind effect)
        {
            return effect == RewardEffectKind.NewAbility
                ? RewardType.NewAbility
                : RewardType.StatBoost;
        }
    }
}
