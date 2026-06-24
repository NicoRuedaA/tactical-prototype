using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;

/// <summary>
/// Serializable entry defining one enemy team roster for a specific node type.
/// Used by <see cref="RunManager.enemyTeamPools"/>.
/// </summary>
[System.Serializable]
public struct TeamRoster
{
    public MapNodeType   nodeType;
    public CharacterData[] enemies;
}

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

    [Header("Enemy Team Pools (keyed by MapNodeType)")]
    [Tooltip("Primary pool — each entry provides a team roster for its node type. " +
             "Combat entries cycle through on successive wins.")]
    public TeamRoster[] enemyTeamPools;

    [Header("Legacy Fallback (used when enemyTeamPools is empty)")]
    [Tooltip("Legacy per-index combat teams. Only used as fallback when enemyTeamPools has no Combat entries.")]
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

    /// <summary>True when the last run ended in victory (boss cleared).</summary>
    public bool LastRunWasVictory { get; private set; }

    /// <summary>Increments each time the player clears a combat. Used to index enemy team config.</summary>
    private int _currentCombatIndex;

    /// <summary>Type of the node the player most recently selected on the map.</summary>
    private MapNodeType _currentNodeType;

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
        _currentNodeType = node.Type;
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
    /// Player victory always loads Reward scene.
    /// Defeat loads the GameOver scene with loss outcome.
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
            LastRunWasVictory = false;
            Debug.Log("<color=red>=== RUN DEFEATED ===</color>");
            SceneManager.sceneLoaded += OnGameOverSceneLoaded;
            SceneManager.LoadScene("GameOver");
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
            LastRunWasVictory = true;
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
    /// Returns the enemy team composition for the given node type.
    /// Uses the primary <see cref="enemyTeamPools"/> array.
    /// Falls back to legacy per-index arrays for Combat type when pools are empty.
    /// </summary>
    public CharacterData[] GetEnemyTeam(MapNodeType nodeType)
    {
        if (enemyTeamPools != null && enemyTeamPools.Length > 0)
        {
            var matches = enemyTeamPools.Where(p => p.nodeType == nodeType).ToArray();
            if (matches.Length > 0)
            {
                if (nodeType == MapNodeType.Combat)
                {
                    // Cycle through combat entries per cleared encounter
                    int idx = _currentCombatIndex % matches.Length;
                    return matches[idx].enemies;
                }

                // Boss / Elite: return first matching entry
                return matches[0].enemies;
            }
        }

        // ── Backward compat fallback ──────────────────────────────────────
        if (nodeType == MapNodeType.Combat)
            return GetLegacyEnemyTeam(_currentCombatIndex);

        return null;
    }

    /// <summary>
    /// Legacy index-based lookup — preserved for backward compatibility.
    /// </summary>
    public CharacterData[] GetEnemyTeam(int combatIndex)
    {
        return GetLegacyEnemyTeam(combatIndex);
    }

    private CharacterData[] GetLegacyEnemyTeam(int combatIndex)
    {
        return combatIndex switch
        {
            0 => EnemyTeamCombat0,
            1 => EnemyTeamCombat1,
            2 => EnemyTeamCombat2,
            _ => null,
        };
    }

    /// <summary>
    /// Called by DefeatScreen to return to the main menu.
    /// Destroys the singleton and loads SampleScene so a new run can begin.
    /// </summary>
    public void RestartRun()
    {
        CurrentRun = null;
        CurrentPhase = RunPhase.None;
        _currentCombatIndex = 0;
        Destroy(gameObject);
        SceneManager.LoadScene("SampleScene");
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
        LastRunWasVictory = true;
        CurrentRun = null;
        CurrentPhase = RunPhase.Victory;
        SceneManager.sceneLoaded += OnGameOverSceneLoaded;
        SceneManager.LoadScene("GameOver");
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

        runner.Initialize(CurrentRun, _currentNodeType);
        runner.CombatEnded += OnCombatEnded;
    }

    private void OnRewardSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnRewardSceneLoaded;

        if (scene.name != "Reward") return;

        // RewardScreen finds RunManager.Instance on its own via OnEnable
        Debug.Log("Reward scene loaded — awaiting player selection.");
    }

    private void OnGameOverSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnGameOverSceneLoaded;

        if (scene.name != "GameOver") return;

        Debug.Log($"GameOver scene loaded — outcome: {(LastRunWasVictory ? "VICTORY" : "DEFEAT")}");
        // DefeatScreen finds RunManager.Instance on its own via OnEnable
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
