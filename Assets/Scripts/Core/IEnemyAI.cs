namespace Game.Core
{
    /// <summary>
    /// Strategy interface for per-type enemy AI.
    /// Implementations define how an enemy piece decides its action
    /// when its turn arrives.
    /// </summary>
    public interface IEnemyAI
    {
        void TakeTurn(CombatEngine engine);
    }
}
