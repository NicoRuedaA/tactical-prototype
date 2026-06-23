using UnityEngine;
using Game.Core;

public class PieceView : MonoBehaviour
{
    public Piece Piece { get; set; }

    [SerializeField] private Renderer _renderer;
    [SerializeField] private GameObject _hpBar;
    [SerializeField] private Transform _hpFill;
    [SerializeField] private Renderer _hpFillRenderer;

    [Header("Mana Bar")]
    [SerializeField] private GameObject _manaBar;
    [SerializeField] private Transform _manaFill;
    [SerializeField] private Renderer _manaFillRenderer;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
    }

    private void LateUpdate()
    {
        // Billboard HP bar and Mana bar to face the camera
        if (Camera.main != null)
        {
            if (_hpBar != null)
                _hpBar.transform.rotation = Camera.main.transform.rotation;
            if (_manaBar != null)
                _manaBar.transform.rotation = Camera.main.transform.rotation;
        }
    }

    public void OnMove(Vector3 destination)
    {
        transform.position = destination;
    }

    public void OnHit()
    {
        UpdateHpBar();
    }

    public void OnDeath()
    {
        Destroy(gameObject);
    }

    public void AssignMaterial(Material material)
    {
        if (_renderer != null)
            _renderer.material = material;
    }

    public void SetHpBarReferences(GameObject bar, Transform fill, Renderer fillRenderer)
    {
        _hpBar = bar;
        _hpFill = fill;
        _hpFillRenderer = fillRenderer;
    }

    private void Start()
    {
        UpdateHpBar();
        UpdateManaBar();
    }

    private void UpdateHpBar()
    {
        if (_hpFill == null || _hpFillRenderer == null || Piece == null) return;
        float ratio = (float)Piece.Hp / Piece.MaxHp;
        ratio = Mathf.Clamp01(ratio);

        _hpFill.localScale = new Vector3(ratio, 1f, 1f);

        // Green > yellow > red based on HP ratio
        Color hpColor;
        if (ratio > 0.5f)
            hpColor = Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f);
        else
            hpColor = Color.Lerp(Color.red, Color.yellow, ratio * 2f);
        _hpFillRenderer.material.color = hpColor;
    }

    private void UpdateManaBar()
    {
        if (_manaFill == null || _manaFillRenderer == null || Piece == null) return;
        if (Piece.MaxMana <= 0)
        {
            _manaBar.SetActive(false);
            return;
        }

        _manaBar.SetActive(true);
        float ratio = (float)Piece.Mana / Piece.MaxMana;
        ratio = Mathf.Clamp01(ratio);
        _manaFill.localScale = new Vector3(ratio, 1f, 1f);
    }

    public void RefreshMana()
    {
        UpdateManaBar();
    }
}
