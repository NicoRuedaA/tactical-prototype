using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// Combat rules engine.
    /// Turn economy: one action opportunity — Move | Attack | UseAbility | Pass.
    /// A successful action ends that opportunity. TurnCount advances only when
    /// combat continues into another piece's turn; a terminal action ends combat
    /// without advancing it.
    /// Win condition: a team loses when its Queen dies (chess rule).
    /// Passive abilities fire on their trigger; WhileInArea buffs are re-evaluated
    /// at the start of every turn.
    /// </summary>
    public sealed class CombatEngine
    {
        private readonly List<Piece>      _pieces;
        private readonly HashSet<Piece>   _removedPieces = new HashSet<Piece>();
        private bool                      _isResolvingDeaths;
        private readonly Piece            _playerQueen;
        private readonly Piece            _enemyQueen;
        private bool                      _turnStartPrepared;

        public Board      Board  { get; }
        public TurnSystem Turns  { get; }

        public IReadOnlyList<Piece> Pieces  => _pieces;
        public Piece                Current => Turns.Current;
        public Team                 CurrentTeam => Turns.CurrentTeam;
        public Team?                Winner  { get; private set; }
        public bool                 IsOver  => Winner.HasValue;
        public bool                 HasPendingDeaths =>
            _pieces.Any(piece => piece.IsDead && !_removedPieces.Contains(piece));

        // Events
        public event Action<Piece, Axial, Axial>             PieceMoved;
        public event Action<Piece, Piece, int>               PieceAttacked;
        public event Action<AttackResolution>                AttackResolved;
        public event Action<Piece>                           PieceDied;
        public event Action<Piece>                           TurnChanged;
        public event Action<Team>                            CombatEnded;
        public event Action<Piece, IAbilityData, IReadOnlyList<Piece>> AbilityUsed;
        public event Action<AbilityResolution>                 AbilityResolved;
        public event Action<BossPhaseTransition>               BossPhaseTransitioned;
        public event Action                                  OnTurnStart;

        public int TurnCount { get; private set; }
        /// <summary>Default mana restored by a legal pass.</summary>
        public const int DefaultPassManaRecovery = 1;
        /// <summary>Mana restored by a legal pass for this combat.</summary>
        public int PassManaRecovery { get; }

        // Keep the original two-parameter constructor for binary compatibility.
        public CombatEngine(Board board, IEnumerable<Piece> pieces)
            : this(board, pieces, DefaultPassManaRecovery)
        {
        }

        public CombatEngine(
            Board board,
            IEnumerable<Piece> pieces,
            int passManaRecovery)
        {
            Board   = board;
            _pieces = pieces.ToList();
            PassManaRecovery = passManaRecovery < 0 ? 0 : passManaRecovery;
            foreach (var p in _pieces)
                Board.Place(p, p.Coords);

            _playerQueen = _pieces.FirstOrDefault(p => p.Team == Team.Player && p.IsQueen);
            _enemyQueen  = _pieces.FirstOrDefault(p => p.Team == Team.Enemy  && p.IsQueen);

            Turns = new TurnSystem(_pieces);
        }

        public void Begin()
        {
            _turnStartPrepared = false;
            TurnChanged?.Invoke(Current);
        }

        // ── Queries ──────────────────────────────────────────────────────────

        public IEnumerable<Piece> AliveOf(Team team) =>
            _pieces.Where(p => p.Team == team && !p.IsDead);

        /// <summary>
        /// Selects the actor for the active team's action. Turn-start passives
        /// are deferred until this explicit choice exists.
        /// </summary>
        public bool SelectPiece(Piece piece)
        {
            if (!Turns.Select(piece))
                return false;

            if (!_turnStartPrepared)
            {
                _turnStartPrepared = true;
                ReEvaluateAuras();
                OnTurnStart?.Invoke();
                TriggerPassives(piece, PassiveTrigger.OnTurnStart);
            }
            return true;
        }

        public BfsResult GetMoveRange(Piece piece) =>
            Pathfinding.GetReachable(Board, piece.Coords, piece.EffectiveMoveRange);

        public IEnumerable<Piece> GetAttackTargets(Piece piece) =>
            _pieces.Where(t => !t.IsDead
                               && t.Team != piece.Team
                               && Axial.Distance(piece.Coords, t.Coords) <= piece.EffectiveAttackRange);

        public IEnumerable<Piece> GetAbilityTargets(Piece caster, IAbilityData ability, Axial center) =>
            AbilityResolver.GetTargets(ability, caster, Board, _pieces, center);

        /// <summary>
        /// Evaluates the same rules used by execution without mutating combat state.
        /// Presentation code should use this instead of reproducing pathfinding,
        /// range, ownership, or target rules.
        /// </summary>
        public CombatActionResult EvaluateAction(CombatActionRequest request)
        {
            if (request == null)
                return CombatActionResult.Rejected(null, CombatActionRejection.InvalidRequest);
            if (IsOver)
                return CombatActionResult.Rejected(request, CombatActionRejection.CombatOver);
            if (_isResolvingDeaths)
                return CombatActionResult.Rejected(
                    request, CombatActionRejection.StateResolutionInProgress);
            if (HasPendingDeaths)
                return CombatActionResult.Rejected(
                    request, CombatActionRejection.PendingDeaths);

            Piece actor = request.Actor;
            if (actor == null || !_pieces.Contains(actor))
                return CombatActionResult.Rejected(request, CombatActionRejection.InvalidActor);
            if (actor.IsDead)
                return CombatActionResult.Rejected(request, CombatActionRejection.ActorDead);
            if (actor != Current)
                return CombatActionResult.Rejected(request, CombatActionRejection.WrongTurn);

            switch (request.Kind)
            {
                case CombatActionKind.Move:
                    if (!Board.Contains(request.Destination))
                        return CombatActionResult.Rejected(request, CombatActionRejection.InvalidDestination);
                    if (!Board.IsWalkable(request.Destination))
                        return CombatActionResult.Rejected(request, CombatActionRejection.DestinationBlocked);
                    if (Board.IsOccupied(request.Destination))
                        return CombatActionResult.Rejected(request, CombatActionRejection.DestinationOccupied);
                    if (!GetMoveRange(actor).CanReach(request.Destination))
                        return CombatActionResult.Rejected(request, CombatActionRejection.Unreachable);
                    return CombatActionResult.Allowed(request);

                case CombatActionKind.Attack:
                    Piece target = request.Target;
                    if (target == null || !_pieces.Contains(target))
                        return CombatActionResult.Rejected(request, CombatActionRejection.InvalidTarget);
                    if (target.IsDead)
                        return CombatActionResult.Rejected(request, CombatActionRejection.TargetDead);
                    if (target.Team == actor.Team)
                        return CombatActionResult.Rejected(request, CombatActionRejection.FriendlyTarget);
                    if (Axial.Distance(actor.Coords, target.Coords) > actor.EffectiveAttackRange)
                        return CombatActionResult.Rejected(request, CombatActionRejection.OutOfRange);
                    return CombatActionResult.Allowed(request, new[] { target });

                case CombatActionKind.Ability:
                    IAbilityData ability = request.Ability;
                    if (ability == null || ability.AbilityType != AbilityType.Active)
                        return CombatActionResult.Rejected(request, CombatActionRejection.InvalidAbility);
                    if (!actor.Abilities.Contains(ability))
                        return CombatActionResult.Rejected(request, CombatActionRejection.AbilityNotOwned);
                    if (actor.Mana < ability.ManaCost)
                        return CombatActionResult.Rejected(request, CombatActionRejection.InsufficientMana);

                    bool selfTarget = ability.AffectsTeam == AffectsTeam.Self;
                    if (!selfTarget && !Board.Contains(request.Destination))
                        return CombatActionResult.Rejected(request, CombatActionRejection.InvalidDestination);
                    if (!selfTarget && Axial.Distance(actor.Coords, request.Destination) > ability.ActiveRange)
                        return CombatActionResult.Rejected(request, CombatActionRejection.OutOfRange);

                    Axial center = selfTarget ? actor.Coords : request.Destination;
                    var targets = AbilityResolver.GetTargets(ability, actor, Board, _pieces, center);
                    if (targets.Count == 0)
                        return CombatActionResult.Rejected(request, CombatActionRejection.NoLegalTargets);
                    return CombatActionResult.Allowed(request, targets);

                case CombatActionKind.Pass:
                    return CombatActionResult.Allowed(request);

                default:
                    return CombatActionResult.Rejected(request, CombatActionRejection.InvalidRequest);
            }
        }

        /// <summary>
        /// Executes an allowed request and ends the actor's action opportunity.
        /// If the action ends combat, no subsequent turn starts and TurnCount does
        /// not advance.
        /// </summary>
        public CombatActionResult ExecuteAction(CombatActionRequest request)
        {
            CombatActionResult evaluation = EvaluateAction(request);
            if (!evaluation.IsAllowed)
                return evaluation;

            Piece actor = request.Actor;
            int manaBefore = actor.Mana;
            switch (request.Kind)
            {
                case CombatActionKind.Move:
                    var from = actor.Coords;
                    Board.MovePiece(actor, request.Destination);
                    PieceMoved?.Invoke(actor, from, request.Destination);
                    EndTurn();
                    break;

                case CombatActionKind.Attack:
                    ExecuteAttack(actor, request.Target);
                    break;

                case CombatActionKind.Ability:
                    actor.SpendMana(request.Ability.ManaCost);
                    ResolveAbility(actor, request.Ability, evaluation.LegalTargets, false, null);
                    AbilityUsed?.Invoke(actor, request.Ability, evaluation.LegalTargets);
                    ProcessNewDeaths();
                    if (!IsOver) EndTurn();
                    break;

                case CombatActionKind.Pass:
                    actor.RestoreMana(PassManaRecovery);
                    EndTurn();
                    break;
            }

            return CombatActionResult.Allowed(
                request,
                evaluation.LegalTargets,
                true,
                manaBefore,
                actor.Mana);
        }

        // ── Actions ──────────────────────────────────────────────────────────

        public bool Move(Piece piece, Axial dest)
        {
            return ExecuteAction(CombatActionRequest.Move(piece, dest)).WasExecuted;
        }

        public bool Attack(Piece attacker, Piece target)
        {
            return ExecuteAction(CombatActionRequest.Attack(attacker, target)).WasExecuted;
        }

        public bool UseAbility(Piece caster, IAbilityData ability, Axial targetCoord)
        {
            return ExecuteAction(
                CombatActionRequest.UseAbility(caster, ability, targetCoord)).WasExecuted;
        }

        public void Pass()
        {
            if (!IsOver)
                ExecuteAction(CombatActionRequest.Pass(Current));
        }

        /// <summary>
        /// Reconciles HP changes made outside an engine action. Every pending dead
        /// piece is removed and announced exactly once before the winner is decided.
        /// Returns the number of pieces resolved by this call.
        /// </summary>
        public int ResolvePendingDeaths()
        {
            return ProcessNewDeaths();
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void ExecuteAttack(Piece attacker, Piece target)
        {
            int requestedDamage = attacker.EffectiveDamage;
            int hpBefore = target.Hp;
            target.TakeDamage(requestedDamage);
            AttackResolved?.Invoke(new AttackResolution(
                attacker, target, requestedDamage, hpBefore, target.Hp));
            PieceAttacked?.Invoke(attacker, target, requestedDamage);

            if (!target.IsDead)
                TriggerPassives(target, PassiveTrigger.OnTakeDamage);

            if (!IsOver && !attacker.IsDead)
                TriggerPassives(attacker, PassiveTrigger.OnHit);

            ProcessNewDeaths();

            if (!IsOver) EndTurn();
        }

        private void ResolveAbility(
            Piece source,
            IAbilityData ability,
            IReadOnlyList<Piece> targets,
            bool isPassive,
            PassiveTrigger? trigger)
        {
            var before = targets.Select(target => new
            {
                Target = target,
                Hp = target.Hp,
                Mana = target.Mana,
                Buffs = target.ActiveBuffs.Count,
            }).ToList();

            AbilityResolver.Apply(ability, source, targets);

            var changes = before.Select(state => new AbilityEffectChange(
                state.Target,
                state.Hp,
                state.Target.Hp,
                state.Mana,
                state.Target.Mana,
                state.Buffs,
                state.Target.ActiveBuffs.Count)).ToList();

            AbilityResolved?.Invoke(new AbilityResolution(
                source, ability, isPassive, trigger, targets, changes));
        }

        internal void ReportBossPhaseTransition(
            Piece boss,
            int phase,
            IAbilityData grantedAbility,
            int damageBonus)
        {
            var transition = new BossPhaseTransition(
                boss, phase, grantedAbility, damageBonus);
            BossPhaseTransitioned?.Invoke(transition);
        }

        private void EndTurn()
        {
            if (IsOver) return;

            Piece actor = Current;
            actor?.TickBuffs();
            Turns.Advance();

            TurnCount++;
            _turnStartPrepared = false;

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
                    ResolveAbility(source, ability, targets, true, null);
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
                ResolveAbility(source, ability, targets, true, trigger);

                ProcessNewDeaths();
                if (IsOver) return;
            }
        }

        /// <summary>
        /// Finds all pieces that have died but not yet been removed and
        /// processes them in order. Guard against re-entrant death handling.
        /// </summary>
        private int ProcessNewDeaths()
        {
            if (_isResolvingDeaths || IsOver)
                return 0;

            int resolvedCount = 0;
            _isResolvingDeaths = true;
            try
            {
                while (true)
                {
                    var newDeaths = _pieces
                        .Where(piece => piece.IsDead && !_removedPieces.Contains(piece))
                        .ToList();

                    if (newDeaths.Count == 0)
                        break;

                    foreach (var dead in newDeaths)
                    {
                        if (_removedPieces.Contains(dead))
                            continue;

                        HandleDeath(dead);
                        resolvedCount++;
                    }
                }

                if (resolvedCount > 0)
                    CheckWinner();
            }
            finally
            {
                _isResolvingDeaths = false;
            }

            return resolvedCount;
        }

        private void HandleDeath(Piece piece)
        {
            if (_removedPieces.Contains(piece)) return;
            _removedPieces.Add(piece);

            Board.RemovePiece(piece);
            Turns.Remove(piece);
            PieceDied?.Invoke(piece);

            TriggerPassives(piece, PassiveTrigger.OnDeath);
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
            if (IsOver) return;
            Winner = team;
            CombatEnded?.Invoke(team);
        }
    }
}
