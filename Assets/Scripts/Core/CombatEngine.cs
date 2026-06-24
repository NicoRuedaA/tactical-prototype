using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Combat rules engine.
    /// Turn economy: one action per turn — Move | Attack | UseAbility | Pass.
    /// Win condition: a team loses when its Queen dies (chess rule).
    /// Passive abilities fire on their trigger; WhileInArea buffs are re-evaluated
    /// at the start of every turn.
    /// </summary>
    public sealed class CombatEngine
    {
        private readonly List<Piece>      _pieces;
        private readonly HashSet<Piece>   _removedPieces = new HashSet<Piece>();
        private readonly Piece            _playerQueen;
        private readonly Piece            _enemyQueen;

        public Board      Board  { get; }
        public TurnSystem Turns  { get; }

        public IReadOnlyList<Piece> Pieces  => _pieces;
        public Piece                Current => Turns.Current;
        public Team?                Winner  { get; private set; }
        public bool                 IsOver  => Winner.HasValue;

        // Events
        public event Action<Piece, Axial, Axial>             PieceMoved;
        public event Action<Piece, Piece, int>               PieceAttacked;
        public event Action<Piece>                           PieceDied;
        public event Action<Piece>                           TurnChanged;
        public event Action<Team>                            CombatEnded;
        public event Action<Piece, IAbilityData, IReadOnlyList<Piece>> AbilityUsed;
        public event Action                                  OnTurnStart;

        public int TurnCount { get; private set; }

        public CombatEngine(Board board, IEnumerable<Piece> pieces)
        {
            Board   = board;
            _pieces = pieces.ToList();
            foreach (var p in _pieces)
                Board.Place(p, p.Coords);

            _playerQueen = _pieces.FirstOrDefault(p => p.Team == Team.Player && p.IsQueen);
            _enemyQueen  = _pieces.FirstOrDefault(p => p.Team == Team.Enemy  && p.IsQueen);

            Turns = new TurnSystem(_pieces);
        }

        public void Begin()
        {
            OnTurnStart?.Invoke();
            TurnChanged?.Invoke(Current);
        }

        // ── Queries ──────────────────────────────────────────────────────────

        public IEnumerable<Piece> AliveOf(Team team) =>
            _pieces.Where(p => p.Team == team && !p.IsDead);

        public BfsResult GetMoveRange(Piece piece) =>
            Pathfinding.GetReachable(Board, piece.Coords, piece.EffectiveMoveRange);

        public IEnumerable<Piece> GetAttackTargets(Piece piece) =>
            _pieces.Where(t => !t.IsDead
                               && t.Team != piece.Team
                               && Axial.Distance(piece.Coords, t.Coords) <= piece.EffectiveAttackRange);

        public IEnumerable<Piece> GetAbilityTargets(Piece caster, IAbilityData ability, Axial center) =>
            AbilityResolver.GetTargets(ability, caster, Board, _pieces, center);

        // ── Actions ──────────────────────────────────────────────────────────

        public bool Move(Piece piece, Axial dest)
        {
            if (IsOver || piece != Current || piece.IsDead) return false;
            if (!GetMoveRange(piece).CanReach(dest)) return false;

            var from = piece.Coords;
            Board.MovePiece(piece, dest);
            PieceMoved?.Invoke(piece, from, dest);
            EndTurn();
            return true;
        }

        public bool Attack(Piece attacker, Piece target)
        {
            if (IsOver || attacker != Current || attacker.IsDead) return false;
            if (target == null || target.IsDead || target.Team == attacker.Team) return false;
            if (Axial.Distance(attacker.Coords, target.Coords) > attacker.EffectiveAttackRange) return false;

            int dmg = attacker.EffectiveDamage;
            target.TakeDamage(dmg);
            PieceAttacked?.Invoke(attacker, target, dmg);

            if (!target.IsDead)
                TriggerPassives(target, PassiveTrigger.OnTakeDamage);

            if (!IsOver && !attacker.IsDead)
                TriggerPassives(attacker, PassiveTrigger.OnHit);

            ProcessNewDeaths();

            if (!IsOver) EndTurn();
            return true;
        }

        public bool UseAbility(Piece caster, IAbilityData ability, Axial targetCoord)
        {
            if (IsOver || caster != Current || caster.IsDead) return false;
            if (ability == null || ability.AbilityType != AbilityType.Active) return false;
            if (!caster.Abilities.Contains(ability)) return false;
            if (caster.Mana < ability.ManaCost) return false;

            bool selfTarget = ability.AffectsTeam == AffectsTeam.Self;
            if (!selfTarget && Axial.Distance(caster.Coords, targetCoord) > ability.ActiveRange)
                return false;

            Axial center = selfTarget ? caster.Coords : targetCoord;

            caster.SpendMana(ability.ManaCost);
            var targets = AbilityResolver.GetTargets(ability, caster, Board, _pieces, center);
            AbilityResolver.Apply(ability, caster, targets);
            AbilityUsed?.Invoke(caster, ability, targets);

            ProcessNewDeaths();

            if (!IsOver) EndTurn();
            return true;
        }

        public void Pass()
        {
            if (!IsOver) EndTurn();
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void EndTurn()
        {
            if (IsOver) return;

            Current.TickBuffs();
            Turns.Advance();

            ReEvaluateAuras();

            TurnCount++;
            OnTurnStart?.Invoke();

            if (!IsOver)
                TriggerPassives(Current, PassiveTrigger.OnTurnStart);

            ProcessNewDeaths();

            if (!IsOver)
                TurnChanged?.Invoke(Current);
        }

        /// <summary>
        /// Re-evaluates all WhileInArea buffs (Buff/Debuff only).
        /// Called at the start of each new turn so aura coverage reflects
        /// current positions.
        /// </summary>
        private void ReEvaluateAuras()
        {
            foreach (var p in _pieces)
                p.ClearAuraBuffs();

            foreach (var source in _pieces.Where(p => !p.IsDead))
            {
                foreach (var ability in source.Abilities)
                {
                    if (ability.AbilityType  != AbilityType.Passive)      continue;
                    if (ability.DurationType != DurationType.WhileInArea)  continue;
                    if (ability.EffectType   != EffectType.Buff &&
                        ability.EffectType   != EffectType.Debuff)         continue;

                    var targets = AbilityResolver.GetTargets(ability, source, Board, _pieces, source.Coords);
                    foreach (var t in targets)
                        t.ApplyBuff(new ActiveBuff(ability, source, -1));
                }
            }
        }

        /// <summary>
        /// Fires FixedTurns passive abilities on <paramref name="source"/> that match
        /// <paramref name="trigger"/>. Handles deaths that result from the effects.
        /// </summary>
        private void TriggerPassives(Piece source, PassiveTrigger trigger)
        {
            if (IsOver) return;

            foreach (var ability in source.Abilities)
            {
                if (ability.AbilityType  != AbilityType.Passive)     continue;
                if (ability.DurationType != DurationType.FixedTurns) continue;
                if (ability.Trigger      != trigger)                  continue;

                var targets = AbilityResolver.GetTargets(
                    ability, source, Board, _pieces, source.Coords);
                AbilityResolver.Apply(ability, source, targets);

                ProcessNewDeaths();
                if (IsOver) return;
            }
        }

        /// <summary>
        /// Finds all pieces that have died but not yet been removed and
        /// processes them in order. Guard against re-entrant death handling.
        /// </summary>
        private void ProcessNewDeaths()
        {
            var newDeaths = _pieces
                .Where(p => p.IsDead && !_removedPieces.Contains(p))
                .ToList();

            foreach (var dead in newDeaths)
            {
                HandleDeath(dead);
                if (IsOver) return;
            }
        }

        private void HandleDeath(Piece piece)
        {
            if (_removedPieces.Contains(piece)) return;
            _removedPieces.Add(piece);

            Board.RemovePiece(piece);
            Turns.Remove(piece);
            PieceDied?.Invoke(piece);

            TriggerPassives(piece, PassiveTrigger.OnDeath);
            CheckWinner();
        }

        private void CheckWinner()
        {
            if (_playerQueen != null && _playerQueen.IsDead) { SetWinner(Team.Enemy);  return; }
            if (_enemyQueen  != null && _enemyQueen.IsDead)  { SetWinner(Team.Player); return; }
            if (!AliveOf(Team.Player).Any()) { SetWinner(Team.Enemy);  return; }
            if (!AliveOf(Team.Enemy).Any())  { SetWinner(Team.Player); return; }
        }

        private void SetWinner(Team team)
        {
            Winner = team;
            CombatEnded?.Invoke(team);
        }
    }
}
