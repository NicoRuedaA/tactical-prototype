using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

/// <summary>
/// Reward types available for post-combat selection.
/// </summary>
public enum RewardType { StatBoost, NewAbility }

/// <summary>
/// Explicit gameplay effect carried by a reward option.
/// </summary>
public enum RewardEffectKind { StatBoost, MaxHpBoost, NewAbility }

/// <summary>
/// Describes a single reward option the player can pick.
/// </summary>
public readonly struct RewardOption
{
    public readonly string Description;
    public readonly RewardType Type;
    public readonly RewardEffectKind Effect;
    public readonly StatType? Stat;
    public readonly int Amount;
    public readonly IAbilityData Ability;

    public RewardOption(string description, RewardType type, StatType? stat, int amount, IAbilityData ability = null)
        : this(description, ToEffectKind(type), type, stat, amount, ability)
    {
    }

    public RewardOption(string description, RewardEffectKind effect, StatType? stat, int amount, IAbilityData ability = null)
        : this(description, effect, ToRewardType(effect), stat, amount, ability)
    {
    }

    private RewardOption(
        string description,
        RewardEffectKind effect,
        RewardType type,
        StatType? stat,
        int amount,
        IAbilityData ability)
    {
        Description = description;
        Type = type;
        Effect = effect;
        Stat = stat;
        Amount = amount;
        Ability = ability;
    }

    private static RewardEffectKind ToEffectKind(RewardType type)
    {
        return type == RewardType.NewAbility
            ? RewardEffectKind.NewAbility
            : RewardEffectKind.StatBoost;
    }

    private static RewardType ToRewardType(RewardEffectKind effect)
    {
        return effect == RewardEffectKind.NewAbility
            ? RewardType.NewAbility
            : RewardType.StatBoost;
    }
}

/// <summary>
/// Displays 3 random reward cards after a combat victory.
/// Player picks one, then selects an alive recipient before the reward is
/// applied and RunManager is signaled to continue.
/// </summary>
public class RewardScreen : MonoBehaviour
{
    [Header("UI References")]
    public Button CardButton0;
    public Button CardButton1;
    public Button CardButton2;

    public Text CardText0;
    public Text CardText1;
    public Text CardText2;

    public Text TitleText;

    private RewardOption[] _currentOptions;
    private RunState _runState;
    private int _rewardRecipientSeed;
    private RewardOption _pendingOption;
    private bool _isSelectingRecipient;
    private GameObject _recipientContainer;
    private readonly List<Button> _recipientButtons = new List<Button>();

    // ── Reward pool ───────────────────────────────────────────────────────────

    private static readonly RewardOption[] RewardPool = new[]
    {
        new RewardOption("+1 Damage",      RewardEffectKind.StatBoost,  StatType.Damage,      1),
        new RewardOption("+1 Max HP",      RewardEffectKind.MaxHpBoost, null,                1),
        new RewardOption("+1 Move Range",  RewardEffectKind.StatBoost,  StatType.MoveRange,   1),
        new RewardOption("+1 Attack Range", RewardEffectKind.StatBoost, StatType.AttackRange, 1),
        new RewardOption("Learn: Fireball", RewardEffectKind.NewAbility, null,                0, new InlineAbility("Fireball", AbilityType.Active, 2, 2, EffectType.Damage, 3, 0, AffectsTeam.Enemies)),
        new RewardOption("Learn: Heal",    RewardEffectKind.NewAbility, null,                0, new InlineAbility("Heal", AbilityType.Active, 2, 2, EffectType.Heal, 3, 0, AffectsTeam.Allies)),
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        ClearRecipientSelectionUi();
        _isSelectingRecipient = false;
        var mgr = RunManager.Instance;
        if (mgr == null || mgr.CurrentRun == null)
        {
            Debug.LogError("RewardScreen: No active RunState found!");
            return;
        }

        _runState = mgr.CurrentRun;
        _currentOptions = GenerateRewardOptions(mgr.GetStreamSeed(RunRandomStream.RewardOptions));
        _rewardRecipientSeed = mgr.GetStreamSeed(RunRandomStream.RewardRecipient);
        DisplayOptions();
    }

