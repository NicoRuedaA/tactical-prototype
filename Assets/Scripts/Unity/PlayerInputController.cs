using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

public class PlayerInputController : MonoBehaviour
{
    [Header("References")]
    public CombatRunner Runner;
    public CombatView CombatView;
    public Camera TargetCamera;
    public CombatHudView CombatHud;

    private CombatEngine _engine;
    private Piece _selected;
    private Piece _lastPlayerActor;
    private HashSet<Axial> _currentReachable = new();
    private HashSet<Piece> _currentAttackable = new();

    // Ability selection state
    private IAbilityData _selectedAbility;
    private HashSet<Axial> _abilityTargetCoords = new();
    private int _lastExecutedFrame = -1;
    private bool _consumePointerUntilRelease;
    private int _consumedPointerReleaseFrame = -1;

    private readonly CombatHudPresenter _hudPresenter = new CombatHudPresenter();

    private void Awake()
    {
        if (Runner == null || CombatView == null || TargetCamera == null || CombatHud == null)
        {
            Debug.LogError(
                "PlayerInputController requires explicit Runner, CombatView, TargetCamera, and CombatHud references.",
                this);
            enabled = false;
        }
    }

    public void OnEngineReady(CombatEngine engine)
    {
        if (engine == null)
            throw new System.ArgumentNullException(nameof(engine));
        if (CombatHud == null || !CombatHud.IsConfigured)
            throw new System.InvalidOperationException(
                "PlayerInputController cannot start because CombatHud is missing or not configured.");

        _engine = engine;
        _lastExecutedFrame = -1;
        _consumePointerUntilRelease = false;
        _consumedPointerReleaseFrame = -1;
        _engine.TurnChanged += OnTurnChanged;
        _engine.CombatEnded += OnCombatEnded;
        _engine.BossPhaseTransitioned += OnBossPhaseTransitioned;

        CombatHud.Bind(SelectAbilityFromHud, PassFromHud);
        RefreshHud();
    }

    private void OnDestroy()
    {
        if (_engine != null)
        {
            _engine.TurnChanged -= OnTurnChanged;
            _engine.CombatEnded -= OnCombatEnded;
            _engine.BossPhaseTransitioned -= OnBossPhaseTransitioned;
        }
    }

    private void OnTurnChanged(Piece current)
    {
        _selected = null;
        _selectedAbility = null;
        ClearHighlights();

        if (_engine == null || _engine.IsOver) return;

        if (current != null && current.Team == Team.Player && !Runner.AutoPlayBothSides)
        {
            _selected = current;
            _lastPlayerActor = current;
            ShowMoveAndAttackHighlights();
        }
        else if (_lastPlayerActor == null)
        {
            _lastPlayerActor = _engine.AliveOf(Team.Player).FirstOrDefault();
        }

        CombatHud?.ClearTransientFeedback();
        RefreshHud();
    }

    private void ShowMoveAndAttackHighlights()
    {
        if (_selected == null) return;

        _currentReachable = new HashSet<Axial>(
            _hudPresenter.GetLegalMoveCoords(_engine, _selected));
        _currentAttackable = new HashSet<Piece>(
            _hudPresenter.GetLegalAttackTargets(_engine, _selected));

        foreach (var coord in _currentReachable)
            CombatView?.SetHighlightForCoord(coord, TileHighlight.Reachable);

        foreach (var target in _currentAttackable)
        {
            CombatView?.SetHighlightForCoord(target.Coords, TileHighlight.Attackable);
            CombatView?.SetHighlightForPiece(target, PieceHighlight.Attackable);
        }

        if (CombatView != null)
        {
            CombatView.SetHighlightForCoord(_selected.Coords, TileHighlight.Selected);
            CombatView.SetHighlightForPiece(_selected, PieceHighlight.Selected);
        }
    }

    private void ShowAbilityRangeHighlights()
    {
        if (_selected == null || _selectedAbility == null) return;

        CombatView?.ClearHighlights();
        _abilityTargetCoords = new HashSet<Axial>(
            _hudPresenter.GetLegalAbilityTargetCoords(
                _engine, _selected, _selectedAbility));
        foreach (var coord in _abilityTargetCoords)
            CombatView?.SetHighlightForCoord(coord, TileHighlight.AbilityRange);
        foreach (Piece piece in _engine.Pieces.Where(piece =>
                     !piece.IsDead && _abilityTargetCoords.Contains(piece.Coords)))
            CombatView?.SetHighlightForPiece(piece, PieceHighlight.Ability);
        if (!_abilityTargetCoords.Contains(_selected.Coords))
            CombatView?.SetHighlightForPiece(_selected, PieceHighlight.Selected);
    }

