using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public int Width = 8;
    public int Height = 8;

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

    [Tooltip("Seconds the combat HUD keeps Victory/Defeat visible before the run changes scenes.")]
    [Min(0f)] public float CombatEndDelaySeconds = 1.5f;

    private CombatEngine _engine;
    private bool _initialized;
    private RunState _runState;
    private Dictionary<string, IEnemyAI> _pieceAIs = new Dictionary<string, IEnemyAI>();
    private bool _aiTurnScheduled;
    private Coroutine _combatEndRoutine;
    private bool _combatEndRelayed;
    private bool _combatEndPending;
    private Team _pendingWinner;

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

        var pieces = new List<Piece>(32);
        for (int i = 0; i < 16; i++)
        {
            bool isQueen = i == 0;
            pieces.Add((isQueen ? PlayerQueenData : PlayerPawnData).CreatePiece(
                isQueen ? "P_Queen" : $"P_Pawn_{i:00}", Team.Player, PlayerStartCoords(i)));
            pieces.Add((isQueen ? EnemyQueenData : EnemyPawnData).CreatePiece(
                isQueen ? "E_Queen" : $"E_Pawn_{i:00}", Team.Enemy, EnemyStartCoords(i)));
        }

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
        if (_engine != null)
            _engine.CombatEnded -= OnEngineCombatEnded;
        bool hadPendingRelay = _combatEndRoutine != null;
        if (hadPendingRelay)
        {
            StopCoroutine(_combatEndRoutine);
            _combatEndRoutine = null;
        }
        if (hadPendingRelay)
            _combatEndPending = true;
    }

    private void OnEnable()
    {
        if (_engine != null)
            _engine.CombatEnded -= OnEngineCombatEnded;
        if (_engine != null && !_combatEndRelayed)
            _engine.CombatEnded += OnEngineCombatEnded;
        if (_combatEndPending && _combatEndRoutine == null)
            _combatEndRoutine = StartCoroutine(RelayCombatEndedAfterDelay(_pendingWinner));
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

        // Relay CombatEnded once, after the HUD result has been visible for the
        // configured terminal delay. PlayerInputController listens to the core
        // event directly and renders the result immediately.
        _engine.CombatEnded += OnEngineCombatEnded;

        CombatView.OnEngineReady(_engine);
        PlayerInput.OnEngineReady(_engine);

        BeginCombat();
    }

    private void OnEngineCombatEnded(Team winner)
    {
        if (_combatEndRelayed)
            return;
        _combatEndRelayed = true;
        _combatEndPending = true;
        _pendingWinner = winner;
        Debug.Log($"<color=lime>Combat over — {winner} wins</color>");
        if (CombatEndDelaySeconds <= 0f)
        {
            _combatEndPending = false;
            CombatEnded?.Invoke(winner);
            return;
        }

        _combatEndRoutine = StartCoroutine(RelayCombatEndedAfterDelay(winner));
    }

    private IEnumerator RelayCombatEndedAfterDelay(Team winner)
    {
        yield return new WaitForSecondsRealtime(CombatEndDelaySeconds);
        _combatEndRoutine = null;
        _combatEndPending = false;
        if (isActiveAndEnabled)
            CombatEnded?.Invoke(winner);
    }

    // ── Turn handling ─────────────────────────────────────────────────────────

    private int _enemyActorIndex = 0; // Tracks which enemy piece acts next in phase-based system

    private void OnTurnChanged(Piece current)
    {
        if (_engine.IsOver) return;
        Debug.Log($"-- {_engine.CurrentTeam}'s phase --");

        bool aiDriven = AutoPlayBothSides || _engine.CurrentTeam == Team.Enemy;
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

        if (!AutoPlayBothSides && _engine.CurrentTeam != Team.Enemy)
            return;
        if (CombatView != null && CombatView.HasActiveFeedback)
        {
            ScheduleAiTurn(FeedbackPollDelay);
            return;
        }

        // Phase-based system: AI chooses which piece acts in this phase
        Team actorTeam = AutoPlayBothSides ? _engine.CurrentTeam : Team.Enemy;
        var current = ChooseAiActor(actorTeam);
        if (current == null) return;
        if (!_engine.SelectPiece(current)) return;

        if (_pieceAIs.TryGetValue(current.Id, out var ai) && ai != null)
            ai.TakeTurn(_engine);
        else
            DefaultEnemyAI.TakeTurn(_engine);
    }

    private Piece ChooseAiActor(Team team)
    {
        if (_engine == null) return null;
        var candidates = new List<Piece>(_engine.AliveOf(team));
        if (candidates.Count == 0) return null;

        // Phase-based rotation: cycle through pieces so all get a turn
        // Prefer pieces that can attack, then rotate through others
        var attackers = candidates
            .Where(p => _engine.GetAttackTargets(p).Any())
            .OrderByDescending(p => p.EffectiveDamage)
            .ToList();

        if (attackers.Count > 0)
        {
            // Use the attacker at current index, then advance
            int idx = _enemyActorIndex % attackers.Count;
            _enemyActorIndex++;
            return attackers[idx];
        }

        // The starting formation occupies two complete rows. Pieces on the
        // outer row can therefore be completely boxed in by allied units.
        // Skip those actors while another piece has a legal move; otherwise
        // the AI burns its phase passing even though the formation can advance.
        var movable = candidates
            .Where(p => _engine.GetMoveRange(p).ReachableTiles.Any())
            .ToList();
        if (movable.Count > 0)
        {
            int movableIdx = _enemyActorIndex % movable.Count;
            _enemyActorIndex++;
            return movable[movableIdx];
        }

        // No actor can move or attack: select one so DefaultEnemyAI can pass.
        int blockedIdx = _enemyActorIndex % candidates.Count;
        _enemyActorIndex++;
        return candidates[blockedIdx];
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
        return new Axial(index % Width, index / Width);
    }

    private Axial EnemyStartCoords(int index)
    {
        return new Axial(Width - 1 - (index % Width), Height - 1 - (index / Width));
    }

    private bool HasDemoCharacterData()
    {
        return PlayerQueenData != null
            && PlayerPawnData != null
            && EnemyQueenData != null
            && EnemyPawnData != null;
    }
}
