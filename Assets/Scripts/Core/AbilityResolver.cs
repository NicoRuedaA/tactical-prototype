using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Stateless helper: resolves ability targets and applies effects.
    /// Contains no Unity types — fully unit-testable.
    /// Note: WhileInArea is only meaningful for Buff/Debuff effects.
    ///       For damage-over-time, use FixedTurns + OnTurnStart trigger instead.
    /// </summary>
    public static class AbilityResolver
    {
        public static List<Piece> GetTargets(
            IAbilityData ability, Piece caster, Board board,
            IEnumerable<Piece> allPieces, Axial center)
        {
            var candidates = allPieces.Where(p => !p.IsDead);

            candidates = ability.AreaRadius == 0
                ? candidates.Where(p => p.Coords == center)
                : candidates.Where(p => Axial.Distance(p.Coords, center) <= ability.AreaRadius);

            candidates = ability.AffectsTeam switch
            {
                AffectsTeam.Self    => candidates.Where(p => p == caster),
                AffectsTeam.Allies  => candidates.Where(p => p.Team == caster.Team),
                AffectsTeam.Enemies => candidates.Where(p => p.Team != caster.Team),
                _                   => candidates,
            };

            return candidates.ToList();
        }

        public static void Apply(IAbilityData ability, Piece caster, IEnumerable<Piece> targets)
        {
            foreach (var target in targets)
            {
                switch (ability.EffectType)
                {
                    case EffectType.Damage:
                        target.TakeDamage(ability.EffectValue);
                        break;
                    case EffectType.Heal:
                        target.Heal(ability.EffectValue);
                        break;
                    case EffectType.ManaRestore:
                        target.RestoreMana(ability.EffectValue);
                        break;
                    case EffectType.Buff:
                    case EffectType.Debuff:
                        int turns = ability.DurationType == DurationType.FixedTurns
                            ? ability.DurationTurns : -1;
                        target.ApplyBuff(new ActiveBuff(ability, caster, turns));
                        break;
                }
            }
        }
    }
}
