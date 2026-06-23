using UnityEngine;
using Game.Core;

/// <summary>
/// Thin Unity driver for the combat core. For now it builds a demo combat and
/// logs the play-by-play to the Console so the slice is verifiable before any
/// visuals exist. When the MCP/scene layer is wired, turn off AutoPlayBothSides
/// and feed player actions from input instead.
/// </summary>
public class CombatRunner : MonoBehaviour
{
    [Header("Board size")]
    public int Width = 6;
    public int Height = 5;

    [Header("Characters")]
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
    public CombatEngine Engine => _engine;

    public void BeginCombat() => Invoke(nameof(StartCombat), 0.3f);

    private void StartCombat() => _engine.Begin();

    private void Awake()
    {
        var board = Board.CreateRectangle(Width, Height);

        var pieces = new[]
        {
            PlayerQueenData.CreatePiece("P_Queen", Team.Player, new Axial(0, 0)),
            PlayerPawnData .CreatePiece("P_Pawn",  Team.Player, new Axial(1, 0)),
            EnemyQueenData .CreatePiece("E_Queen", Team.Enemy,  new Axial(Width - 1, Height - 1)),
            EnemyPawnData  .CreatePiece("E_Pawn",  Team.Enemy,  new Axial(Width - 2, Height - 1)),
        };

        _engine = new CombatEngine(board, pieces);
    }

    private void Start()
    {
        _engine.PieceMoved    += (p, from, to) => Debug.Log($"{p.Name} moved {from} -> {to}");
        _engine.PieceAttacked += (a, t, dmg)   => Debug.Log($"{a.Name} hit {t.Name} for {dmg}  (HP {t.Hp}/{t.MaxHp})");
        _engine.PieceDied     += p             => Debug.Log($"<color=red>{p.Name} died</color>");
        _engine.CombatEnded   += team          => Debug.Log($"<color=lime>Combat over — {team} wins</color>");
        _engine.TurnChanged   += OnTurnChanged;

        GetComponent<CombatView>()?.OnEngineReady(_engine);
        GetComponent<PlayerInputController>()?.OnEngineReady(_engine);

        BeginCombat();
    }

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
}
