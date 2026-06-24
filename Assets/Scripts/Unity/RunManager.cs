using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;

/// <summary>
/// DontDestroyOnLoad singleton that orchestrates the full run loop:
/// Combat → Reward → Combat → ... → Run End.
///
/// Owns the RunState (piece roster + HP persistence) and drives scene
/// transitions via SceneManager.sceneLoaded.
/// </summary>
public sealed class RunManager : MonoBehaviour
{
    [Header("Player Team (initial roster)")]
    public CharacterData[] PlayerTeam;

    [Header("Enemy Teams (per-combat config)")]
    public CharacterData[] EnemyTeamCombat0;
    public CharacterData[] EnemyTeamCombat1;
    public CharacterData[] EnemyTeamCombat2;

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static RunManager Instance { get; private set; }

    public RunState CurrentRun { get; private set; }

    /// <summary>Increments each time the player clears a combat. Used to index enemy team config.</summary>
    private int _currentCombatIndex;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Run lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a new run: builds initial pieces from PlayerTeam data, creates
    /// RunState, and loads the first combat scene.
    /// </summary>
    public void StartNewRun()
    {
        if (PlayerTeam == null || PlayerTeam.Length == 0)
        {
            Debug.LogError("RunManager.StartNewRun: PlayerTeam is null or empty!");
            return;
        }

        var pieces = new List<Piece>();
        for (int i = 0; i < PlayerTeam.Length; i++)
        {
            var data = PlayerTeam[i];
            if (data == null)
            {
                Debug.LogError($"RunManager.StartNewRun: PlayerTeam[{i}] is null!");
                return;
            }
            var piece = data.CreatePiece($"run_piece_{i}", Team.Player, default(Axial));
            pieces.Add(piece);
        }

        _currentCombatIndex = 0;
        CurrentRun = new RunState(pieces, totalCombats: 3);
        Debug.Log($"Run started with {pieces.Count} pieces, 3 combats");

        LoadCombatScene();
    }

    /// <summary>
    /// Called by CombatRunner when combat ends.
    /// </summary>
    public void OnCombatEnded(Team winner)
    {
        if (CurrentRun == null) return;

        if (winner == Team.Player)
        {
            bool moreCombats = CurrentRun.AdvanceCombat();
            if (moreCombats)
            {
                _currentCombatIndex++;
                Debug.Log($"Combat {_currentCombatIndex} won! Loading reward...");
                SceneManager.sceneLoaded += OnRewardSceneLoaded;
                SceneManager.LoadScene("Reward");
            }
            else
            {
                Debug.Log("<color=yellow>=== RUN VICTORY === All combats cleared!</color>");
                EndRun();
            }
        }
        else
        {
            Debug.Log("<color=red>=== RUN DEFEATED ===</color>");
            RestartRun();
        }
    }

    /// <summary>
    /// Called by RewardScreen after the player picks a reward.
    /// </summary>
    public void OnRewardApplied()
    {
        SceneManager.sceneLoaded += OnCombatSceneLoaded;
        SceneManager.LoadScene("Combat");
    }

    /// <summary>
    /// Returns the enemy team composition for a given combat index.
    /// </summary>
    public CharacterData[] GetEnemyTeam(int combatIndex)
    {
        return combatIndex switch
        {
            0 => EnemyTeamCombat0,
            1 => EnemyTeamCombat1,
            2 => EnemyTeamCombat2,
            _ => null,
        };
    }

    private void EndRun()
    {
        CurrentRun = null;
        SceneManager.LoadScene("SampleScene");
    }

    private void RestartRun()
    {
        CurrentRun = null;
        Destroy(gameObject);
        SceneManager.LoadScene("SampleScene");
    }

    // ── Scene load handlers ──────────────────────────────────────────────────

    private void OnCombatSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnCombatSceneLoaded;

        if (scene.name != "Combat") return;

        var runner = FindObjectOfType<CombatRunner>();
        if (runner == null)
        {
            Debug.LogError("CombatRunner not found in Combat scene!");
            return;
        }

        runner.Initialize(CurrentRun, _currentCombatIndex);
        runner.CombatEnded += OnCombatEnded;
    }

    private void OnRewardSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnRewardSceneLoaded;

        if (scene.name != "Reward") return;

        // RewardScreen finds RunManager.Instance on its own via OnEnable
        Debug.Log("Reward scene loaded — awaiting player selection.");
    }

    private void LoadCombatScene()
    {
        SceneManager.sceneLoaded += OnCombatSceneLoaded;
        SceneManager.LoadScene("Combat");
    }
}
