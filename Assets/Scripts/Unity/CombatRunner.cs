using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

/// <summary>
/// Thin Unity driver for the combat core.
///
/// Two entry modes:
/// 1. Run loop:  Initialize(RunState, MapNodeType) — called by RunManager after scene load.
/// 2. Fallback:  InitializeDemo() — hardcoded setup for direct scene editing.
///
/// Awake() is intentionally empty — the entry point is always explicit.
/// </summary>
public class CombatRunner : MonoBehaviour
{
    [Header("Scene references")]
    public CombatView CombatView;
    public PlayerInputController PlayerInput;

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

    [Tooltip("When opening the Combat scene directly, initialize a self-playing demo if no RunManager exists.")]
    public bool AutoStartDemoWhenOpenedDirectly = true;

    [Tooltip("Seconds between AI turns so you can watch the action.")]
    public float TurnDelay = 0.6f;

    private CombatEngine _engine;
    private bool _initialized;
    private RunState _runState;
    private Dictionary<string, IEnemyAI> _pieceAIs = new Dictionary<string, IEnemyAI>();
    private bool _aiTurnScheduled;

    private const float FeedbackPollDelay = 0.02f;

    public CombatEngine Engine => _engine;

    /// <summary>True after the deferred combat startup has invoked CombatEngine.Begin.</summary>
    public bool HasCombatStarted { get; private set; }

    /// <summary>Fired when combat ends. RunManager subscribes to this.</summary>
    public event Action<Team> CombatEnded;

    // ── Entry points ──────────────────────────────────────────────────────────

    /// <summary>
    /// Primary entry point for run loop. Called by RunManager after scene load.
    /// Creates board and pieces from RunState + enemy team keyed by node type,
    /// assigns per-type AI (BossEnemyAI, EliteEnemyAI, or DefaultEnemyAI),
    /// wires events, and begins combat.
    /// </summary>
    public void Initialize(RunState runState, MapNodeType nodeType)
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

        // Enemy pieces created from RunManager's per-type pools
        var enemyData = RunManager.Instance.GetEnemyTeam(nodeType);
        for (int i = 0; i < enemyData.Length; i++)
        {
            var data = enemyData[i];
            var enemy = data.CreatePiece($"E_{nodeType}_{i}", Team.Enemy, EnemyStartCoords(i));

            // ── AI dispatch per data type ──────────────────────────────
            IEnemyAI ai = null;
            if (data is BossData bossData)
            {
                ai = new BossEnemyAI(enemy, bossData.phaseAbility,
                    bossData.damageBuff, bossData.phaseThresholdPercent);
            }
            else if (data is EliteData)
            {
                ai = new EliteEnemyAI();
            }
            // else: ai stays null → TakeAiTurn falls back to DefaultEnemyAI

            if (ai != null)
                _pieceAIs[enemy.Id] = ai;

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

        if (!HasDemoCharacterData())
        {
            Debug.LogWarning("CombatRunner.InitializeDemo: Missing fallback CharacterData references. Assign PlayerQueenData, PlayerPawnData, EnemyQueenData, and EnemyPawnData.");
            return;
        }

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

    private void StartCombat()
    {
        if (_engine == null || HasCombatStarted)
            return;

        _engine.Begin();
        HasCombatStarted = true;
    }

    // ── Unity lifecycle (intentionally passive) ───────────────────────────────

    private void Awake()
    {
        if (CombatView == null || PlayerInput == null)
            Debug.LogError(
                "CombatRunner requires explicit CombatView and PlayerInput references.",
                this);
    }

    private void Start()
    {
        if (!_initialized && AutoStartDemoWhenOpenedDirectly && RunManager.Instance == null)
            InitializeDemo();
    }

    private void OnDisable()
    {
        CancelScheduledAiTurn();
    }

    // ── Event wiring ─────────────────────────────────────────────────────────

    private void WireEventsAndBegin()
    {
        if (CombatView == null || PlayerInput == null)
            throw new InvalidOperationException(
                "CombatRunner cannot start because its scene references are missing.");

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

        CombatView.OnEngineReady(_engine);
        PlayerInput.OnEngineReady(_engine);

        BeginCombat();
    }

    // ── Turn handling ─────────────────────────────────────────────────────────

    private void OnTurnChanged(Piece current)
    {
        if (_engine.IsOver || current == null) return;
        Debug.Log($"-- {current.Name}'s turn ({current.Team}) --");

        bool aiDriven = AutoPlayBothSides || current.Team == Team.Enemy;
        if (aiDriven)
            ScheduleAiTurn(TurnDelay);
        else
            CancelScheduledAiTurn();
    }

    /// <summary>
    /// Dispatches AI for the current piece.
    /// Uses stored <see cref="IEnemyAI"/> when available (Boss, Elite).
    /// Falls back to static <see cref="DefaultEnemyAI"/> for standard enemies.
    /// </summary>
    private void TakeAiTurn()
    {
        _aiTurnScheduled = false;
        if (_engine.IsOver) return;

        var current = _engine.Current;
        if (current == null) return;
        if (!AutoPlayBothSides && current.Team != Team.Enemy)
            return;
        if (CombatView != null && CombatView.HasActiveFeedback)
        {
            ScheduleAiTurn(FeedbackPollDelay);
            return;
        }

        if (_pieceAIs.TryGetValue(current.Id, out var ai) && ai != null)
            ai.TakeTurn(_engine);
        else
            DefaultEnemyAI.TakeTurn(_engine);
    }

    private void ScheduleAiTurn(float delay)
    {
        if (_aiTurnScheduled || _engine == null || _engine.IsOver)
            return;
        _aiTurnScheduled = true;
        Invoke(nameof(TakeAiTurn), Mathf.Max(0f, delay));
    }

    private void CancelScheduledAiTurn()
    {
        if (!_aiTurnScheduled)
            return;
        CancelInvoke(nameof(TakeAiTurn));
        _aiTurnScheduled = false;
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

    private bool HasDemoCharacterData()
    {
        return PlayerQueenData != null
            && PlayerPawnData != null
            && EnemyQueenData != null
            && EnemyPawnData != null;
    }
}
