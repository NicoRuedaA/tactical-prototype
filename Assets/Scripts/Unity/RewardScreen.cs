using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Game.Core;

public sealed class RewardScreen : MonoBehaviour
{
    public UIDocument Document;
    public RewardPoolData NormalRewardPool;
    public RewardPoolData EliteRewardPool;
    public RewardPoolData BossRewardPool;

    private UIDocument _document;
    private Label _title;
    private VisualElement _content;
    private RewardOption[] _currentOptions;
    private RunState _runState;
    private RewardPoolData _rewardPool;
    private int _rewardOptionsSeed;
    private int _rewardRecipientSeed;
    private Piece _selectedRecipient;
    private bool _isSelectingRecipient;

    private void OnEnable()
    {
        BuildUi();
        var mgr = RunManager.Instance;
        if (mgr == null || mgr.CurrentRun == null)
        {
            Debug.LogError("RewardScreen: No active RunState found!");
            return;
        }
        _runState = mgr.CurrentRun;
        _rewardPool = SelectRewardPool(mgr.CurrentNodeType, NormalRewardPool, EliteRewardPool, BossRewardPool);
        _rewardOptionsSeed = mgr.GetStreamSeed(RunRandomStream.RewardOptions);
        _rewardRecipientSeed = mgr.GetStreamSeed(RunRandomStream.RewardRecipient);
        _currentOptions = System.Array.Empty<RewardOption>();
        var recipients = GetDeterministicAliveRecipients(_runState);
        if (recipients.Count > 0)
            ShowRecipients(recipients);
        else
            SelectRecipientAndDisplayOptions(SelectRewardRecipient(_runState, _rewardRecipientSeed));
    }

    private void BuildUi()
    {
        _document = Document != null ? Document : GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
        Document = _document;
        _document.panelSettings ??= ScriptableObject.CreateInstance<PanelSettings>();
        var root = _document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.paddingLeft = 48;
        root.style.paddingRight = 48;
        root.style.paddingTop = 36;
        _title = new Label { name = "Title" };
        _title.style.fontSize = 32;
        root.Add(_title);
        _content = new VisualElement { name = "Content" };
        _content.style.marginTop = 24;
        root.Add(_content);
    }

    private void ShowRecipients(IReadOnlyList<Piece> recipients)
    {
        _isSelectingRecipient = true;
        _title.text = "CHOOSE A UNIT";
        _content.Clear();
        foreach (var piece in recipients)
        {
            var capturedId = piece.Id;
            _content.Add(new Button(() => OnRecipientClicked(capturedId))
            {
                text = FormatRecipientLabel(piece),
                name = "Recipient-" + piece.Id
            });
        }
    }

    private void DisplayOptions()
    {
        _isSelectingRecipient = false;
        _title.text = $"CHOOSE A REWARD FOR {_selectedRecipient.Name}";
        _content.Clear();
        for (var i = 0; i < _currentOptions.Length; i++)
        {
            var index = i;
            var option = _currentOptions[i];
            _content.Add(new Button(() => OnCardClicked(index))
            {
                text = $"{GetIcon(option)} {FormatRewardPreview(_selectedRecipient, option)}",
                name = "RewardCard-" + i
            });
        }
    }

    private void OnRecipientClicked(string pieceId)
    {
        if (!_isSelectingRecipient || _runState == null) return;
        var piece = _runState.Pieces.FirstOrDefault(p => p != null && p.Id == pieceId && !p.IsDead);
        if (piece == null) return;
        SelectRecipientAndDisplayOptions(piece);
    }

    private void OnCardClicked(int index)
    {
        if (_currentOptions == null || index < 0 || index >= _currentOptions.Length || _selectedRecipient == null || _selectedRecipient.IsDead) return;
        ApplyReward(_selectedRecipient, _currentOptions[index]);
        RunManager.Instance?.OnRewardApplied();
    }

    private void SelectRecipientAndDisplayOptions(Piece piece)
    {
        if (piece == null) { Debug.LogError("RewardScreen: No pieces available to receive a reward!"); return; }
        _selectedRecipient = piece;
        _currentOptions = GenerateRewardOptions(_rewardOptionsSeed, _rewardPool, piece);
        if (_currentOptions.Length == 0) { RunManager.Instance?.OnRewardApplied(); return; }
        DisplayOptions();
    }

