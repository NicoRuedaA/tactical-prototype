using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Default enemy AI — extracted from the old static EnemyTurnAI.
    /// Priority: ability → attack → move toward → pass.
    /// No instance state — call via <see cref="TakeTurn"/>.
    /// </summary>
    public static class DefaultEnemyAI
    {
        public static void TakeTurn(CombatEngine engine)
        {
            var me = engine.Current;
            if (me == null || engine.IsOver) return;

            var foes = engine.AliveOf(Opponent(me.Team)).ToList();
            if (foes.Count == 0) { engine.Pass(); return; }

            // 1) Try active abilities (highest value first).
            if (TryUseAbility(engine, me, foes)) return;

            // 2) Basic attack.
            var inRange = engine.GetAttackTargets(me).ToList();
            if (inRange.Count > 0)
            {
                var target = inRange
                    .OrderByDescending(t => t.IsQueen)
                    .ThenBy(t => t.Hp)
                    .First();
                engine.Attack(me, target);
                return;
            }

            // 3) Move toward primary target.
            var primary = foes
                .OrderByDescending(t => t.IsQueen)
                .ThenBy(t => Axial.Distance(me.Coords, t.Coords))
                .First();

            var reachable = engine.GetMoveRange(me).ReachableTiles.ToList();
            if (reachable.Count == 0) { engine.Pass(); return; }

            int currentDist = Axial.Distance(me.Coords, primary.Coords);
            var step = reachable
                .OrderBy(c => Axial.Distance(c, primary.Coords))
                .First();

            if (Axial.Distance(step, primary.Coords) < currentDist)
                engine.Move(me, step);
            else
                engine.Pass();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool TryUseAbility(CombatEngine engine, Piece me, System.Collections.Generic.List<Piece> foes)
        {
            IAbilityData bestAbility  = null;
            Axial        bestCenter   = default;
            int          bestScore    = -1;

            foreach (var ability in me.Abilities)
            {
                if (ability.AbilityType != AbilityType.Active) continue;
                if (me.Mana < ability.ManaCost) continue;

                // Determine best target tile: aim at each foe position and evaluate.
                var candidates = ability.AffectsTeam == AffectsTeam.Self
                    ? new[] { me.Coords }
                    : foes
                        .Where(f => Axial.Distance(me.Coords, f.Coords) <= ability.ActiveRange)
                        .Select(f => f.Coords)
                        .ToArray();

                foreach (var center in candidates)
                {
                    var targets = engine.GetAbilityTargets(me, ability, center).ToList();
                    if (targets.Count == 0) continue;

                    int score = ScoreAbility(ability, targets);
                    if (score > bestScore)
                    {
                        bestScore   = score;
                        bestAbility = ability;
                        bestCenter  = center;
                    }
                }
            }

            if (bestAbility == null) return false;

            engine.UseAbility(me, bestAbility, bestCenter);
            return true;
        }

        private static int ScoreAbility(IAbilityData ability, System.Collections.Generic.List<Piece> targets)
        {
            if (ability.EffectType == EffectType.Damage)
            {
                // High score for killing the enemy Queen.
                int score = 0;
                foreach (var t in targets)
                {
                    score += ability.EffectValue;
                    if (t.IsQueen && ability.EffectValue >= t.Hp)
                        score += 1000;
                }
                return score;
            }

            // Healing / buffing own side: score by total value delivered.
            return ability.EffectValue * targets.Count;
        }

        private static Team Opponent(Team team) =>
            team == Team.Player ? Team.Enemy : Team.Player;
    }
}
