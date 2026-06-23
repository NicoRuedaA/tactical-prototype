using UnityEngine;
using Game.Core;

public enum TileHighlight
{
    Normal,
    Reachable,
    Attackable,
    Selected,
    AbilityRange
}

public class TileView : MonoBehaviour
{
    public Axial Coord { get; set; }

    [SerializeField] private Renderer _renderer;
    [SerializeField] private Material _normalMat;
    [SerializeField] private Material _reachableMat;
    [SerializeField] private Material _attackableMat;
    [SerializeField] private Material _selectedMat;
    [SerializeField] private Material _abilityRangeMat;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
    }

    public void SetHighlight(TileHighlight state)
    {
        if (_renderer == null) return;

        _renderer.material = state switch
        {
            TileHighlight.Reachable => _reachableMat,
            TileHighlight.Attackable => _attackableMat,
            TileHighlight.Selected => _selectedMat,
            TileHighlight.AbilityRange => _abilityRangeMat,
            _ => _normalMat,
        };
    }

    public void AssignMaterials(Material normal, Material reachable, Material attackable, Material selected, Material abilityRange = null)
    {
        _normalMat = normal;
        _reachableMat = reachable;
        _attackableMat = attackable;
        _selectedMat = selected;
        if (abilityRange != null) _abilityRangeMat = abilityRange;
    }
}
