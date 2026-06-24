using System.Linq;
using UnityEngine;
using Game.Core;

/// <summary>
/// Data-driven character definition. Stats + ability list in one ScriptableObject.
/// Create via: Right-click -> Create -> TacticalRogue -> Character Data
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "TacticalRogue/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Character";
    public Sprite icon;

    [Header("Stats")]
    public int  maxHp      = 5;
    public int  maxMana    = 0;
    public int  damage     = 1;
    public int  attackRange = 1;
    public int  moveRange  = 2;
    public int  initiative = 5;
    public bool isQueen    = false;

    [Header("Abilities")]
    public AbilityData[] abilities;

    /// <summary>
    /// Factory method — creates a fully wired Piece from this definition.
    /// Called by CombatRunner / CombatView at scene load.
    /// Override in derived classes (BossData, EliteData) to inject
    /// phase/passive data at creation time.
    /// </summary>
    public virtual Piece CreatePiece(string id, Team team, Axial coords)
    {
        var piece = new Piece(
            id, team, maxHp, damage, attackRange, moveRange, initiative,
            isQueen, displayName, maxMana,
            abilities?.Cast<IAbilityData>());
        piece.Coords = coords;
        return piece;
    }
}
