using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;

/// <summary>
/// DontDestroyOnLoad singleton that orchestrates the full run loop:
/// Map → Combat → Reward → Map → ... → Boss → Victory.
///
/// Owns the RunState (piece roster + HP persistence), the MapGraph,
/// and drives scene transitions via SceneManager.sceneLoaded.
///
/// CurrentPhase tracks the run state machine position.
/// </summary>
public sealed class RunManager : MonoBehaviour
{
    [Header("Player Team (initial roster)")]
    public CharacterData[] PlayerTeam;

    [Header("Enemy Teams (per-combat config)")]
    public CharacterData[] EnemyTeamCombat0;
    public CharacterData[] EnemyTeamCombat1;
    public CharacterData[] EnemyTeamCombat2;

    [Header("Rest Node")]
    [Tooltip("Percentage of EffectiveMaxHp healed at Rest nodes.")]
    public int RestHealPercent = 30;

    // ── Run Phase State Machine ───────────────────────────────────────────────

    /// <summary>
    /// Tracks which phase of the run loop the manager is in.
    /// Used for dispatch — no nested conditional chains.
    /// </summary>
    public enum RunPhase
    {
        None,
        Map,
        Combat,
        Reward,
        Victory,
        Defeat
    }

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static RunManager Instance { get; private set; }

    public RunState CurrentRun { get; private set; }

    /// <summary>Current phase of the run state machine.</summary>
    public RunPhase CurrentPhase { get; private set; } = RunPhase.None;

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
    /// Starts a new run: builds initial pieces from PlayerTeam data,
    /// generates a MapGraph via MapGenerator, creates RunState, and
    /// loads the Map scene.
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
        var graph = MapGenerator.Generate(seed: null, rows: 2, nodesPerRow: 3);
        CurrentRun = new RunState(pieces, graph);
        CurrentPhase = RunPhase.Map;
        Debug.Log($"Run started with {pieces.Count} pieces, {graph.Nodes.Count} map nodes");

        LoadMapScene();
    }

    /// <summary>
    /// Called by MapView when the player clicks a node on the map.
    /// Validates the node is available, visits it, and dispatches
    /// to the appropriate scene based on node type.
    /// </summary>
    public void OnNodeSelected(string nodeId)
    {
        if (CurrentRun == null)
        {
            Debug.LogWarning("RunManager.OnNodeSelected: No active run.");
            return;
        }

        if (CurrentPhase != RunPhase.Map)
        {
            Debug.LogWarning($"RunManager.OnNodeSelected: Not in Map phase (CurrentPhase={CurrentPhase})");
            return;
        }

        // Validate the node is available
        var available = CurrentRun.GetAvailableNodes();
        if (!available.Contains(nodeId))
        {
            Debug.LogWarning($"RunManager.OnNodeSelected: Node '{nodeId}' is not available. " +
                             $"Available: [{string.Join(", ", available)}]");
            return;
        }

        // Visit the node
        CurrentRun.VisitNode(nodeId);
        var node = CurrentRun.Graph.Nodes[nodeId];
        Debug.Log($"Node selected: {nodeId} ({node.Type})");

        switch (node.Type)
        {
            case MapNodeType.Combat:
            case MapNodeType.Elite:
            case MapNodeType.Boss:
                CurrentPhase = RunPhase.Combat;
                LoadCombatScene();
                break;

            case MapNodeType.Rest:
                ApplyRestHeal();
                // Stay in Map phase — reload the map
                LoadMapScene();
                break;

            case MapNodeType.Shop:
                // Visual-only for v0.4 — return to map
                LoadMapScene();
                break;
        }
    }

    /// <summary>
    /// Called by CombatRunner when combat ends.
    /// Player victory always loads Reward scene. Defeat restarts.
    /// Victory detection (isComplete) happens in OnRewardApplied.
    /// </summary>
    public void OnCombatEnded(Team winner)
    {
        if (CurrentRun == null) return;

        if (winner == Team.Player)
        {
            _currentCombatIndex++;
            CurrentPhase = RunPhase.Reward;
            Debug.Log($"Combat {_currentCombatIndex} won! Loading reward...");
            SceneManager.sceneLoaded += OnRewardSceneLoaded;
            SceneManager.LoadScene("Reward");
        }
        else
        {
            CurrentPhase = RunPhase.Defeat;
            Debug.Log("<color=red>=== RUN DEFEATED ===</color>");
            RestartRun();
        }
    }

    /// <summary>
    /// Called by RewardScreen after the player picks a reward.
    /// If the Boss node has been visited, the run is complete → Victory.
    /// Otherwise, returns to the Map scene for the next node selection.
    /// </summary>
    public void OnRewardApplied()
    {
        if (CurrentRun == null) return;

        if (CurrentRun.Graph.IsComplete)
        {
            CurrentPhase = RunPhase.Victory;
            Debug.Log("<color=yellow>=== RUN VICTORY === Boss defeated!</color>");
            EndRun();
        }
        else
        {
            CurrentPhase = RunPhase.Map;
            LoadMapScene();
        }
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

    private void ApplyRestHeal()
    {
        int count = 0;
        foreach (var piece in CurrentRun.GetAlivePlayerPieces())
        {
            piece.HealPercentEffective(RestHealPercent);
            count++;
        }
        Debug.Log($"Rest healed {count} alive pieces by {RestHealPercent}% of EffectiveMaxHp.");
    }

    private void EndRun()
    {
        CurrentRun = null;
        CurrentPhase = RunPhase.None;
        SceneManager.LoadScene("SampleScene");
    }

    private void RestartRun()
    {
        CurrentRun = null;
        CurrentPhase = RunPhase.None;
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

    private void OnMapSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnMapSceneLoaded;

        if (scene.name != "Map") return;

        // MapView finds RunManager.Instance on its own via OnEnable
        Debug.Log("Map scene loaded — MapView will rebuild on OnEnable.");
    }

    private void LoadCombatScene()
    {
        SceneManager.sceneLoaded += OnCombatSceneLoaded;
        SceneManager.LoadScene("Combat");
    }

    private void LoadMapScene()
    {
        SceneManager.sceneLoaded += OnMapSceneLoaded;
        SceneManager.LoadScene("Map");
    }
}
