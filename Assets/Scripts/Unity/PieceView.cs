using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Game.Core;

public enum PieceHighlight
{
    Normal,
    Selected,
    Attackable,
    Ability,
    Invalid,
    Cancelled,
}

public enum CombatFeedbackKind
{
    Damage,
    Heal,
    Mana,
    Buff,
    Debuff,
}

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

    [Header("Target Indicator")]
    [SerializeField] private Renderer _targetIndicator;

    [Header("Feedback Timing")]
    [Min(0f)] public float MoveDuration = 0.18f;
    [Min(0f)] public float HitDuration = 0.16f;
    [Min(0f)] public float DeathDuration = 0.28f;
    [Min(0f)] public float DamageShakeDegrees = 8f;
    public bool CompleteAnimationsImmediately;

    private GameObject _indicatorPrefab;
    private Material _selectedIndicatorMaterial;
    private Material _attackIndicatorMaterial;
    private Material _abilityIndicatorMaterial;
    private MaterialPropertyBlock _indicatorPropertyBlock;
    private MaterialPropertyBlock _hpPropertyBlock;
    private MaterialPropertyBlock _bodyPropertyBlock;
    private Vector3 _hpFullScale = Vector3.one;
    private Vector3 _manaFullScale = Vector3.one;
    private Vector3 _baseScale = Vector3.one;
    private Quaternion _baseRotation = Quaternion.identity;
    private Vector3 _moveDestination;
    private Coroutine _moveRoutine;
    private Coroutine _feedbackRoutine;
    private Coroutine _deathRoutine;
    private Action _deathCompleted;
    private bool _deathCompletionInvoked;

    public PieceHighlight CurrentHighlight { get; private set; } = PieceHighlight.Normal;
    public float HpFillRatio { get; private set; } = 1f;
    public float ManaFillRatio { get; private set; } = 1f;
    public bool IsMoving => _moveRoutine != null;
    public bool HasActiveFeedback => _moveRoutine != null
                                     || _feedbackRoutine != null
                                     || _deathRoutine != null;
    public bool IsDying { get; private set; }
    public bool HasVitalReferences => _hpBar != null
                                      && _hpFill != null
                                      && _hpFillRenderer != null
                                      && _manaBar != null
                                      && _manaFill != null
                                      && _manaFillRenderer != null;

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();
        _indicatorPropertyBlock = new MaterialPropertyBlock();
        _hpPropertyBlock = new MaterialPropertyBlock();
        _bodyPropertyBlock = new MaterialPropertyBlock();
        _baseScale = transform.localScale;
        _baseRotation = transform.localRotation;
        if (_hpFill != null)
            _hpFullScale = _hpFill.localScale;
        if (_manaFill != null)
            _manaFullScale = _manaFill.localScale;
    }

    private void Start()
    {
        RefreshVitals();
    }

    private void LateUpdate()
    {
        if (Camera.main == null)
            return;
        if (_hpBar != null)
            _hpBar.transform.rotation = Camera.main.transform.rotation;
        if (_manaBar != null)
            _manaBar.transform.rotation = Camera.main.transform.rotation;
    }

    public void SetCompleteAnimationsImmediately(bool completeImmediately)
    {
        CompleteAnimationsImmediately = completeImmediately;
        if (completeImmediately)
            CompleteAllFeedbackImmediately();
    }

    public void OnMove(Vector3 destination)
    {
        _moveDestination = destination;
        if (_moveRoutine != null)
            StopCoroutine(_moveRoutine);
        if (CompleteAnimationsImmediately || MoveDuration <= 0f)
        {
            transform.position = destination;
            _moveRoutine = null;
            return;
        }
        _moveRoutine = StartCoroutine(MoveTo(destination));
    }

    public void OnDamage(int amount)
    {
        RefreshVitals();
        PlayVitalFeedback(CombatFeedbackKind.Damage, amount);
    }

    public void OnHeal(int amount)
    {
        RefreshVitals();
        PlayVitalFeedback(CombatFeedbackKind.Heal, amount);
    }

    public void OnManaChanged(int amount)
    {
        RefreshVitals();
        PlayVitalFeedback(CombatFeedbackKind.Mana, amount);
    }

    public void OnBuffChanged(int amount)
    {
        RefreshVitals();
        PlayVitalFeedback(CombatFeedbackKind.Buff, amount);
    }

    public void OnDebuffChanged(int amount)
    {
        RefreshVitals();
        PlayVitalFeedback(CombatFeedbackKind.Debuff, amount);
    }

    public void OnDeath(Action completed)
    {
        if (IsDying)
            return;
        IsDying = true;
        _deathCompleted = completed;
        DisableInteraction();
        if (_hpBar != null)
            _hpBar.SetActive(false);
        if (_manaBar != null)
            _manaBar.SetActive(false);

        if (CompleteAnimationsImmediately || DeathDuration <= 0f)
        {
            CompleteDeath();
            return;
        }
        _deathRoutine = StartCoroutine(PlayDeath());
    }

    public void CompleteAllFeedbackImmediately()
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            transform.position = _moveDestination;
        }
        StopVitalFeedback();
        if (IsDying)
        {
            if (_deathRoutine != null)
                StopCoroutine(_deathRoutine);
            _deathRoutine = null;
            CompleteDeath();
        }
    }

    public void AssignMaterial(Material material)
    {
        if (_renderer != null)
            _renderer.sharedMaterial = material;
    }

    public void ConfigureHighlight(
        GameObject indicatorPrefab,
        Material selectedMaterial,
        Material attackMaterial,
        Material abilityMaterial)
    {
        _indicatorPrefab = indicatorPrefab;
        _selectedIndicatorMaterial = selectedMaterial;
        _attackIndicatorMaterial = attackMaterial;
        _abilityIndicatorMaterial = abilityMaterial;
        EnsureTargetIndicator();
        SetHighlight(PieceHighlight.Normal);
    }

    public void SetHighlight(PieceHighlight state)
    {
        CurrentHighlight = state;
        EnsureTargetIndicator();
        if (_targetIndicator == null)
            return;

        bool visible = state != PieceHighlight.Normal && !IsDying;
        _targetIndicator.gameObject.SetActive(visible);
        if (!visible)
            return;

        _targetIndicator.SetPropertyBlock(null);
        _targetIndicator.sharedMaterial = state switch
        {
            PieceHighlight.Attackable => _attackIndicatorMaterial,
            PieceHighlight.Ability => _abilityIndicatorMaterial,
            PieceHighlight.Invalid => _attackIndicatorMaterial,
            PieceHighlight.Cancelled => _selectedIndicatorMaterial,
            _ => _selectedIndicatorMaterial,
        };

        if (state == PieceHighlight.Invalid)
            ApplyIndicatorTint(new Color(1f, 0.05f, 0.65f, 1f));
        else if (state == PieceHighlight.Cancelled)
            ApplyIndicatorTint(new Color(0.55f, 0.62f, 0.72f, 1f));
    }

    public void SetHpBarReferences(GameObject bar, Transform fill, Renderer fillRenderer)
    {
        _hpBar = bar;
        _hpFill = fill;
        _hpFillRenderer = fillRenderer;
        if (_hpFill != null)
            _hpFullScale = _hpFill.localScale;
    }

    public void SetManaBarReferences(GameObject bar, Transform fill, Renderer fillRenderer)
    {
        _manaBar = bar;
        _manaFill = fill;
        _manaFillRenderer = fillRenderer;
        if (_manaFill != null)
            _manaFullScale = _manaFill.localScale;
    }

    public void RefreshVitals()
    {
        UpdateHpBar();
        UpdateManaBar();
    }

    public void RefreshMana()
    {
        UpdateManaBar();
    }

    private IEnumerator MoveTo(Vector3 destination)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        while (elapsed < MoveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / MoveDuration);
            transform.position = Vector3.Lerp(start, destination, t * t * (3f - 2f * t));
            yield return null;
        }
        transform.position = destination;
        _moveRoutine = null;
    }

    private void PlayVitalFeedback(CombatFeedbackKind kind, int amount)
    {
        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);
        if (CompleteAnimationsImmediately || HitDuration <= 0f)
        {
            StopVitalFeedback();
            return;
        }
        _feedbackRoutine = StartCoroutine(AnimateVitalFeedback(kind));
    }

    private IEnumerator AnimateVitalFeedback(CombatFeedbackKind kind)
    {
        Color color = FeedbackColor(kind);
        float elapsed = 0f;
        while (elapsed < HitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / HitDuration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            ApplyBodyTint(Color.Lerp(color, new Color(color.r, color.g, color.b, 0f), t));
            transform.localScale = _baseScale * (1f + pulse * (kind == CombatFeedbackKind.Heal ? 0.12f : 0.06f));
            transform.localRotation = kind == CombatFeedbackKind.Damage || kind == CombatFeedbackKind.Debuff
                ? _baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * DamageShakeDegrees * (1f - t))
                : _baseRotation;
            yield return null;
        }
        StopVitalFeedback();
    }

    private IEnumerator PlayDeath()
    {
        if (_feedbackRoutine != null && HitDuration > 0f)
            yield return new WaitForSecondsRealtime(HitDuration);
        StopVitalFeedback();
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < DeathDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / DeathDuration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            ApplyBodyTint(new Color(1f, 1f, 1f, 1f - t));
            yield return null;
        }
        CompleteDeath();
    }

    private void CompleteDeath()
    {
        if (_deathCompletionInvoked)
            return;
        _deathCompletionInvoked = true;
        _deathRoutine = null;
        _deathCompleted?.Invoke();
        _deathCompleted = null;
        Destroy(gameObject);
    }

    private void StopVitalFeedback()
    {
        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);
        _feedbackRoutine = null;
        transform.localScale = _baseScale;
        transform.localRotation = _baseRotation;
        if (_renderer != null)
            _renderer.SetPropertyBlock(null);
    }

    private void DisableInteraction()
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        if (_targetIndicator != null)
            _targetIndicator.gameObject.SetActive(false);
    }

    private void UpdateHpBar()
    {
        if (_hpFill == null || _hpFillRenderer == null || Piece == null)
            return;
        int effectiveMaxHp = Mathf.Max(1, Piece.EffectiveMaxHp);
        HpFillRatio = Mathf.Clamp01((float)Piece.Hp / effectiveMaxHp);
        _hpFill.localScale = new Vector3(
            _hpFullScale.x * HpFillRatio,
            _hpFullScale.y,
            _hpFullScale.z);

        Color hpColor = HpFillRatio > 0.5f
            ? Color.Lerp(Color.yellow, Color.green, (HpFillRatio - 0.5f) * 2f)
            : Color.Lerp(Color.red, Color.yellow, HpFillRatio * 2f);
        if (_hpPropertyBlock == null)
            _hpPropertyBlock = new MaterialPropertyBlock();
        _hpPropertyBlock.Clear();
        _hpPropertyBlock.SetColor("_BaseColor", hpColor);
        _hpPropertyBlock.SetColor("_Color", hpColor);
        _hpFillRenderer.SetPropertyBlock(_hpPropertyBlock);
    }

    private void UpdateManaBar()
    {
        if (_manaBar == null || _manaFill == null || _manaFillRenderer == null || Piece == null)
            return;
        if (Piece.MaxMana <= 0)
        {
            ManaFillRatio = 0f;
            _manaBar.SetActive(false);
            return;
        }

        _manaBar.SetActive(true);
        ManaFillRatio = Mathf.Clamp01((float)Piece.Mana / Piece.MaxMana);
        _manaFill.localScale = new Vector3(
            _manaFullScale.x * ManaFillRatio,
            _manaFullScale.y,
            _manaFullScale.z);
    }

    private void EnsureTargetIndicator()
    {
        if (_targetIndicator != null)
            return;

        GameObject indicator;
        if (_indicatorPrefab != null)
        {
            indicator = Instantiate(_indicatorPrefab, transform);
        }
        else
        {
            indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.transform.SetParent(transform, false);
            Collider indicatorCollider = indicator.GetComponent<Collider>();
            if (indicatorCollider != null)
            {
                indicatorCollider.enabled = false;
                if (Application.isPlaying)
                    Destroy(indicatorCollider);
                else
                    DestroyImmediate(indicatorCollider);
            }
        }

        indicator.name = "Target Indicator";
        indicator.transform.localPosition = new Vector3(0f, 0.055f, 0f);
        indicator.transform.localRotation = Quaternion.identity;
        indicator.transform.localScale = new Vector3(1.35f, 0.025f, 1.35f);
        _targetIndicator = indicator.GetComponentInChildren<Renderer>(true);
        if (_targetIndicator != null)
        {
            _targetIndicator.shadowCastingMode = ShadowCastingMode.Off;
            _targetIndicator.receiveShadows = false;
        }
    }

    private void ApplyIndicatorTint(Color color)
    {
        if (_indicatorPropertyBlock == null)
            _indicatorPropertyBlock = new MaterialPropertyBlock();
        _indicatorPropertyBlock.Clear();
        _indicatorPropertyBlock.SetColor("_BaseColor", color);
        _indicatorPropertyBlock.SetColor("_Color", color);
        _targetIndicator.SetPropertyBlock(_indicatorPropertyBlock);
    }

    private void ApplyBodyTint(Color color)
    {
        if (_renderer == null)
            return;
        if (_bodyPropertyBlock == null)
            _bodyPropertyBlock = new MaterialPropertyBlock();
        _bodyPropertyBlock.Clear();
        _bodyPropertyBlock.SetColor("_BaseColor", color);
        _bodyPropertyBlock.SetColor("_Color", color);
        _renderer.SetPropertyBlock(_bodyPropertyBlock);
    }

    private static Color FeedbackColor(CombatFeedbackKind kind)
    {
        return kind switch
        {
            CombatFeedbackKind.Damage => new Color(1f, 0.16f, 0.12f, 1f),
            CombatFeedbackKind.Heal => new Color(0.2f, 1f, 0.42f, 1f),
            CombatFeedbackKind.Mana => new Color(0.2f, 0.75f, 1f, 1f),
            CombatFeedbackKind.Debuff => new Color(0.9f, 0.25f, 0.65f, 1f),
            _ => new Color(1f, 0.78f, 0.18f, 1f),
        };
    }
}
