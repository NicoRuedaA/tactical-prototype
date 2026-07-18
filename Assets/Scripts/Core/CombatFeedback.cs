using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Actual HP change produced by one basic attack.</summary>
    public sealed class AttackResolution
    {
        public AttackResolution(
            Piece attacker,
            Piece target,
            int requestedDamage,
            int hpBefore,
            int hpAfter)
        {
            Attacker = attacker;
            Target = target;
            RequestedDamage = requestedDamage;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
        }

        public Piece Attacker { get; }
        public Piece Target { get; }
        public int RequestedDamage { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public int HpDelta => HpAfter - HpBefore;
        public int AppliedDamage => System.Math.Max(0, -HpDelta);
    }

    /// <summary>Before/after state for one target affected by an ability.</summary>
    public sealed class AbilityEffectChange
    {
        public AbilityEffectChange(
            Piece target,
            int hpBefore,
            int hpAfter,
            int manaBefore,
            int manaAfter,
            int buffsBefore,
            int buffsAfter)
        {
            Target = target;
            HpBefore = hpBefore;
            HpAfter = hpAfter;
            ManaBefore = manaBefore;
            ManaAfter = manaAfter;
            BuffsBefore = buffsBefore;
            BuffsAfter = buffsAfter;
        }

        public Piece Target { get; }
        public int HpBefore { get; }
        public int HpAfter { get; }
        public int ManaBefore { get; }
        public int ManaAfter { get; }
        public int BuffsBefore { get; }
        public int BuffsAfter { get; }
        public int HpDelta => HpAfter - HpBefore;
        public int ManaDelta => ManaAfter - ManaBefore;
        public int BuffDelta => BuffsAfter - BuffsBefore;
    }

    /// <summary>Presentation-neutral feedback for an active or passive ability.</summary>
    public sealed class AbilityResolution
    {
        public AbilityResolution(
            Piece source,
            IAbilityData ability,
            bool isPassive,
            PassiveTrigger? trigger,
            IReadOnlyList<Piece> targets,
            IReadOnlyList<AbilityEffectChange> changes)
        {
            Source = source;
            Ability = ability;
            IsPassive = isPassive;
            Trigger = trigger;
            Targets = targets ?? Array.Empty<Piece>();
            Changes = changes ?? Array.Empty<AbilityEffectChange>();
        }

        public Piece Source { get; }
        public IAbilityData Ability { get; }
        public bool IsPassive { get; }
        public PassiveTrigger? Trigger { get; }
        public IReadOnlyList<Piece> Targets { get; }
        public IReadOnlyList<AbilityEffectChange> Changes { get; }
    }

    /// <summary>One boss phase transition emitted by Core as a typed event payload.</summary>
    public sealed class BossPhaseTransition
    {
        public BossPhaseTransition(
            Piece boss,
            int phase,
            IAbilityData grantedAbility,
            int damageBonus)
        {
            Boss = boss;
            Phase = phase;
            GrantedAbility = grantedAbility;
            DamageBonus = damageBonus;
        }

        public Piece Boss { get; }
        public int Phase { get; }
        public IAbilityData GrantedAbility { get; }
        public int DamageBonus { get; }
    }
}
