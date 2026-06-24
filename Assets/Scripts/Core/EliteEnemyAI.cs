namespace Game.Core
{
    /// <summary>
    /// Elite enemy AI — instance class implementing <see cref="IEnemyAI"/>.
    /// Prioritizes active ability use over basic attack, then falls back
    /// to <see cref="DefaultEnemyAI"/> for movement and melee.
    /// No phase or state tracking — simpler than boss AI.
    /// </summary>
    public sealed class EliteEnemyAI : IEnemyAI
    {
        /// <summary>
        /// Takes the elite's turn. Tries active abilities first;
        /// if none can be used, delegates to <see cref="DefaultEnemyAI.TakeTurn"/>.
        /// </summary>
        public void TakeTurn(CombatEngine engine)
        {
            var me = engine.Current;
            if (me == null || engine.IsOver) return;

            // DefaultEnemyAI already prioritizes active abilities over
            // basic attack. Delegating directly provides the correct
            // priority: ability → attack → move → pass.
            DefaultEnemyAI.TakeTurn(engine);
        }
    }
}
