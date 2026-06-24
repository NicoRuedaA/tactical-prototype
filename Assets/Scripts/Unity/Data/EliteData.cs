using UnityEngine;
using Game.Core;

/// <summary>
/// Data-driven elite enemy definition. Extends <see cref="CharacterData"/> with
/// a passive ability that is injected at piece creation time.
///
/// Create via: Right-click -> Create -> TacticalRogue -> Elite Data
/// </summary>
[CreateAssetMenu(fileName = "NewElite", menuName = "TacticalRogue/Elite Data")]
public class EliteData : CharacterData
{
    [Header("Elite Passive")]
    [Tooltip("Passive ability injected into the piece at creation time (e.g., Thorns, Burning Aura).")]
    public AbilityData elitePassive;

    /// <summary>
    /// Factory method — creates a fully wired Piece from this elite definition.
    /// Injects <see cref="elitePassive"/> into the piece's abilities at creation time.
    /// </summary>
    public override Piece CreatePiece(string id, Team team, Axial coords)
    {
        var piece = base.CreatePiece(id, team, coords);
        if (elitePassive != null)
            piece.AddAbility(elitePassive);
        return piece;
    }
}
