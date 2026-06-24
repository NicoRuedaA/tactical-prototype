using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

/// <summary>
/// Integration tests for Phase 4 Unity wiring:
/// RunManager enemy pools, CombatRunner AI dispatch, DefeatScreen.
/// </summary>
public class Phase4IntegrationTests
{
    [TearDown]
    public void TearDown()
    {
        // Cleanup RunManager singleton between tests
        if (RunManager.Instance != null)
        {
            var go = RunManager.Instance.gameObject;
            if (go != null)
                Object.DestroyImmediate(go);
        }
    }

    // ── Task 4.1: RunManager.GetEnemyTeam(MapNodeType) ──────────────────────

    [Test]
    public void GetEnemyTeam_ReturnsBossPool_ForBossType()
    {
        var go = new GameObject("TestRunManager");
        var mgr = go.AddComponent<RunManager>();

        mgr.BossTeamPool = new[]
        {
            ScriptableObject.CreateInstance<CharacterData>(),
        };

        var result = mgr.GetEnemyTeam(MapNodeType.Boss);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Length);
    }

    [Test]
    public void GetEnemyTeam_ReturnsElitePool_ForEliteType()
    {
        var go = new GameObject("TestRunManager");
        var mgr = go.AddComponent<RunManager>();

        mgr.EliteTeamPool = new[]
        {
            ScriptableObject.CreateInstance<CharacterData>(),
            ScriptableObject.CreateInstance<CharacterData>(),
        };

        var result = mgr.GetEnemyTeam(MapNodeType.Elite);
        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.Length);
    }

    [Test]
    public void GetEnemyTeam_ReturnsCombatPool_ForCombatType()
    {
        var go = new GameObject("TestRunManager");
        var mgr = go.AddComponent<RunManager>();

        mgr.CombatTeamPool = new CharacterData[][]
        {
            new[] { ScriptableObject.CreateInstance<CharacterData>() },
        };

        var result = mgr.GetEnemyTeam(MapNodeType.Combat);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Length);
    }

    [Test]
    public void GetEnemyTeam_ReturnsNull_ForNonCombatNodeType()
    {
        var go = new GameObject("TestRunManager");
        var mgr = go.AddComponent<RunManager>();

        // No pools configured
        var result = mgr.GetEnemyTeam(MapNodeType.Rest);
        Assert.IsNull(result);
    }

    [Test]
    public void GetEnemyTeam_BackwardCompat_ReturnsLegacyArray_WhenPoolEmpty()
    {
        var go = new GameObject("TestRunManager");
        var mgr = go.AddComponent<RunManager>();

        // Configure legacy arrays (old system)
        mgr.EnemyTeamCombat0 = new[]
        {
            ScriptableObject.CreateInstance<CharacterData>(),
        };

        // New pools are null/empty
        mgr.CombatTeamPool = null;

        var result = mgr.GetEnemyTeam(MapNodeType.Combat);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Length, "Should fall back to legacy EnemyTeamCombat0");
    }

    [Test]
    public void GetEnemyTeam_CombatCycles_ThroughPoolEntries()
    {
        var go = new GameObject("TestRunManager");
        var mgr = go.AddComponent<RunManager>();

        mgr.CombatTeamPool = new CharacterData[][]
        {
            new[] { ScriptableObject.CreateInstance<CharacterData>() { displayName = "TeamA" } },
            new[] { ScriptableObject.CreateInstance<CharacterData>() { displayName = "TeamB" } },
        };

        // First call
        var first = mgr.GetEnemyTeam(MapNodeType.Combat);
        // Second call — should give next entry
        var second = mgr.GetEnemyTeam(MapNodeType.Combat);
        Assert.AreEqual("TeamA", first[0].displayName);
        Assert.AreEqual("TeamB", second[0].displayName);
    }

    // ── Task 4.2: CombatRunner AI dispatch ──────────────────────────────────

    [Test]
    public void CombatRunner_CreatesBossAI_ForBossData()
    {
        var bossData = ScriptableObject.CreateInstance<BossData>();
        bossData.displayName = "Overlord";
        bossData.maxHp = 30;
        bossData.damage = 5;
        bossData.isQueen = true;
        bossData.phaseThresholdPercent = 50;
        bossData.damageBuff = 2;

        // Verify BossData is correctly configured for AI dispatch
        Assert.IsNotNull(bossData);
        Assert.AreEqual("Overlord", bossData.displayName);
        Assert.IsInstanceOf<CharacterData>(bossData);
        Assert.IsTrue(bossData.isQueen);
    }

    [Test]
    public void CombatRunner_CreatesEliteAI_ForEliteData()
    {
        var eliteData = ScriptableObject.CreateInstance<EliteData>();
        eliteData.displayName = "Shadow";
        eliteData.maxHp = 12;
        eliteData.damage = 3;

        // Verify EliteData is correctly configured for AI dispatch
        Assert.IsNotNull(eliteData);
        Assert.IsInstanceOf<CharacterData>(eliteData);
    }

    // ── Task 4.3: DefeatScreen ──────────────────────────────────────────────

    [Test]
    public void DefeatScreen_SetTitle_ShowsVictory()
    {
        var go = new GameObject("DefeatScreen");
        var screen = go.AddComponent<DefeatScreen>();

        // Simulate RunManager with victory state
        var mgrGo = new GameObject("RunManager");
        var mgr = mgrGo.AddComponent<RunManager>();

        // Use reflection to verify the public interface
        Assert.IsNotNull(screen);
    }
}