    private void OnCombatEnded(Team winner)
    {
        CombatHud?.ShowCombatResult(winner == Team.Player ? "VICTORY" : "DEFEAT");
    }

    private void OnBossPhaseTransitioned(BossPhaseTransition transition)
    {
        if (transition == null || CombatHud == null)
            return;

        string ability = transition.GrantedAbility != null
            ? $" Ability: {transition.GrantedAbility.DisplayName}."
            : string.Empty;
        string bonus = transition.DamageBonus > 0
            ? $" Damage +{transition.DamageBonus}."
            : string.Empty;
        CombatHud.ShowBossPhaseFeedback(
            $"BOSS PHASE {transition.Phase}!{ability}{bonus}");
    }

    private void Update()
    {
        if (_engine == null) return;

        if (UpdateConsumedPointerGesture())
            return;

        if (CombatHud != null && CombatHud.HasPersistentFeedback)
        {
            if (WasPointerPressedThisFrame())
                CombatHud.ConsumePersistentFeedback();
            return;
        }

        if (CombatView != null && CombatView.HasActiveFeedback)
        {
            if (WasPointerPressedThisFrame())
                ConsumeActiveFeedback(true);
            return;
        }

        if (_engine.IsOver) return;
        if (_engine.Current == null) return;
        if (Runner.AutoPlayBothSides) return;

        if (Mouse.current == null || Keyboard.current == null) return;

        // Ability selection keys
        HandleAbilityKeys();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryHandleWorldClick();

        // Right-click or Escape to cancel ability
        if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            CancelAbility();

        // Space is a dedicated Pass shortcut in this project. Enter is also the
        // uGUI Submit key, so it yields when a selectable control has focus.
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            TryPassFromKeyboard(false);
        else if (Keyboard.current.enterKey.wasPressedThisFrame)
            TryPassFromKeyboard(true);
    }

    private void HandleAbilityKeys()
    {
        Piece actor = GetInteractionActor();
        if (actor == null) return;

        var abilities = actor.Abilities
            .Where(a => a.AbilityType == AbilityType.Active)
            .ToList();

        int pressedIndex = GetPressedAbilityIndex();
        if (pressedIndex >= 0 && pressedIndex < abilities.Count)
            SelectAbilityAtIndex(pressedIndex);
    }

    public bool TryHandleWorldClick()
    {
        if (IsPointerInputConsumed())
            return false;
        if (CombatView != null && CombatView.HasActiveFeedback)
        {
            ConsumeActiveFeedback(IsAnyPointerButtonPressed());
            return false;
        }
        if (IsPointerOverUi())
            return false;

        RestoreSelectionHighlights();

        Ray ray = TargetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            CombatHud?.ShowFeedback(
                CombatHudPresenter.EmptyClickMessage,
                CombatFeedbackTone.Invalid);
            return false;
        }

        PieceView pieceView = hit.collider.GetComponentInParent<PieceView>();
        Piece clickedPiece = pieceView != null ? pieceView.Piece : null;
        TileView tileView = hit.collider.GetComponentInParent<TileView>();

        Axial? clickedCoord = clickedPiece != null
            ? clickedPiece.Coords
            : tileView != null
                ? tileView.Coord
                : null;

        // A piece does not cover the whole hex visually. If the ray reaches an
        // occupied TileView around the piece collider, preserve the attack intent
        // by resolving the occupant from the authoritative Core coordinates.
        if (clickedPiece == null && clickedCoord.HasValue)
        {
            clickedPiece = _engine.Pieces.FirstOrDefault(piece =>
                !piece.IsDead && piece.Coords.Equals(clickedCoord.Value));
        }

        if (!clickedCoord.HasValue)
        {
            CombatHud?.ShowFeedback(
                CombatHudPresenter.EmptyClickMessage,
                CombatFeedbackTone.Invalid);
            return false;
        }

        Piece actor = GetInteractionActor();
        if (actor == null)
        {
            PresentRejection(CombatActionRejection.InvalidActor, clickedCoord, clickedPiece);
            return false;
        }

        if (_selectedAbility != null)
        {
            bool wasExecuted = SubmitAction(
                CombatActionRequest.UseAbility(actor, _selectedAbility, clickedCoord.Value),
                clickedCoord,
                clickedPiece);
            if (wasExecuted)
                _selectedAbility = null;
            return wasExecuted;
        }