    public static RewardOption[] GenerateRewardOptions(int seed) => GenerateFallbackOptions(seed);
    public static RewardOption[] GenerateRewardOptions(int seed, RewardPoolData pool) => GenerateRewardOptions(seed, pool, null);
    public static RewardOption[] GenerateRewardOptions(int seed, RewardPoolData pool, Piece recipient) => pool != null && pool.HasAuthoredDefinitions ? pool.PickOptions(seed, recipient) : GenerateFallbackOptions(seed);
    public static RewardPoolData SelectRewardPool(MapNodeType type, RewardPoolData normal, RewardPoolData elite, RewardPoolData boss) => type == MapNodeType.Elite ? elite : type == MapNodeType.Boss ? boss : normal;

    private static RewardOption[] GenerateFallbackOptions(int seed)
    {
        var rng = new DeterministicRandom(seed);
        var indices = rng.PickDistinctIndices(6, 3);
        var options = new[]
        {
            new RewardOption("+1 Damage", RewardEffectKind.StatBoost, StatType.Damage, 1),
            new RewardOption("+1 Max HP", RewardEffectKind.MaxHpBoost, null, 1),
            new RewardOption("+1 Move Range", RewardEffectKind.StatBoost, StatType.MoveRange, 1),
            new RewardOption("+1 Attack Range", RewardEffectKind.StatBoost, StatType.AttackRange, 1),
            new RewardOption("Learn: Fireball", RewardEffectKind.NewAbility, null, 0, new InlineAbility("Fireball")),
            new RewardOption("Learn: Heal", RewardEffectKind.NewAbility, null, 0, new InlineAbility("Heal"))
        };
        return indices.Select(i => options[i]).ToArray();
    }

    public static IReadOnlyList<Piece> GetDeterministicAliveRecipients(RunState state) => state == null ? new List<Piece>() : state.GetAlivePlayerPieces().ToList();
    public static Piece SelectRewardRecipient(RunState state, int seed)
    {
        var pieces = GetDeterministicAliveRecipients(state).ToList();
        if (pieces.Count == 0 && state != null) pieces = state.Pieces.ToList();
        return pieces.Count == 0 ? null : pieces[new DeterministicRandom(seed).Next(pieces.Count)];
    }
    public static string FormatRecipientLabel(Piece piece) => piece == null ? string.Empty : $"{piece.Name}    HP {piece.Hp}/{piece.EffectiveMaxHp}";
    public static string FormatRecipientLabel(Piece piece, RewardOption option) => piece == null ? string.Empty : $"{piece.Name}    HP {piece.Hp}/{piece.EffectiveMaxHp} — {FormatRewardPreview(piece, option)}";
    public static string FormatRewardPreview(Piece piece, RewardOption option)
    {
        if (piece == null) return string.Empty;
        if (option.Effect == RewardEffectKind.MaxHpBoost)
            return $"{option.Description}: HP {piece.Hp}/{piece.EffectiveMaxHp}→{piece.Hp + option.Amount}/{piece.EffectiveMaxHp + option.Amount}";
        if (option.Effect == RewardEffectKind.NewAbility)
            return $"{option.Description}: learn ability {(option.Ability == null ? "ability" : option.Ability.DisplayName)}";
        var before = GetEffectiveStat(piece, option.Stat ?? StatType.Damage);
        return $"{option.Description}: {option.Stat} {before}→{before + option.Amount}";
    }
    private static int GetEffectiveStat(Piece p, StatType s) => s == StatType.Damage ? p.EffectiveDamage : s == StatType.AttackRange ? p.EffectiveAttackRange : p.EffectiveMoveRange;
    private static string GetIcon(RewardOption option) => option.Effect == RewardEffectKind.MaxHpBoost ? "♥" : option.Effect == RewardEffectKind.NewAbility ? "★" : "⚔";
    private void ApplyReward(Piece piece, RewardOption option) => _runState.ApplyReward(piece.Id, option);
}

internal class InlineAbility : IAbilityData
{
    public string DisplayName { get; } public AbilityType AbilityType => AbilityType.Active; public int ManaCost => 2; public int ActiveRange => 2; public PassiveTrigger Trigger => PassiveTrigger.OnHit; public EffectType EffectType => EffectType.Damage; public int EffectValue => 3; public StatType StatToModify => StatType.Damage; public int AreaRadius => 0; public AffectsTeam AffectsTeam => AffectsTeam.Enemies; public DurationType DurationType => DurationType.FixedTurns; public int DurationTurns => 1;
    public InlineAbility(string name) { DisplayName = name; }
}
