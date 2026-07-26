using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

/// <summary>
/// Pure presentation model for the combat HUD. It translates combat state into
/// display-ready values without depending on Unity UI objects.
/// </summary>
public sealed class CombatHudPresenter
{
    public const string ActionRule = "ONE ACTION: Move, Attack, Ability, or Pass.";
    public const string Controls = "Mouse: left-click move/attack/target. 1-9: ability. Right-click/Esc: cancel. Space: pass. Enter: activate focused UI, or pass with no focus.";
    public const string EmptyClickMessage = "Nothing to target.";
    public const string CancelledMessage = "Ability cancelled.";
    public const string NothingToCancelMessage = "Nothing to cancel.";
    public const string ActionUnavailableMessage = "Action unavailable.";

    public CombatHudState Build(
        CombatEngine engine,
        bool autoPlayBothSides,
        Piece selected = null)
    {
        if (engine == null)
            throw new ArgumentNullException(nameof(engine));

        Piece current = selected;
        bool isPlayerTurn = !engine.IsOver
                            && engine.CurrentTeam == Team.Player
                            && !autoPlayBothSides;

        var abilities = current == null
            ? new List<CombatHudAbilityState>()
            : current.Abilities
                .Where(ability => ability.AbilityType == AbilityType.Active)
                .Select((ability, index) =>
                {
                    CombatActionRejection rejection =
                        GetAbilityRejection(engine, current, ability);
                    return new CombatHudAbilityState(
                        index,
                        ability.DisplayName,
                        ability.ManaCost,
                        isPlayerTurn && rejection == CombatActionRejection.None,
                        rejection,
                        isPlayerTurn,
                        GetRejectionMessage(rejection));
                })
                .ToList();

        string activeUnit = current == null
            ? "Active: —"
            : $"Active: {current.Name} ({current.Team})";
        string resources = current == null
            ? "HP —  |  Mana —"
            : $"HP {current.Hp}/{current.EffectiveMaxHp}  |  Mana {current.Mana}/{current.MaxMana}";

        int playerCount = engine.AliveOf(Team.Player).Count();
        int enemyCount = engine.AliveOf(Team.Enemy).Count();
        string turnOrder = $"Phase: {engine.CurrentTeam.ToString().ToUpperInvariant()}  |  Player {playerCount}  Enemy {enemyCount}";

        return new CombatHudState(
            activeUnit,
            resources,
            turnOrder,
            ActionRule,
            Controls,
            isPlayerTurn && selected != null,
            selected != null,
            abilities);
    }

    public bool CanUseAbility(CombatEngine engine, Piece actor, IAbilityData ability)
    {
        if (engine == null || actor == null || ability == null)
            return false;
        return GetAbilityRejection(engine, actor, ability) == CombatActionRejection.None;
    }

    public IReadOnlyList<Axial> GetLegalMoveCoords(CombatEngine engine, Piece actor)
    {
        if (engine == null || actor == null)
            return Array.Empty<Axial>();

        return engine.Board.Tiles
            .Select(tile => tile.Coords)
            .Where(coord => engine.EvaluateAction(
                CombatActionRequest.Move(actor, coord)).IsAllowed)
            .ToList();
    }

    public IReadOnlyList<Piece> GetLegalAttackTargets(CombatEngine engine, Piece actor)
    {
        if (engine == null || actor == null)
            return Array.Empty<Piece>();

        return engine.Pieces
            .Where(target => engine.EvaluateAction(
                CombatActionRequest.Attack(actor, target)).IsAllowed)
            .ToList();
    }

    /// <summary>
    /// Returns target coordinates accepted by the core preview contract. This is
    /// the single source used by HUD availability and world highlights.
    /// </summary>
    public IReadOnlyList<Axial> GetLegalAbilityTargetCoords(
        CombatEngine engine,
        Piece actor,
        IAbilityData ability)
    {
        if (engine == null || actor == null || ability == null)
            return Array.Empty<Axial>();

        var legalTargets = new List<Axial>();
        if (ability.AffectsTeam == AffectsTeam.Self)
        {
            if (engine.EvaluateAction(
                    CombatActionRequest.UseAbility(actor, ability, actor.Coords)).IsAllowed)
                legalTargets.Add(actor.Coords);
            return legalTargets;
        }

        foreach (Tile tile in engine.Board.Tiles)
        {
            if (engine.EvaluateAction(
                    CombatActionRequest.UseAbility(actor, ability, tile.Coords)).IsAllowed)
                legalTargets.Add(tile.Coords);
        }

        return legalTargets;
    }

