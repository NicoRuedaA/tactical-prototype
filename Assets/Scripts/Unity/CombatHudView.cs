using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Game.Core;

public enum CombatFeedbackTone { Info, Invalid, Cancelled }
public enum CombatHudInputOrigin { Pointer, Submit }

public sealed class CombatHudView : MonoBehaviour
{
    public UIDocument Document;
    public PanelSettings PanelSettings;
    [Min(0.1f)] public float FeedbackDuration = 1.75f;

    private Label _active, _resources, _turnOrder, _rule, _controls, _feedback;
    private VisualElement _feedbackBox, _actions, _abilities;
    private VisualElement _inspector;
    private Button _pass;
    private Action<int, CombatHudInputOrigin> _abilityRequested;
    private Action<CombatHudInputOrigin> _passRequested;
    private Coroutine _feedbackRoutine;
    private bool _persistentFeedback;

    public bool IsConfigured => Document != null;
    public bool IsSelectionVisible { get; private set; }
    public CombatFeedbackTone LastFeedbackTone { get; private set; }
    public string LastFeedbackMessage { get; private set; } = string.Empty;
    public bool HasPersistentFeedback => _persistentFeedback && _feedbackBox != null && _feedbackBox.style.display == DisplayStyle.Flex;

    private void Awake() { BuildUi(); }

    public void Bind(Action<int, CombatHudInputOrigin> abilityRequested, Action<CombatHudInputOrigin> passRequested)
    {
        EnsureConfigured();
        _abilityRequested = abilityRequested;
        _passRequested = passRequested;
        _pass.clicked += () => _passRequested?.Invoke(CombatHudInputOrigin.Pointer);
        ClearFeedback();
        SetSelectionVisible(false);
    }

    public void Render(CombatHudState state)
    {
        EnsureConfigured();
        if (state == null) throw new ArgumentNullException(nameof(state));
        _active.text = state.ActiveUnit;
        _resources.text = state.Resources;
        _turnOrder.text = state.TurnOrder;
        _rule.text = state.ActionRule;
        _controls.text = state.Controls;
        _pass.SetEnabled(state.CanPass);
        _abilities.Clear();
        foreach (var ability in state.Abilities)
        {
            var index = ability.Index;
            var button = new Button(() => _abilityRequested?.Invoke(index, CombatHudInputOrigin.Pointer))
            { text = ability.CanAttempt && !ability.IsEnabled ? $"{ability.Label} ({ability.UnavailableMessage})" : ability.Label };
            button.SetEnabled(ability.CanAttempt);
            button.style.height = 34;
            button.style.marginBottom = 5;
            button.style.color = ability.CanAttempt
                ? new Color(0.85f, 0.95f, 1f, 1f)
                : new Color(0.45f, 0.5f, 0.58f, 1f);
            button.style.backgroundColor = ability.CanAttempt
                ? new Color(0.08f, 0.3f, 0.42f, 1f)
                : new Color(0.1f, 0.12f, 0.16f, 1f);
            _abilities.Add(button);
        }
        SetSelectionVisible(state.HasSelection);
    }

