using UnityEngine;
using Game.Core;

public enum TileHighlight
{
    Normal,
    Reachable,
    Attackable,
    Selected,
    AbilityRange,
    Invalid,
    Cancelled,
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

    private MaterialPropertyBlock _propertyBlock;
    public TileHighlight CurrentHighlight { get; private set; } = TileHighlight.Normal;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
    }

    public void SetHighlight(TileHighlight state)
    {
        if (_renderer == null) return;

        CurrentHighlight = state;
        _renderer.SetPropertyBlock(null);

        _renderer.sharedMaterial = state switch
        {
            TileHighlight.Reachable => _reachableMat,
            TileHighlight.Attackable => _attackableMat,
            TileHighlight.Selected => _selectedMat,
            TileHighlight.AbilityRange => _abilityRangeMat,
            TileHighlight.Invalid => _selectedMat,
            TileHighlight.Cancelled => _selectedMat,
            _ => _normalMat,
        };

        if (state == TileHighlight.Invalid)
            ApplyTint(new Color(1f, 0.05f, 0.65f, 1f));
        else if (state == TileHighlight.Cancelled)
            ApplyTint(new Color(0.55f, 0.62f, 0.72f, 1f));
    }

    public void AssignMaterials(Material normal, Material reachable, Material attackable, Material selected, Material abilityRange = null)
    {
        _normalMat = normal;
        _reachableMat = reachable;
        _attackableMat = attackable;
        _selectedMat = selected;
        if (abilityRange != null) _abilityRangeMat = abilityRange;
    }

    private void ApplyTint(Color color)
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();
        _propertyBlock.Clear();
        _propertyBlock.SetColor("_BaseColor", color);
        _propertyBlock.SetColor("_Color", color);
        _renderer.SetPropertyBlock(_propertyBlock);
    }
}
