using UnityEngine;
using Game.Core;

/// <summary>
/// Data-driven ability definition. One ScriptableObject per ability variant.
/// Create via: Right-click -> Create -> TacticalRogue -> Ability Data
/// </summary>
[CreateAssetMenu(fileName = "NewAbility", menuName = "TacticalRogue/Ability Data")]
public class AbilityData : ScriptableObject, IAbilityData
{
    [Header("Identity")]
    public string displayName = "New Ability";

    [Header("Type")]
    public AbilityType abilityType = AbilityType.Active;

    [Header("Active")]
    public int manaCost   = 0;
    public int activeRange = 1;

    [Header("Passive")]
    public PassiveTrigger trigger = PassiveTrigger.OnHit;

    [Header("Effect")]
    public EffectType effectType  = EffectType.Damage;
    public int        effectValue = 1;
    public StatType   statToModify = StatType.Damage;

    [Header("Area")]
    public int         areaRadius  = 0;
    public AffectsTeam affectsTeam = AffectsTeam.Enemies;

    [Header("Duration")]
    public DurationType durationType  = DurationType.FixedTurns;
    public int          durationTurns = 1;

    // IAbilityData — explicit to avoid polluting the inspector with extra properties
    string         IAbilityData.DisplayName    => displayName;
    AbilityType    IAbilityData.AbilityType    => abilityType;
    int            IAbilityData.ManaCost       => manaCost;
    int            IAbilityData.ActiveRange    => activeRange;
    PassiveTrigger IAbilityData.Trigger        => trigger;
    EffectType     IAbilityData.EffectType     => effectType;
    int            IAbilityData.EffectValue    => effectValue;
    StatType       IAbilityData.StatToModify   => statToModify;
    int            IAbilityData.AreaRadius     => areaRadius;
    AffectsTeam    IAbilityData.AffectsTeam    => affectsTeam;
    DurationType   IAbilityData.DurationType   => durationType;
    int            IAbilityData.DurationTurns  => durationTurns;
}
