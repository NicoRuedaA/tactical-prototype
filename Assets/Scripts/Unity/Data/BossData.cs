using UnityEngine;
using Game.Core;

/// <summary>
/// Data-driven boss definition. Extends <see cref="CharacterData"/> with
/// phase-transition fields and a <see cref="CreatePiece"/> override.
///
/// The phase ability is NOT injected at piece creation — <see cref="BossEnemyAI"/>
/// adds it dynamically when the boss HP drops below the threshold during combat.
///
/// Create via: Right-click -> Create -> TacticalRogue -> Boss Data
/// </summary>
[CreateAssetMenu(fileName = "NewBoss", menuName = "TacticalRogue/Boss Data")]
public class BossData : CharacterData
{
    [Header("Boss Phase")]
    [Tooltip("Ability granted to the boss when its HP drops below the threshold (added by BossEnemyAI at runtime).")]
    public AbilityData phaseAbility;

    [Tooltip("Bonus damage applied to the boss when phase triggers (default 2).")]
    public int damageBuff = 2;

    [Tooltip("HP percentage (0-100) that triggers phase 2. E.g., 50 means phase triggers when HP <= 49% of max.")]
    [Range(1, 99)]
    public int phaseThresholdPercent = 50;

    /// <summary>
    /// Factory method — creates a fully wired Piece from this boss definition.
    /// Phase ability is NOT injected here; BossEnemyAI handles phase logic at runtime.
    /// </summary>
    public override Piece CreatePiece(string id, Team team, Axial coords)
    {
        var piece = base.CreatePiece(id, team, coords);
        return piece;
    }
}
