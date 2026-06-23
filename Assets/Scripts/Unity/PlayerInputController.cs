using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

public class PlayerInputController : MonoBehaviour
{
    [Header("References")]
    public CombatRunner Runner;
    public CombatView CombatView;
    public Camera TargetCamera;

    private CombatEngine _engine;
    private Piece _selected;
    private HashSet<Axial> _currentReachable = new();
    private HashSet<Piece> _currentAttackable = new();

    // Ability selection state
    private IAbilityData _selectedAbility;
    private HashSet<Axial> _abilityTargetCoords = new();

    private void Awake()
    {
        if (Runner == null) Runner = GetComponent<CombatRunner>();
        if (CombatView == null) CombatView = GetComponent<CombatView>();
        if (TargetCamera == null) TargetCamera = Camera.main;
    }

    public void OnEngineReady(CombatEngine engine)
    {
        _engine = engine;
        _engine.TurnChanged += OnTurnChanged;
        _engine.CombatEnded += OnCombatEnded;
    }

    private void OnDestroy()
    {
        if (_engine != null)
        {
            _engine.TurnChanged -= OnTurnChanged;
            _engine.CombatEnded -= OnCombatEnded;
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
            ShowMoveAndAttackHighlights();
        }
    }

    private void ShowMoveAndAttackHighlights()
    {
        if (_selected == null) return;

        _currentReachable = new HashSet<Axial>(_engine.GetMoveRange(_selected).ReachableTiles);
        _currentAttackable = new HashSet<Piece>(_engine.GetAttackTargets(_selected));

        foreach (var coord in _currentReachable)
            CombatView?.SetHighlightForCoord(coord, TileHighlight.Reachable);

        foreach (var target in _currentAttackable)
        {
            Axial tCoord = target.Coords;
            if (_currentReachable.Contains(tCoord))
                CombatView?.SetHighlightForCoord(tCoord, TileHighlight.Attackable);
        }

        if (CombatView != null)
        {
            CombatView.SetHighlightForCoord(_selected.Coords, TileHighlight.Selected);
        }
    }

    private void ShowAbilityRangeHighlights()
    {
        if (_selected == null || _selectedAbility == null) return;

        // Clear move/attack highlights, show ability range instead
        CombatView?.ClearHighlights();
        _abilityTargetCoords.Clear();

        bool selfTarget = _selectedAbility.AffectsTeam == AffectsTeam.Self;
        Axial center = _selected.Coords;

        if (selfTarget)
        {
            // Self-target: just the caster's tile
            _abilityTargetCoords.Add(center);
            CombatView?.SetHighlightForCoord(center, TileHighlight.AbilityRange);
        }
        else
        {
            // Show all tiles within activeRange that contain valid targets
            foreach (var piece in _engine.Pieces.Where(p => !p.IsDead))
            {
                int dist = Axial.Distance(center, piece.Coords);
                if (dist > _selectedAbility.ActiveRange) continue;

                bool isEnemy = piece.Team != _selected.Team;
                bool isAlly = piece.Team == _selected.Team && piece != _selected;

                switch (_selectedAbility.AffectsTeam)
                {
                    case AffectsTeam.Enemies when isEnemy:
                    case AffectsTeam.Allies when isAlly:
                    case AffectsTeam.All:
                        _abilityTargetCoords.Add(piece.Coords);
                        break;
                }
            }
        }

        // Also show empty tiles within range for positional targeting
        var allRangeTiles = Pathfinding.GetReachable(_engine.Board, center, _selectedAbility.ActiveRange).ReachableTiles;
        foreach (var coord in allRangeTiles)
        {
            if (!_abilityTargetCoords.Contains(coord))
                _abilityTargetCoords.Add(coord);
        }

        // Highlight all ability target coords
        foreach (var coord in _abilityTargetCoords)
            CombatView?.SetHighlightForCoord(coord, TileHighlight.AbilityRange);
    }

    private void OnCombatEnded(Team winner) { }

    private void Update()
    {
        if (_engine == null || _engine.IsOver) return;
        if (_engine.Current == null || _engine.Current.Team != Team.Player) return;
        if (Runner.AutoPlayBothSides) return;

        // Ability selection keys
        HandleAbilityKeys();

        if (Mouse.current.leftButton.wasPressedThisFrame)
            HandleClick();

        // Right-click or Escape to cancel ability
        if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
            CancelAbility();
    }

    private void HandleAbilityKeys()
    {
        if (_selected == null) return;

        var abilities = _selected.Abilities
            .Where(a => a.AbilityType == AbilityType.Active && a.ManaCost <= _selected.Mana)
            .ToList();

        IAbilityData newSelection = null;

        if (Keyboard.current.digit1Key.wasPressedThisFrame && abilities.Count > 0)
            newSelection = abilities[0];
        else if (Keyboard.current.digit2Key.wasPressedThisFrame && abilities.Count > 1)
            newSelection = abilities[1];
        else if (Keyboard.current.digit3Key.wasPressedThisFrame && abilities.Count > 2)
            newSelection = abilities[2];

        if (newSelection != null)
        {
            _selectedAbility = newSelection;
            ShowAbilityRangeHighlights();
        }
    }

    private void HandleClick()
    {
        Ray ray = TargetCamera.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        TileView tv = hit.collider.GetComponentInParent<TileView>();
        if (tv == null) return;

        if (_selected == null) return;
        Axial clickedCoord = tv.Coord;

        // If ability is selected, try to use it
        if (_selectedAbility != null)
        {
            if (_abilityTargetCoords.Contains(clickedCoord))
            {
                bool ok = _engine.UseAbility(_selected, _selectedAbility, clickedCoord);
                if (ok)
                {
                    _selectedAbility = null;
                    ClearHighlights();
                }
            }
            return;
        }

        // Normal move/attack mode
        if (_currentReachable.Contains(clickedCoord))
        {
            _engine.Move(_selected, clickedCoord);
            ClearHighlights();
            return;
        }

        foreach (var target in _currentAttackable)
        {
            if (target.Coords.Equals(clickedCoord))
            {
                _engine.Attack(_selected, target);
                ClearHighlights();
                return;
            }
        }
    }

    private void CancelAbility()
    {
        if (_selectedAbility == null) return;
        _selectedAbility = null;
        _abilityTargetCoords.Clear();
        CombatView?.ClearHighlights();
        ShowMoveAndAttackHighlights();
    }

    private void ClearHighlights()
    {
        _currentReachable.Clear();
        _currentAttackable.Clear();
        _abilityTargetCoords.Clear();
        _selectedAbility = null;
        CombatView?.ClearHighlights();
    }
}
