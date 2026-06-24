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
/// Describes a single reward option the player can pick.
/// </summary>
public readonly struct RewardOption
{
    public readonly string Description;
    public readonly RewardType Type;
    public readonly StatType? Stat;
    public readonly int Amount;
    public readonly IAbilityData Ability;

    public RewardOption(string description, RewardType type, StatType? stat, int amount, IAbilityData ability = null)
    {
        Description = description;
        Type = type;
        Stat = stat;
        Amount = amount;
        Ability = ability;
    }
}

/// <summary>
/// Displays 3 random reward cards after a combat victory.
/// Player picks one, which is applied to a random alive piece,
/// then signals RunManager to continue.
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

    // ── Reward pool ───────────────────────────────────────────────────────────

    private static readonly RewardOption[] RewardPool = new[]
    {
        new RewardOption("+1 Damage",      RewardType.StatBoost,  StatType.Damage,      1),
        new RewardOption("+1 Max HP",      RewardType.StatBoost,  StatType.Damage,      1), // special handling via ApplyMaxHpBoost
        new RewardOption("+1 Move Range",  RewardType.StatBoost,  StatType.MoveRange,   1),
        new RewardOption("+1 Attack Range", RewardType.StatBoost,  StatType.AttackRange, 1),
        new RewardOption("Learn: Fireball", RewardType.NewAbility, null,                 0, new InlineAbility("Fireball", AbilityType.Active, 2, 2, EffectType.Damage, 3, 0, AffectsTeam.Enemies)),
        new RewardOption("Learn: Heal",    RewardType.NewAbility, null,                 0, new InlineAbility("Heal", AbilityType.Active, 2, 2, EffectType.Heal, 3, 0, AffectsTeam.Allies)),
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        var mgr = RunManager.Instance;
        if (mgr == null || mgr.CurrentRun == null)
        {
            Debug.LogError("RewardScreen: No active RunState found!");
            return;
        }

        _runState = mgr.CurrentRun;
        GenerateOptions();
        DisplayOptions();
    }

    // ── Reward generation ─────────────────────────────────────────────────────

    private void GenerateOptions()
    {
        // Pick 3 distinct options from pool
        var pool = new List<RewardOption>(RewardPool);
        var selected = new List<RewardOption>();

        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            selected.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        _currentOptions = selected.ToArray();
    }

    // ── UI display ────────────────────────────────────────────────────────────

    private void DisplayOptions()
    {
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
        return option.Type switch
        {
            RewardType.StatBoost => option.Stat switch
            {
                StatType.Damage => "\u2694",       // sword
                StatType.AttackRange => "\uD83C\uDFAF", // target
                StatType.MoveRange => "\uD83D\uDC5F",   // boot
                _ => "?"
            },
            RewardType.NewAbility => "\u2B50",     // star
            _ => "?"
        };
    }

    // ── Card click handling ───────────────────────────────────────────────────

    private void OnCardClicked(int cardIndex)
    {
        if (_currentOptions == null || cardIndex < 0 || cardIndex >= _currentOptions.Length)
            return;

        var option = _currentOptions[cardIndex];
        var piece = PickRandomAlivePiece();

        if (piece == null)
        {
            Debug.LogError("RewardScreen: No pieces available to apply reward!");
            return;
        }

        ApplyReward(piece, option);
        Debug.Log($"Reward applied: {option.Description} -> {piece.Name}");

        // Notify RunManager to continue
        var mgr = RunManager.Instance;
        if (mgr != null)
            mgr.OnRewardApplied();
    }

    private Piece PickRandomAlivePiece()
    {
        var alivePieces = _runState.GetAlivePlayerPieces().ToList();

        if (alivePieces.Count == 0)
        {
            // Fallback: use any piece (dead or alive) — shouldn't normally happen
            alivePieces = _runState.Pieces.ToList();
        }

        if (alivePieces.Count == 0)
            return null;

        return alivePieces[Random.Range(0, alivePieces.Count)];
    }

    private void ApplyReward(Piece piece, RewardOption option)
    {
        switch (option.Type)
        {
            case RewardType.StatBoost when option.Stat == StatType.Damage && option.Description == "+1 Max HP":
                // Special case: "+1 Max HP" uses ApplyMaxHpBoost
                _runState.ApplyMaxHpBoost(piece.Id, option.Amount);
                break;

            case RewardType.StatBoost when option.Stat.HasValue:
                _runState.ApplyStatBoost(piece.Id, option.Stat.Value, option.Amount);
                break;

            case RewardType.NewAbility when option.Ability != null:
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
