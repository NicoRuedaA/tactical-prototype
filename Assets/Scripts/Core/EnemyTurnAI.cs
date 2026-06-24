using System;

namespace Game.Core
{
    /// <summary>
    /// Legacy redirect — use <see cref="DefaultEnemyAI.TakeTurn"/> instead.
    /// </summary>
    [Obsolete("Use DefaultEnemyAI.TakeTurn() instead")]
    public static class EnemyTurnAI
    {
        public static void TakeTurn(CombatEngine engine) =>
            DefaultEnemyAI.TakeTurn(engine);
    }
}
