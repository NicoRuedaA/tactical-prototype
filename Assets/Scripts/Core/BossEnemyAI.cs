using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Boss enemy AI — phase-aware instance class implementing <see cref="IEnemyAI"/>.
    /// Phase 1 (HP > threshold): delegates to <see cref="DefaultEnemyAI"/>.
    /// Phase transition (first turn HP <= threshold): adds phase ability and damage buff.
    /// Phase 2 (HP <= threshold): on TurnCount % 3 == 0, forces the phase ability.
    /// Otherwise falls back to <see cref="DefaultEnemyAI"/>.
    /// </summary>
    public sealed class BossEnemyAI : IEnemyAI
    {
        private readonly Piece          _boss;
        private readonly IAbilityData   _phaseAbility;
        private readonly int            _damageBuff;
        private readonly int            _phaseThresholdPercent;
        private          bool           _phaseTriggered;

        /// <summary>True after phase transition has fired once.</summary>
        public bool IsPhaseTriggered => _phaseTriggered;

        /// <param name="boss">The boss piece this AI controls.</param>
        /// <param name="phaseAbility">Ability granted on phase transition (e.g. Infernal Blast).</param>
        /// <param name="damageBuff">Bonus damage applied on phase transition (default 2).</param>
        /// <param name="phaseThresholdPercent">HP percentage that triggers phase 2 (default 50).</param>
        public BossEnemyAI(Piece boss, IAbilityData phaseAbility,
            int damageBuff = 2, int phaseThresholdPercent = 50)
        {
            _boss                 = boss;
            _phaseAbility         = phaseAbility;
            _damageBuff           = damageBuff;
            _phaseThresholdPercent = phaseThresholdPercent;
        }

        public void TakeTurn(CombatEngine engine)
        {
            if (_boss == null || _boss.IsDead || engine.IsOver) return;
            if (engine.Current != _boss) return;

            // ── Phase transition check ────────────────────────────────
            // Fires once: when boss HP first drops below threshold.
            if (!_phaseTriggered
                && _boss.Hp < (_boss.EffectiveMaxHp * _phaseThresholdPercent / 100))
            {
                _phaseTriggered = true;

                if (_phaseAbility != null)
                    _boss.AddAbility(_phaseAbility);

                if (_damageBuff > 0)
                    _boss.AddBonusDamage(_damageBuff);
            }

            // ── Phase 2 AoE schedule ──────────────────────────────────
            // On every 3rd combat turn (TurnCount > 0, % 3 == 0),
            // force-use the phase ability instead of default AI.
            if (_phaseTriggered && _phaseAbility != null
                && engine.TurnCount > 0 && engine.TurnCount % 3 == 0
                && _boss.Mana >= _phaseAbility.ManaCost)
            {
                var foes = engine.AliveOf(Opponent(_boss.Team)).ToList();
                if (foes.Count > 0)
                {
                    // Target queen if alive, otherwise closest foe.
                    var target = foes.FirstOrDefault(f => f.IsQueen)
                        ?? foes.OrderBy(f => Axial.Distance(_boss.Coords, f.Coords))
                               .First();

                    if (engine.UseAbility(_boss, _phaseAbility, target.Coords))
                        return;
                }
            }

            // ── Default AI (ability → attack → move → pass) ──────────
            DefaultEnemyAI.TakeTurn(engine);
        }

        private static Team Opponent(Team team) =>
            team == Team.Player ? Team.Enemy : Team.Player;
    }
}