    public CombatActionRejection GetAbilityRejection(
        CombatEngine engine,
        Piece actor,
        IAbilityData ability)
    {
        if (engine == null || actor == null)
            return CombatActionRejection.InvalidActor;
        if (ability == null)
            return CombatActionRejection.InvalidAbility;
        if (GetLegalAbilityTargetCoords(engine, actor, ability).Count > 0)
            return CombatActionRejection.None;

        return engine.EvaluateAction(
            CombatActionRequest.UseAbility(actor, ability, actor.Coords)).Rejection;
    }

    public string GetRejectionMessage(CombatActionRejection rejection)
    {
        return rejection switch
        {
            CombatActionRejection.None => string.Empty,
            CombatActionRejection.InvalidRequest => ActionUnavailableMessage,
            CombatActionRejection.CombatOver => "Combat is already over.",
            CombatActionRejection.StateResolutionInProgress => "Resolving combat...",
            CombatActionRejection.PendingDeaths => "Resolving combat...",
            CombatActionRejection.InvalidActor => "No active unit.",
            CombatActionRejection.ActorDead => "That unit is defeated.",
            CombatActionRejection.WrongTurn => "Not your turn.",
            CombatActionRejection.InvalidDestination => "Invalid destination.",
            CombatActionRejection.DestinationBlocked => "Tile is blocked.",
            CombatActionRejection.DestinationOccupied => "Tile is occupied.",
            CombatActionRejection.Unreachable => "Tile is unreachable.",
            CombatActionRejection.InvalidTarget => "Choose a valid target.",
            CombatActionRejection.TargetDead => "Target is already defeated.",
            CombatActionRejection.FriendlyTarget => "Cannot attack an ally.",
            CombatActionRejection.OutOfRange => "Target is out of range.",
            CombatActionRejection.InvalidAbility => "Ability unavailable.",
            CombatActionRejection.AbilityNotOwned => "Ability unavailable.",
            CombatActionRejection.InsufficientMana => "Not enough mana.",
            CombatActionRejection.NoLegalTargets => "No legal targets.",
            _ => ActionUnavailableMessage,
        };
    }
}

public sealed class CombatHudState
{
        public CombatHudState(
            string activeUnit,
            string resources,
            string turnOrder,
            string actionRule,
            string controls,
            bool canPass,
            bool hasSelection,
            IReadOnlyList<CombatHudAbilityState> abilities)
    {
        ActiveUnit = activeUnit;
        Resources = resources;
        TurnOrder = turnOrder;
        ActionRule = actionRule;
        Controls = controls;
            CanPass = canPass;
            HasSelection = hasSelection;
            Abilities = abilities ?? Array.Empty<CombatHudAbilityState>();
    }

    public string ActiveUnit { get; }
    public string Resources { get; }
    public string TurnOrder { get; }
    public string ActionRule { get; }
    public string Controls { get; }
        public bool CanPass { get; }
        public bool HasSelection { get; }
    public IReadOnlyList<CombatHudAbilityState> Abilities { get; }
}

public sealed class CombatHudAbilityState
{
    public CombatHudAbilityState(
        int index,
        string name,
        int manaCost,
        bool isEnabled,
        CombatActionRejection unavailableReason = CombatActionRejection.None,
        bool canAttempt = false,
        string unavailableMessage = "")
    {
        Index = index;
        Name = name;
        ManaCost = manaCost;
        IsEnabled = isEnabled;
        UnavailableReason = unavailableReason;
        CanAttempt = canAttempt;
        UnavailableMessage = unavailableMessage ?? string.Empty;
    }

    public int Index { get; }
    public bool HasHotkey => Index >= 0 && Index < 9;
    public int Hotkey => HasHotkey ? Index + 1 : 0;
    public string Name { get; }
    public int ManaCost { get; }
    public bool IsEnabled { get; }
    public CombatActionRejection UnavailableReason { get; }
    public bool CanAttempt { get; }
    public string UnavailableMessage { get; }
    public string Label => HasHotkey
        ? $"[{Hotkey}] {Name} — {ManaCost} Mana"
        : $"Click: {Name} — {ManaCost} Mana";
}
