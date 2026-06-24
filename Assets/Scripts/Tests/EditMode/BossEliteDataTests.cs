using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

/// <summary>
/// Tests for BossData and EliteData ScriptableObject CreatePiece behavior.
/// These verify the factory method produces correct Piece objects from data.
/// Uses ScriptableObject.CreateInstance<T>() — valid in EditMode tests.
/// </summary>
public class BossEliteDataTests
{
    [Test]
    public void BossData_CreatePiece_ReturnsPieceWithCorrectStats()
    {
        var data = ScriptableObject.CreateInstance<BossData>();
        data.displayName  = "Infernal King";
        data.maxHp        = 30;
        data.maxMana      = 15;
        data.damage       = 5;
        data.attackRange  = 2;
        data.moveRange    = 1;
        data.initiative   = 8;
        data.isQueen      = true;
        data.damageBuff   = 2;
        data.phaseThresholdPercent = 50;

        var piece = data.CreatePiece("boss_01", Team.Enemy, new Axial(0, 0));

        Assert.AreEqual(30,        piece.MaxHp);
        Assert.AreEqual(15,        piece.MaxMana);
        Assert.AreEqual(5,         piece.Damage);
        Assert.AreEqual(2,         piece.AttackRange);
        Assert.AreEqual(1,         piece.MoveRange);
        Assert.AreEqual(8,         piece.Initiative);
        Assert.IsTrue(piece.IsQueen);
        Assert.AreEqual("Infernal King", piece.Name);
        Assert.AreEqual("boss_01", piece.Id);
        Assert.AreEqual(Team.Enemy, piece.Team);
        Assert.AreEqual(new Axial(0, 0), piece.Coords);
    }

    [Test]
    public void BossData_CreatePiece_DoesNotInjectPhaseAbility()
    {
        var data = ScriptableObject.CreateInstance<BossData>();
        data.maxHp = 30;
        data.damage = 5;

        var phaseAbility = ScriptableObject.CreateInstance<AbilityData>();
        phaseAbility.name = "PhaseStrike";
        data.phaseAbility = phaseAbility;

        var piece = data.CreatePiece("boss_02", Team.Enemy, new Axial(1, 1));

        // Phase ability should NOT be in the piece's abilities at creation time
        // BossEnemyAI adds it on phase threshold trigger
        Assert.IsFalse(piece.Abilities.Contains(phaseAbility));
    }

    [Test]
    public void BossData_CreatePiece_DamageBuffFieldIsStored()
    {
        var data = ScriptableObject.CreateInstance<BossData>();
        data.damageBuff = 2;
        data.phaseThresholdPercent = 50;

        Assert.AreEqual(2,  data.damageBuff);
        Assert.AreEqual(50, data.phaseThresholdPercent);
    }

    [Test]
    public void EliteData_CreatePiece_ReturnsPieceWithCorrectStats()
    {
        var data = ScriptableObject.CreateInstance<EliteData>();
        data.displayName = "Shadow Assassin";
        data.maxHp       = 12;
        data.maxMana     = 8;
        data.damage      = 3;
        data.attackRange = 2;
        data.moveRange   = 2;
        data.initiative  = 6;
        data.isQueen     = false;

        var piece = data.CreatePiece("elite_01", Team.Enemy, new Axial(2, 0));

        Assert.AreEqual(12,        piece.MaxHp);
        Assert.AreEqual(8,         piece.MaxMana);
        Assert.AreEqual(3,         piece.Damage);
        Assert.AreEqual(2,         piece.AttackRange);
        Assert.AreEqual(2,         piece.MoveRange);
        Assert.AreEqual(6,         piece.Initiative);
        Assert.IsFalse(piece.IsQueen);
        Assert.AreEqual("Shadow Assassin", piece.Name);
        Assert.AreEqual("elite_01", piece.Id);
        Assert.AreEqual(Team.Enemy, piece.Team);
        Assert.AreEqual(new Axial(2, 0), piece.Coords);
    }

    [Test]
    public void EliteData_CreatePiece_InjectsPassiveAbility()
    {
        var data = ScriptableObject.CreateInstance<EliteData>();
        data.maxHp = 12;
        data.damage = 3;

        var passive = ScriptableObject.CreateInstance<AbilityData>();
        passive.name = "Thorns";
        data.elitePassive = passive;

        var piece = data.CreatePiece("elite_02", Team.Enemy, new Axial(0, 0));

        // EliteData.CreatePiece MUST add the passive to Piece.Abilities
        Assert.Contains(passive, new System.Collections.Generic.List<IAbilityData>(piece.Abilities));
    }

    [Test]
    public void EliteData_CreatePiece_NullPassiveDoesNotCrash()
    {
        var data = ScriptableObject.CreateInstance<EliteData>();
        data.maxHp = 12;
        data.damage = 3;
        data.elitePassive = null; // explicitly null

        Piece piece = null;
        Assert.DoesNotThrow(() =>
            piece = data.CreatePiece("elite_03", Team.Enemy, new Axial(1, 0))
        );
        Assert.IsNotNull(piece);
        Assert.AreEqual(12, piece.MaxHp);
    }

    [Test]
    public void EliteData_CreatePiece_InjectsPassive_OnlyOnce()
    {
        var data = ScriptableObject.CreateInstance<EliteData>();
        data.maxHp = 12;
        data.damage = 3;

        var passive = ScriptableObject.CreateInstance<AbilityData>();
        passive.name = "Thorns";
        data.elitePassive = passive;

        // Create piece twice — passive should appear exactly once each time
        var piece1 = data.CreatePiece("e1", Team.Enemy, new Axial(0, 0));
        var piece2 = data.CreatePiece("e2", Team.Enemy, new Axial(1, 0));

        int count1 = 0;
        foreach (var a in piece1.Abilities) if (a == passive) count1++;
        Assert.AreEqual(1, count1, "Passive should appear exactly once in piece");

        int count2 = 0;
        foreach (var a in piece2.Abilities) if (a == passive) count2++;
        Assert.AreEqual(1, count2, "Passive should appear exactly once in piece");
    }

    [Test]
    public void BossData_DefaultFields_HaveExpectedDefaults()
    {
        var data = ScriptableObject.CreateInstance<BossData>();

        Assert.AreEqual(2,  data.damageBuff,          "damageBuff default should be 2");
        Assert.AreEqual(50, data.phaseThresholdPercent, "phaseThresholdPercent default should be 50");
        Assert.IsNull(data.phaseAbility, "phaseAbility default should be null");
    }
}
