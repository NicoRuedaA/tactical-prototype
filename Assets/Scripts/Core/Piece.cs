using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// A logical combat unit. No Unity types — fully unit-testable.
    /// Effective stats (EffectiveDamage, EffectiveAttackRange, EffectiveMoveRange)
    /// incorporate active buffs and debuffs dynamically.
    /// </summary>
    public sealed class Piece
    {
        public string Id   { get; }
        public string Name { get; set; }
        public Team   Team { get; }
        public bool   IsQueen { get; }

        // --- Base stats ---
        public int MaxHp      { get; }
        public int Hp         { get; private set; }
        public int MaxMana    { get; }
        public int Mana       { get; private set; }
        public int Damage     { get; }
        public int AttackRange { get; }
        public int MoveRange  { get; }
        public int Initiative { get; }

        // --- Bonus stat fields (run progression buffs) ---
        private int _bonusHp;
        private int _bonusDamage;
        private int _bonusAttackRange;
        private int _bonusMoveRange;

        // --- Effective stats (base + bonuses + active buff modifiers) ---
        public int EffectiveDamage      => Damage      + _bonusDamage      + SumBuffs(StatType.Damage);
        public int EffectiveAttackRange => AttackRange + _bonusAttackRange + SumBuffs(StatType.AttackRange);
        public int EffectiveMoveRange   => MoveRange   + _bonusMoveRange   + SumBuffs(StatType.MoveRange);
        public int EffectiveMaxHp       => MaxHp       + _bonusHp;

        public Axial Coords { get; set; }
        public bool  IsDead => Hp <= 0;

        // --- Abilities ---
        private readonly List<IAbilityData> _abilities;
        public IReadOnlyList<IAbilityData> Abilities => _abilities;

        // --- Active buffs / debuffs ---
        private readonly List<ActiveBuff> _buffs = new List<ActiveBuff>();
        public IReadOnlyList<ActiveBuff> ActiveBuffs => _buffs;

        public Piece(string id, Team team, int maxHp, int damage,
                     int attackRange, int moveRange, int initiative,
                     bool isQueen = false, string name = null,
                     int maxMana = 0, IEnumerable<IAbilityData> abilities = null)
        {
            Id         = id;
            Team       = team;
            MaxHp      = maxHp;
            Hp         = maxHp;
            MaxMana    = maxMana < 0 ? 0 : maxMana;
            Mana       = MaxMana;
            Damage     = damage;
            AttackRange = attackRange < 1 ? 1 : attackRange;
            MoveRange  = moveRange  < 0 ? 0 : moveRange;
            Initiative = initiative;
            IsQueen    = isQueen;
            Name       = name ?? id;
            _abilities = abilities != null
                ? new List<IAbilityData>(abilities)
                : new List<IAbilityData>();
        }

        // --- HP ---
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            Hp -= amount;
            if (Hp < 0) Hp = 0;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            Hp += amount;
            if (Hp > EffectiveMaxHp) Hp = EffectiveMaxHp;
        }

        /// <summary>
        /// Heals the piece by <paramref name="percent"/>% of EffectiveMaxHp.
        /// Only heals alive pieces. Clamps at EffectiveMaxHp.
        /// </summary>
        public void HealPercentEffective(int percent)
        {
            if (percent <= 0 || IsDead) return;
            int healAmount = (EffectiveMaxHp * percent) / 100;
            Heal(healAmount);
        }

        // --- Mana ---
        public bool SpendMana(int amount)
        {
            if (amount <= 0 || Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        public void RestoreMana(int amount)
        {
            if (amount <= 0) return;
            Mana += amount;
            if (Mana > MaxMana) Mana = MaxMana;
        }

        // --- Run progression (ability / stat boost management) ---

        public void AddAbility(IAbilityData ability)
        {
            if (ability == null) return;
            _abilities.Add(ability);
        }

        public void AddBonusDamage(int amount)      => _bonusDamage += amount;
        public void AddBonusAttackRange(int amount)  => _bonusAttackRange += amount;
        public void AddBonusMoveRange(int amount)    => _bonusMoveRange += amount;

        /// <summary>
        /// Increases max HP by <paramref name="amount"/> and heals by the same
        /// amount so the piece effectively gains that much max HP without
        /// requiring a separate heal.
        /// Note: does NOT use Heal() because Heal clamps at base MaxHp, not EffectiveMaxHp.
        /// </summary>
        public void AddBonusMaxHp(int amount)
        {
            _bonusHp += amount;
            Hp += amount;
            if (Hp > EffectiveMaxHp) Hp = EffectiveMaxHp; // safety clamp
        }

        // --- Buff management ---
        public void ApplyBuff(ActiveBuff buff) => _buffs.Add(buff);

        public void RemoveBuff(ActiveBuff buff) => _buffs.Remove(buff);

        /// <summary>Decrement fixed-duration buffs; removes expired ones.</summary>
        public void TickBuffs()
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].IsAura) continue;
                _buffs[i].RemainingTurns--;
                if (_buffs[i].RemainingTurns <= 0)
                    _buffs.RemoveAt(i);
            }
        }

        /// <summary>Remove all WhileInArea buffs. Called before every aura re-evaluation.</summary>
        public void ClearAuraBuffs() =>
            _buffs.RemoveAll(b => b.IsAura);

        private int SumBuffs(StatType stat)
        {
            int sum = 0;
            foreach (var b in _buffs)
            {
                if (b.Source.StatToModify != stat) continue;
                sum += b.Source.EffectType == EffectType.Buff
                    ? b.Source.EffectValue
                    : -b.Source.EffectValue;
            }
            return sum;
        }
    }
}
