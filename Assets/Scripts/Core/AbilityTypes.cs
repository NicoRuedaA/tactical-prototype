namespace Game.Core
{
    public enum AbilityType   { Active, Passive }
    public enum PassiveTrigger { OnHit, OnTurnStart, OnDeath, OnTakeDamage }
    public enum EffectType    { Damage, Heal, ManaRestore, Buff, Debuff }
    public enum AffectsTeam   { Self, Allies, Enemies, All }
    public enum StatType      { Damage, AttackRange, MoveRange }
    public enum DurationType  { FixedTurns, WhileInArea }
}
