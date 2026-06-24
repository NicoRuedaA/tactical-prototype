using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Core;

/// <summary>
/// Thin Unity driver for the combat core.
///
/// Two entry modes:
/// 1. Run loop:  Initialize(RunState, int) — called by RunManager after scene load.
/// 2. Fallback:  InitializeDemo() — hardcoded setup for direct scene editing.
///
/// Awake() is intentionally empty — the entry point is always explicit.
/// </summary>
public class CombatRunner : MonoBehaviour
{
    [Header("Board size")]
    public int Width = 6;
    public int Height = 5;

    [Header("Characters (fallback only — used when RunState is null)")]
    public CharacterData PlayerQueenData;
    public CharacterData PlayerPawnData;
    public CharacterData EnemyQueenData;
    public CharacterData EnemyPawnData;

    [Tooltip("When true, both sides are driven by the AI (self-playing demo). " +
             "Set false once player input is wired through the view layer.")]
    public bool AutoPlayBothSides = true;

    [Tooltip("Seconds between AI turns so you can watch the action.")]
    public float TurnDelay = 0.6f;

    private CombatEngine _engine;
    private bool _initialized;
    private RunState _runState;

    public CombatEngine Engine => _engine;

    /// <summary>Fired when combat ends. RunManager subscribes to this.</summary>
    public event Action<Team> CombatEnded;

    // ── Entry points ──────────────────────────────────────────────────────────

    /// <summary>
    /// Primary entry point for run loop. Called by RunManager after scene load.
    /// Creates board and pieces from RunState + enemy team data, wires events,
    /// and begins combat. Force-disables AutoPlayBothSides so the player controls
    /// their pieces while enemy AI still auto-plays.
    /// </summary>
    public void Initialize(RunState runState, int combatIndex)
    {
        if (_initialized) return;
        _initialized = true;
        _runState = runState;

        // Force player input on — run loop always expects player control
        AutoPlayBothSides = false;

        var board = Board.CreateRectangle(Width, Height);

        var pieces = new List<Piece>();

        // Player pieces from RunState (alive only — dead pieces skip combat)
        int idx = 0;
        foreach (var playerPiece in runState.GetAlivePlayerPieces())
        {
            playerPiece.Coords = PlayerStartCoords(idx);
            pieces.Add(playerPiece);
            idx++;
        }

        // Enemy pieces created fresh from RunManager's enemy team config
        var enemyData = RunManager.Instance.GetEnemyTeam(combatIndex);
        for (int i = 0; i < enemyData.Length; i++)
        {
            var enemy = enemyData[i].CreatePiece($"E_{combatIndex}_{i}", Team.Enemy, EnemyStartCoords(i));
            pieces.Add(enemy);
        }

        _engine = new CombatEngine(board, pieces);
        WireEventsAndBegin();
    }

    /// <summary>
    /// Fallback entry point for direct scene editing (no RunManager).
    /// Uses inspector-assigned CharacterData slots to build a demo combat.
    /// </summary>
    public void InitializeDemo()
    {
        if (_initialized) return;
        _initialized = true;

        var board = Board.CreateRectangle(Width, Height);

        var pieces = new[]
        {
            PlayerQueenData.CreatePiece("P_Queen", Team.Player, new Axial(0, 0)),
            PlayerPawnData .CreatePiece("P_Pawn",  Team.Player, new Axial(1, 0)),
            EnemyQueenData .CreatePiece("E_Queen", Team.Enemy,  new Axial(Width - 1, Height - 1)),
            EnemyPawnData  .CreatePiece("E_Pawn",  Team.Enemy,  new Axial(Width - 2, Height - 1)),
        };

        _engine = new CombatEngine(board, pieces);
        WireEventsAndBegin();
    }

    public void BeginCombat() => Invoke(nameof(StartCombat), 0.3f);

    private void StartCombat() => _engine.Begin();

    // ── Unity lifecycle (intentionally passive) ───────────────────────────────

    private void Awake()
    {
        // Intentionally empty. Initialization is always explicit via
        // Initialize() or InitializeDemo().
    }

    private void Start()
    {
        // Intentionally empty. Wireup happens in WireEventsAndBegin().
    }

    // ── Event wiring ─────────────────────────────────────────────────────────

    private void WireEventsAndBegin()
    {
        _engine.PieceMoved    += (p, from, to) => Debug.Log($"{p.Name} moved {from} -> {to}");
        _engine.PieceAttacked += (a, t, dmg)   => Debug.Log($"{a.Name} hit {t.Name} for {dmg}  (HP {t.Hp}/{t.EffectiveMaxHp})");
        _engine.PieceDied     += p             => Debug.Log($"<color=red>{p.Name} died</color>");
        _engine.TurnChanged   += OnTurnChanged;

        // Relay CombatEnded to public event
        _engine.CombatEnded += team =>
        {
            Debug.Log($"<color=lime>Combat over — {team} wins</color>");
            CombatEnded?.Invoke(team);
        };

        GetComponent<CombatView>()?.OnEngineReady(_engine);
        GetComponent<PlayerInputController>()?.OnEngineReady(_engine);

        BeginCombat();
    }

    // ── Turn handling ─────────────────────────────────────────────────────────

    private void OnTurnChanged(Piece current)
    {
        if (_engine.IsOver || current == null) return;
        Debug.Log($"-- {current.Name}'s turn ({current.Team}) --");

        bool aiDriven = AutoPlayBothSides || current.Team == Team.Enemy;
        if (aiDriven)
            Invoke(nameof(TakeAiTurn), TurnDelay);
    }

    private void TakeAiTurn()
    {
        if (!_engine.IsOver)
            EnemyTurnAI.TakeTurn(_engine);
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private Axial PlayerStartCoords(int index)
    {
        return new Axial(0, index);
    }

    private Axial EnemyStartCoords(int index)
    {
        return new Axial(Width - 1, index);
    }
}