    public void SetSelectionVisible(bool visible)
    {
        IsSelectionVisible = visible;
        if (_actions != null) _actions.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void ShowPieceInspector(Piece piece)
    {
        if (piece == null || _inspector == null)
            return;

        _inspector.Clear();
        _inspector.style.display = DisplayStyle.Flex;
        var heading = AddLabel(_inspector, piece.Name.ToUpperInvariant());
        heading.style.fontSize = 16;
        heading.style.unityFontStyleAndWeight = FontStyle.Bold;
        heading.style.color = piece.Team == Game.Core.Team.Player
            ? new Color(0.35f, 0.75f, 1f, 1f)
            : new Color(1f, 0.42f, 0.42f, 1f);
        AddLabel(_inspector, $"{piece.Team}  |  {(piece.IsQueen ? "Queen" : "Unit")}");
        AddLabel(_inspector, $"HP {piece.Hp}/{piece.EffectiveMaxHp}  |  Mana {piece.Mana}/{piece.MaxMana}");
        AddLabel(_inspector, $"Damage {piece.EffectiveDamage}  |  Attack {piece.EffectiveAttackRange}  |  Move {piece.EffectiveMoveRange}");
        AddLabel(_inspector, $"Position {piece.Coords}");
    }

    public void HidePieceInspector()
    {
        if (_inspector == null)
            return;
        _inspector.Clear();
        _inspector.style.display = DisplayStyle.None;
    }

    public void ShowCombatResult(string message)
    {
        SetSelectionVisible(false);
        ClearFeedback();
        _active.text = message;
        _pass.SetEnabled(false);
        foreach (var button in _abilities.Query<Button>().ToList()) button.SetEnabled(false);
    }

    public void ShowFeedback(string message, CombatFeedbackTone tone)
    {
        LastFeedbackTone = tone;
        LastFeedbackMessage = message ?? string.Empty;
        _feedback.text = LastFeedbackMessage;
        _feedbackBox.style.display = DisplayStyle.Flex;
        RestartFeedbackTimer();
    }

    public void ShowBossPhaseFeedback(string message) { _persistentFeedback = true; ShowFeedback(message, CombatFeedbackTone.Info); }
    public void ConsumePersistentFeedback() { if (_persistentFeedback) { _persistentFeedback = false; ClearFeedback(); } }

    public void ClearTransientFeedback() { if (!_persistentFeedback) ClearFeedback(); }
    public void ClearFeedback()
    {
        _persistentFeedback = false;
        if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
        _feedbackRoutine = null;
        if (_feedback != null) _feedback.text = string.Empty;
        if (_feedbackBox != null) _feedbackBox.style.display = DisplayStyle.None;
        LastFeedbackMessage = string.Empty;
    }

    private void BuildUi()
    {
        Document = Document != null ? Document : GetComponent<UIDocument>();
        if (Document == null) Document = gameObject.AddComponent<UIDocument>();
        if (Document.panelSettings == null)
            Document.panelSettings = PanelSettings != null
                ? PanelSettings
                : ScriptableObject.CreateInstance<PanelSettings>();
        Document.sortingOrder = 100;
        var root = Document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.paddingLeft = 20;
        root.style.paddingTop = 20;

        var panel = new VisualElement { name = "CombatHudPanel" };
        panel.style.position = Position.Absolute;
        panel.style.left = 20;
        panel.style.top = 20;
        panel.style.width = 390;
        panel.style.backgroundColor = new Color(0.025f, 0.035f, 0.06f, 0.96f);
        panel.style.borderTopLeftRadius = 10;
        panel.style.borderTopRightRadius = 10;
        panel.style.borderBottomLeftRadius = 10;
        panel.style.borderBottomRightRadius = 10;
        panel.style.borderLeftWidth = 1;
        panel.style.borderRightWidth = 1;
        panel.style.borderTopWidth = 1;
        panel.style.borderBottomWidth = 1;
        panel.style.borderLeftColor = new Color(0.25f, 0.42f, 0.62f, 1f);
        panel.style.borderRightColor = panel.style.borderLeftColor;
        panel.style.borderTopColor = panel.style.borderLeftColor;
        panel.style.borderBottomColor = panel.style.borderLeftColor;
        panel.style.paddingLeft = 16;
        panel.style.paddingRight = 16;
        panel.style.paddingTop = 14;
        panel.style.paddingBottom = 14;
        root.Add(panel);

        var title = AddLabel(panel, "TACTICAL COMMAND");
        title.style.color = new Color(0.35f, 0.78f, 1f, 1f);
        title.style.fontSize = 13;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.letterSpacing = 1;
        title.style.marginBottom = 8;

        var activeCard = new VisualElement { name = "ActiveUnitCard" };
        activeCard.style.backgroundColor = new Color(0.08f, 0.12f, 0.19f, 1f);
        activeCard.style.paddingLeft = 12;
        activeCard.style.paddingRight = 12;
        activeCard.style.paddingTop = 8;
        activeCard.style.paddingBottom = 8;
        activeCard.style.borderLeftWidth = 3;
        activeCard.style.borderLeftColor = new Color(0.2f, 0.75f, 1f, 1f);
        panel.Add(activeCard);

        _active = AddLabel(activeCard, "Active: -");
        _resources = AddLabel(activeCard, "HP - | Mana -");
        _turnOrder = AddLabel(panel, "Turn order: -");
        _rule = AddLabel(panel, CombatHudPresenter.ActionRule);
        _controls = AddLabel(panel, CombatHudPresenter.Controls);
        _feedbackBox = new VisualElement { name = "Feedback" };
        _feedback = AddLabel(_feedbackBox, string.Empty);
        panel.Add(_feedbackBox);
        _inspector = new VisualElement { name = "PieceInspector" };
        _inspector.style.marginTop = 10;
        _inspector.style.paddingLeft = 10;
        _inspector.style.paddingRight = 10;
        _inspector.style.paddingTop = 8;
        _inspector.style.paddingBottom = 4;
        _inspector.style.backgroundColor = new Color(0.1f, 0.12f, 0.18f, 1f);
        _inspector.style.borderLeftWidth = 3;
        _inspector.style.borderLeftColor = new Color(0.35f, 0.75f, 1f, 1f);
        _inspector.style.display = DisplayStyle.None;
        panel.Add(_inspector);
        _actions = new VisualElement { name = "Actions" };
        _abilities = new VisualElement { name = "Abilities" };
        _actions.Add(_abilities);
        panel.Add(_actions);
        _pass = new Button { text = "PASS  [SPACE]" };
        _actions.Add(_pass);
        _feedbackBox.style.display = DisplayStyle.None;

        foreach (var label in panel.Query<Label>().ToList())
        {
            if (label != title)
            {
                label.style.color = new Color(0.82f, 0.88f, 0.96f, 1f);
                label.style.fontSize = 13;
                label.style.marginBottom = 5;
            }
        }
        _active.style.color = Color.white;
        _active.style.fontSize = 18;
        _active.style.unityFontStyleAndWeight = FontStyle.Bold;
        _resources.style.color = new Color(0.35f, 1f, 0.45f, 1f);
        _resources.style.fontSize = 14;
        _turnOrder.style.marginTop = 10;
        _rule.style.color = new Color(1f, 0.75f, 0.35f, 1f);
        _rule.style.unityFontStyleAndWeight = FontStyle.Bold;
        _controls.style.color = new Color(0.58f, 0.66f, 0.78f, 1f);
        _controls.style.fontSize = 11;
        _actions.style.marginTop = 10;
        _pass.style.height = 38;
        _pass.style.marginTop = 8;
        _pass.style.backgroundColor = new Color(0.14f, 0.2f, 0.3f, 1f);
        _pass.style.color = Color.white;
    }
    private static Label AddLabel(VisualElement root, string text) { var label = new Label(text); root.Add(label); return label; }
    private void EnsureConfigured() { if (!IsConfigured) throw new InvalidOperationException("CombatHudView requires a UIDocument."); }
    private void RestartFeedbackTimer() { if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine); _feedbackRoutine = StartCoroutine(HideFeedback()); }
    private IEnumerator HideFeedback() { yield return new WaitForSecondsRealtime(FeedbackDuration); if (!_persistentFeedback) ClearFeedback(); _feedbackRoutine = null; }
}