        CombatActionRequest request = clickedPiece != null
            ? CombatActionRequest.Attack(actor, clickedPiece)
            : CombatActionRequest.Move(actor, clickedCoord.Value);
        return SubmitAction(request, clickedCoord, clickedPiece);
    }

    private void CancelAbility()
    {
        if (IsFeedbackInputBlocked())
            return;
        if (_selectedAbility == null)
        {
            CombatHud?.ShowFeedback(
                CombatHudPresenter.NothingToCancelMessage,
                CombatFeedbackTone.Invalid);
            return;
        }

        _selectedAbility = null;
        _abilityTargetCoords.Clear();
        CombatView?.ClearHighlights();
        ShowMoveAndAttackHighlights();
        if (_selected != null)
        {
            CombatView?.SetHighlightForCoord(_selected.Coords, TileHighlight.Cancelled);
            CombatView?.SetHighlightForPiece(_selected, PieceHighlight.Cancelled);
        }
        CombatHud?.ShowFeedback(
            CombatHudPresenter.CancelledMessage,
            CombatFeedbackTone.Cancelled);
    }

    private void PassTurn()
    {
        PassFromHud(CombatHudInputOrigin.Submit);
    }

    private void PassFromHud(CombatHudInputOrigin origin)
    {
        if (GateUiCallbackDuringFeedback(origin))
            return;
        if (_engine == null) return;
        SubmitAction(CombatActionRequest.Pass(GetInteractionActor()));
    }

    /// <summary>
    /// Handles a keyboard pass shortcut. Submit-bound keys yield to the focused
    /// uGUI control; dedicated shortcuts pass regardless of UI selection.
    /// </summary>
    private bool TryPassFromKeyboard(bool isUiSubmitKey)
    {
        if (isUiSubmitKey && HasActiveUiSubmitTarget())
            return false;
        if (_engine == null)
            return false;
        return SubmitAction(CombatActionRequest.Pass(GetInteractionActor()));
    }

    public bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;
        if (eventSystem.IsPointerOverGameObject())
            return true;
        if (Mouse.current == null)
            return false;

        // InputSystemUIInputModule may update its cached pointer after this
        // component's Update. Raycast the same current position as a deterministic
        // fallback so world input never leaks through UI for one frame.
        var pointer = new PointerEventData(eventSystem)
        {
            position = Mouse.current.position.ReadValue(),
        };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);
        return results.Count > 0;
    }

    private static bool HasActiveUiSubmitTarget()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null
            ? eventSystem.currentSelectedGameObject
            : null;
        if (selected == null)
            return false;

        Selectable selectable = selected.GetComponentInParent<Selectable>();
        return selectable != null
               && selectable.IsActive()
               && selectable.IsInteractable()
               && ExecuteEvents.GetEventHandler<ISubmitHandler>(selected) != null;
    }

    private void SelectAbilityAtIndex(int index)
    {
        SelectAbilityFromHud(index, CombatHudInputOrigin.Submit);
    }

    private void SelectAbilityFromHud(int index, CombatHudInputOrigin origin)
    {
        if (GateUiCallbackDuringFeedback(origin))
            return;
        if (_engine == null)
            return;

        Piece actor = GetInteractionActor();
        if (actor == null)
        {
            PresentRejection(CombatActionRejection.InvalidActor);
            return;
        }

        var abilities = actor.Abilities
            .Where(ability => ability.AbilityType == AbilityType.Active)
            .ToList();
        if (index < 0 || index >= abilities.Count)
        {
            CombatHud?.ShowFeedback(
                CombatHudPresenter.ActionUnavailableMessage,
                CombatFeedbackTone.Invalid);
            return;
        }

        IAbilityData ability = abilities[index];
        CombatActionRejection rejection =
            _hudPresenter.GetAbilityRejection(_engine, actor, ability);
        if (rejection != CombatActionRejection.None)
        {
            PresentRejection(rejection);
            return;
        }

        _selectedAbility = ability;
        CombatHud?.ClearFeedback();
        ShowAbilityRangeHighlights();
    }

    private static int GetPressedAbilityIndex()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) return 0;
        if (Keyboard.current.digit2Key.wasPressedThisFrame) return 1;
        if (Keyboard.current.digit3Key.wasPressedThisFrame) return 2;
        if (Keyboard.current.digit4Key.wasPressedThisFrame) return 3;
        if (Keyboard.current.digit5Key.wasPressedThisFrame) return 4;
        if (Keyboard.current.digit6Key.wasPressedThisFrame) return 5;
        if (Keyboard.current.digit7Key.wasPressedThisFrame) return 6;
        if (Keyboard.current.digit8Key.wasPressedThisFrame) return 7;
        if (Keyboard.current.digit9Key.wasPressedThisFrame) return 8;
        return -1;
    }

    private void RefreshHud()
    {
        if (_engine == null || CombatHud == null)
            return;
        CombatHud.Render(_hudPresenter.Build(_engine, Runner.AutoPlayBothSides));
    }

    private void ClearHighlights()
    {
        _currentReachable.Clear();
        _currentAttackable.Clear();
        _abilityTargetCoords.Clear();
        _selectedAbility = null;
        CombatView?.ClearHighlights();
    }

    private Piece GetInteractionActor()
    {
        if (_selected != null)
            return _selected;
        if (_lastPlayerActor != null && !_lastPlayerActor.IsDead)
            return _lastPlayerActor;
        return _engine?.AliveOf(Team.Player).FirstOrDefault();
    }

    private bool SubmitAction(
        CombatActionRequest request,
        Axial? feedbackCoord = null,
        Piece feedbackPiece = null)
    {
        if (_engine == null || request == null)
            return false;
        if (IsFeedbackInputBlocked())
            return false;

        // Update, uGUI callbacks, and Input System submit can all run during the
        // same Unity frame. Once one request succeeds, every remaining route for
        // that frame becomes a no-op so adjacent player turns cannot be consumed.
        if (_lastExecutedFrame == Time.frameCount)
            return false;

        CombatActionResult result = _engine.ExecuteAction(request);
        if (result.WasExecuted)
        {
            _lastExecutedFrame = Time.frameCount;
            CombatHud?.ClearFeedback();
            return true;
        }

        PresentRejection(result.Rejection, feedbackCoord, feedbackPiece);
        RefreshHud();
        return false;
    }

    private bool GateUiCallbackDuringFeedback(CombatHudInputOrigin origin)
    {
        if (IsPointerInputConsumed())
            return true;
        if (CombatView == null || !CombatView.HasActiveFeedback)
            return false;

        if (origin == CombatHudInputOrigin.Pointer)
            ConsumeActiveFeedback(false);
        return true;
    }

    private bool IsFeedbackInputBlocked()
    {
        return IsPointerInputConsumed()
               || (CombatView != null && CombatView.HasActiveFeedback);
    }

    private bool UpdateConsumedPointerGesture()
    {
        if (!_consumePointerUntilRelease)
            return _consumedPointerReleaseFrame == Time.frameCount;

        if (!IsAnyPointerButtonPressed())
        {
            _consumePointerUntilRelease = false;
            _consumedPointerReleaseFrame = Time.frameCount;
        }
        return true;
    }

    private bool IsPointerInputConsumed()
    {
        return _consumePointerUntilRelease
               || _consumedPointerReleaseFrame == Time.frameCount;
    }

    private void ConsumeActiveFeedback(bool consumeThroughRelease)
    {
        CombatView?.CompleteActiveFeedbackImmediately();
        if (consumeThroughRelease)
            _consumePointerUntilRelease = true;
        else
            _consumedPointerReleaseFrame = Time.frameCount;
    }

    private static bool WasPointerPressedThisFrame()
    {
        return Mouse.current != null
               && (Mouse.current.leftButton.wasPressedThisFrame
                   || Mouse.current.rightButton.wasPressedThisFrame);
    }

    private static bool IsAnyPointerButtonPressed()
    {
        return Mouse.current != null
               && (Mouse.current.leftButton.isPressed
                   || Mouse.current.rightButton.isPressed);
    }

    private void PresentRejection(
        CombatActionRejection rejection,
        Axial? feedbackCoord = null,
        Piece feedbackPiece = null)
    {
        string message = _hudPresenter.GetRejectionMessage(rejection);
        if (string.IsNullOrEmpty(message))
            message = CombatHudPresenter.ActionUnavailableMessage;
        CombatHud?.ShowFeedback(message, CombatFeedbackTone.Invalid);

        if (feedbackPiece != null)
            CombatView?.SetHighlightForPiece(feedbackPiece, PieceHighlight.Invalid);
        else if (feedbackCoord.HasValue)
            CombatView?.SetHighlightForCoord(feedbackCoord.Value, TileHighlight.Invalid);
    }

    private void RestoreSelectionHighlights()
    {
        if (_engine == null || _engine.IsOver || _selected == null)
            return;
        CombatView?.ClearHighlights();
        if (_selectedAbility != null)
            ShowAbilityRangeHighlights();
        else
            ShowMoveAndAttackHighlights();
    }
}