    private void OnDisable()
    {
        ClearRecipientSelectionUi();
        _isSelectingRecipient = false;
    }

    // ── Reward generation ─────────────────────────────────────────────────────

    public static RewardOption[] GenerateRewardOptions(int streamSeed)
    {
        var rng = new DeterministicRandom(streamSeed);
        int optionCount = System.Math.Min(3, RewardPool.Length);
        var indices = rng.PickDistinctIndices(RewardPool.Length, optionCount);
        return indices.Select(index => RewardPool[index]).ToArray();
    }

    // ── UI display ────────────────────────────────────────────────────────────

    private void DisplayOptions()
    {
        SetCardUiVisible(true);
        if (TitleText != null)
            TitleText.text = "CHOOSE A REWARD";

        var cardTexts = new[] { CardText0, CardText1, CardText2 };
        var cardButtons = new[] { CardButton0, CardButton1, CardButton2 };

        for (int i = 0; i < 3; i++)
        {
            if (i < _currentOptions.Length)
            {
                var opt = _currentOptions[i];
                string icon = GetIcon(opt);

                if (cardTexts[i] != null)
                    cardTexts[i].text = $"{icon} {opt.Description}";

                if (cardButtons[i] != null)
                {
                    int capturedIndex = i; // capture for closure
                    cardButtons[i].onClick.RemoveAllListeners();
                    cardButtons[i].onClick.AddListener(() => OnCardClicked(capturedIndex));
                    cardButtons[i].gameObject.SetActive(true);
                }
            }
            else
            {
                if (cardButtons[i] != null)
                    cardButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private static string GetIcon(RewardOption option)
    {
        return option.Effect switch
        {
            RewardEffectKind.StatBoost => option.Stat switch
            {
                StatType.Damage => "\u2694",       // sword
                StatType.AttackRange => "\uD83C\uDFAF", // target
                StatType.MoveRange => "\uD83D\uDC5F",   // boot
                _ => "?"
            },
            RewardEffectKind.MaxHpBoost => "\u2764", // heart
            RewardEffectKind.NewAbility => "\u2B50", // star
            _ => "?"
        };
    }

    // ── Card click handling ───────────────────────────────────────────────────

    private void OnCardClicked(int cardIndex)
    {
        if (_currentOptions == null || cardIndex < 0 || cardIndex >= _currentOptions.Length)
            return;

        _pendingOption = _currentOptions[cardIndex];

        // Prefer an explicit recipient choice. Legacy deterministic random selection
        // remains available when no recipient UI can be created or no one is alive.
        if (GetDeterministicAliveRecipients(_runState).Count > 0 && BuildRecipientSelectionUi())
            return;

        ApplyLegacyRewardFallback(_pendingOption);
    }

    private Piece PickRandomAlivePiece()
    {
        return SelectRewardRecipient(_runState, _rewardRecipientSeed);
    }

    public static Piece SelectRewardRecipient(RunState runState, int streamSeed)
    {
        if (runState == null)
            return null;

        var alivePieces = GetDeterministicAliveRecipients(runState).ToList();

        if (alivePieces.Count == 0)
        {
            // Fallback: use any piece (dead or alive) — shouldn't normally happen
            alivePieces = runState.Pieces.ToList();
        }

        if (alivePieces.Count == 0)
            return null;

        var rng = new DeterministicRandom(streamSeed);
        return alivePieces[rng.Next(alivePieces.Count)];
    }

    /// <summary>
    /// Returns the alive player roster in RunState order. RunState preserves the
    /// authored roster order, so this list is deterministic for a given run.
    /// </summary>
    public static IReadOnlyList<Piece> GetDeterministicAliveRecipients(RunState runState)
    {
        if (runState == null)
            return new List<Piece>();
        return runState.GetAlivePlayerPieces().ToList();
    }

    private void ApplyLegacyRewardFallback(RewardOption option)
    {
        var piece = PickRandomAlivePiece();
        if (piece == null)
        {
            Debug.LogError("RewardScreen: No pieces available to apply reward!");
            return;
        }

        ApplyReward(piece, option);
        Debug.Log($"Reward applied: {option.Description} -> {piece.Name}");
        RunManager.Instance?.OnRewardApplied();
    }

    private bool BuildRecipientSelectionUi()
    {
        try
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
                return false;

            ClearRecipientSelectionUi();
            _recipientContainer = new GameObject("Runtime Reward Recipients", typeof(RectTransform));
            _recipientContainer.transform.SetParent(canvas.transform, false);
            var containerRect = _recipientContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = new Vector2(700f, 420f);

            SetCardUiVisible(false);
            if (TitleText != null)
                TitleText.text = "CHOOSE A UNIT";

            var recipients = GetDeterministicAliveRecipients(_runState);
            for (int i = 0; i < recipients.Count; i++)
                CreateRecipientButton(_recipientContainer.transform, recipients[i], i);

            _isSelectingRecipient = _recipientButtons.Count > 0;
            if (!_isSelectingRecipient)
            {
                ClearRecipientSelectionUi();
                SetCardUiVisible(true);
            }
            return _isSelectingRecipient;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"RewardScreen: Recipient UI unavailable; using deterministic fallback. {exception.Message}");
            ClearRecipientSelectionUi();
            SetCardUiVisible(true);
            return false;
        }
    }

    private void CreateRecipientButton(Transform parent, Piece piece, int index)
    {
        var buttonObject = new GameObject($"Reward Recipient {index}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -20f - index * 72f);
        rect.sizeDelta = new Vector2(560f, 56f);

        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.2f, 0.28f, 1f);
        colors.highlightedColor = new Color(0.25f, 0.35f, 0.5f, 1f);
        colors.pressedColor = new Color(0.35f, 0.45f, 0.65f, 1f);
        button.colors = colors;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 4f);
        labelRect.offsetMax = new Vector2(-14f, -4f);
        var label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = FormatRecipientLabel(piece, _pendingOption);

        string capturedId = piece.Id;
        button.onClick.AddListener(() => OnRecipientClicked(capturedId));
        _recipientButtons.Add(button);
    }

