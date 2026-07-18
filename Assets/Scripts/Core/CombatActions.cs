using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The mutually-exclusive actions available during one action opportunity.
    /// A successful action ends the opportunity; terminal actions need not start
    /// another turn.
    /// </summary>
    public enum CombatActionKind
    {
        Move,
        Attack,
        Ability,
        Pass,
    }

    /// <summary>A stable, UI-safe explanation for why an action is not legal.</summary>
    public enum CombatActionRejection
    {
        None,
        InvalidRequest,
        CombatOver,
        StateResolutionInProgress,
        PendingDeaths,
        InvalidActor,
        ActorDead,
        WrongTurn,
        InvalidDestination,
        DestinationBlocked,
        DestinationOccupied,
        Unreachable,
        InvalidTarget,
        TargetDead,
        FriendlyTarget,
        OutOfRange,
        InvalidAbility,
        AbilityNotOwned,
        InsufficientMana,
        NoLegalTargets,
    }

    /// <summary>
    /// Pure Core request shared by previews and execution. Keeping the requested
    /// intent in one type prevents presentation code from reproducing combat rules.
    /// </summary>
    public sealed class CombatActionRequest
    {
        private CombatActionRequest(
            CombatActionKind kind,
            Piece actor,
            Axial destination,
            Piece target,
            IAbilityData ability)
        {
            Kind = kind;
            Actor = actor;
            Destination = destination;
            Target = target;
            Ability = ability;
        }

        public CombatActionKind Kind { get; }
        public Piece Actor { get; }
        public Axial Destination { get; }
        public Piece Target { get; }
        public IAbilityData Ability { get; }

        public static CombatActionRequest Move(Piece actor, Axial destination) =>
            new CombatActionRequest(CombatActionKind.Move, actor, destination, null, null);

        public static CombatActionRequest Attack(Piece actor, Piece target) =>
            new CombatActionRequest(CombatActionKind.Attack, actor, default, target, null);

        public static CombatActionRequest UseAbility(
            Piece actor, IAbilityData ability, Axial targetCoordinate) =>
            new CombatActionRequest(CombatActionKind.Ability, actor, targetCoordinate, null, ability);

        public static CombatActionRequest Pass(Piece actor) =>
            new CombatActionRequest(CombatActionKind.Pass, actor, default, null, null);
    }

    /// <summary>
    /// Result returned by both preview and execution. A legal preview has
    /// <see cref="IsAllowed"/> true and <see cref="WasExecuted"/> false.
    /// </summary>
    public sealed class CombatActionResult
    {
        private static readonly IReadOnlyList<Piece> NoTargets = Array.Empty<Piece>();

        private CombatActionResult(
            CombatActionRequest request,
            CombatActionRejection rejection,
            bool wasExecuted,
            IReadOnlyList<Piece> legalTargets)
        {
            Request = request;
            Rejection = rejection;
            WasExecuted = wasExecuted;
            LegalTargets = legalTargets ?? NoTargets;
        }

        public CombatActionRequest Request { get; }
        public CombatActionRejection Rejection { get; }
        public bool IsAllowed => Rejection == CombatActionRejection.None;
        public bool WasExecuted { get; }
        public IReadOnlyList<Piece> LegalTargets { get; }

        internal static CombatActionResult Allowed(
            CombatActionRequest request,
            IReadOnlyList<Piece> legalTargets = null,
            bool wasExecuted = false) =>
            new CombatActionResult(request, CombatActionRejection.None, wasExecuted, legalTargets);

        internal static CombatActionResult Rejected(
            CombatActionRequest request,
            CombatActionRejection rejection) =>
            new CombatActionResult(request, rejection, false, NoTargets);
    }
}
