namespace Game.Core
{
    /// <summary>
    /// Pure interface so Game.Core can resolve ability effects without depending on
    /// UnityEngine. The Unity layer implements this via AbilityData (ScriptableObject).
    /// Tests implement it via plain C# stubs.
    /// </summary>
    public interface IAbilityData
    {
        string DisplayName   { get; }
        AbilityType AbilityType { get; }

        // --- Active only ---
        int ManaCost    { get; }
        int ActiveRange { get; }

        // --- Passive only ---
        PassiveTrigger Trigger { get; }

        // --- Shared ---
        EffectType EffectType   { get; }
        int        EffectValue  { get; }
        StatType   StatToModify { get; }   // meaningful only for Buff / Debuff

        int         AreaRadius  { get; }   // 0 = single target / self
        AffectsTeam AffectsTeam { get; }

        DurationType DurationType  { get; }
        int          DurationTurns { get; } // meaningful only for FixedTurns
    }
}