    /// <summary>
    /// Formats the exact effect of a pending reward for one recipient without
    /// mutating the piece. This is intentionally pure so the preview can be
    /// tested independently from the runtime UI.
    /// </summary>
    public static string FormatRewardPreview(Piece piece, RewardOption option)
    {
        if (piece == null)
            return string.Empty;

        switch (option.Effect)
        {
            case RewardEffectKind.StatBoost when option.Stat.HasValue:
                int statBefore = GetEffectiveStat(piece, option.Stat.Value);
                int statAfter = statBefore + option.Amount;
                return $"{option.Description}: {option.Stat.Value} {statBefore}→{statAfter}";

            case RewardEffectKind.MaxHpBoost:
                int hpBefore = piece.Hp;
                int maxHpBefore = piece.EffectiveMaxHp;
                return $"{option.Description}: HP {hpBefore}/{maxHpBefore}→{hpBefore + option.Amount}/{maxHpBefore + option.Amount}";

            case RewardEffectKind.NewAbility:
                string abilityName = option.Ability == null || string.IsNullOrEmpty(option.Ability.DisplayName)
                    ? "ability"
                    : option.Ability.DisplayName;
                return $"{option.Description}: learn ability {abilityName}";

            default:
                return option.Description ?? string.Empty;
        }
    }

    /// <summary>
    /// Formats a deterministic recipient button label including the current
    /// vitals and the selected reward's current→post-reward preview.
    /// </summary>
    public static string FormatRecipientLabel(Piece piece, RewardOption option)
    {
        if (piece == null)
            return string.Empty;

        string preview = FormatRewardPreview(piece, option);
        if (string.IsNullOrEmpty(preview))
            return $"{piece.Name}    HP {piece.Hp}/{piece.EffectiveMaxHp}";

        return $"{piece.Name}    HP {piece.Hp}/{piece.EffectiveMaxHp} — {preview}";
    }

    private static int GetEffectiveStat(Piece piece, StatType stat)
    {
        switch (stat)
        {
            case StatType.Damage:
                return piece.EffectiveDamage;
            case StatType.AttackRange:
                return piece.EffectiveAttackRange;
            case StatType.MoveRange:
                return piece.EffectiveMoveRange;
            default:
                return 0;
        }
    }

    private void OnRecipientClicked(string pieceId)
    {
        if (!_isSelectingRecipient || _runState == null)
            return;

        var piece = _runState.Pieces.FirstOrDefault(candidate => candidate != null && candidate.Id == pieceId
            && !candidate.IsDead);
        if (piece == null)
            return;

        var option = _pendingOption;
        ClearRecipientSelectionUi();
        _isSelectingRecipient = false;
        SetCardUiVisible(true);
        ApplyReward(piece, option);
        Debug.Log($"Reward applied: {option.Description} -> {piece.Name}");
        RunManager.Instance?.OnRewardApplied();
    }

    private void SetCardUiVisible(bool visible)
    {
        var cardButtons = new[] { CardButton0, CardButton1, CardButton2 };
        var cardTexts = new[] { CardText0, CardText1, CardText2 };
        foreach (var button in cardButtons)
            if (button != null) button.gameObject.SetActive(visible);
        foreach (var text in cardTexts)
            if (text != null) text.gameObject.SetActive(visible);
    }

    private void ClearRecipientSelectionUi()
    {
        foreach (var button in _recipientButtons)
            if (button != null) button.onClick.RemoveAllListeners();
        _recipientButtons.Clear();
        if (_recipientContainer != null)
        {
            if (Application.isPlaying)
                Destroy(_recipientContainer);
            else
                DestroyImmediate(_recipientContainer);
            _recipientContainer = null;
        }
    }

    private void ApplyReward(Piece piece, RewardOption option)
    {
        switch (option.Effect)
        {
            case RewardEffectKind.MaxHpBoost:
                _runState.ApplyMaxHpBoost(piece.Id, option.Amount);
                break;

            case RewardEffectKind.StatBoost when option.Stat.HasValue:
                _runState.ApplyStatBoost(piece.Id, option.Stat.Value, option.Amount);
                break;

            case RewardEffectKind.NewAbility when option.Ability != null:
                _runState.AddAbility(piece.Id, option.Ability);
                break;
        }
    }
}

/// <summary>
/// Minimal IAbilityData implementation for inline reward abilities.
/// Avoids requiring ScriptableObject assets for Fireball/Heal test abilities.
/// </summary>
internal class InlineAbility : IAbilityData
{
    public string DisplayName { get; }
    public AbilityType AbilityType { get; }
    public int ManaCost { get; }
    public int ActiveRange { get; }
    public PassiveTrigger Trigger { get; }
    public EffectType EffectType { get; }
    public int EffectValue { get; }
    public StatType StatToModify { get; }
    public int AreaRadius { get; }
    public AffectsTeam AffectsTeam { get; }
    public DurationType DurationType { get; }
    public int DurationTurns { get; }

    public InlineAbility(
        string displayName,
        AbilityType abilityType,
        int manaCost,
        int activeRange,
        EffectType effectType,
        int effectValue,
        int areaRadius,
        AffectsTeam affectsTeam,
        PassiveTrigger trigger = PassiveTrigger.OnHit,
        StatType statToModify = StatType.Damage,
        DurationType durationType = DurationType.FixedTurns,
        int durationTurns = 1)
    {
        DisplayName = displayName;
        AbilityType = abilityType;
        ManaCost = manaCost;
        ActiveRange = activeRange;
        EffectType = effectType;
        EffectValue = effectValue;
        AreaRadius = areaRadius;
        AffectsTeam = affectsTeam;
        Trigger = trigger;
        StatToModify = statToModify;
        DurationType = durationType;
        DurationTurns = durationTurns;
    }
}
