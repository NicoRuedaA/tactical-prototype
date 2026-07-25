using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum CombatFeedbackTone
{
    Info,
    Invalid,
    Cancelled,
}

public enum CombatHudInputOrigin
{
    Pointer,
    Submit,
}

/// <summary>
/// Serialized uGUI view for combat information. The scene owns the Canvas,
/// EventSystem, panels, and template; this component only renders state.
/// </summary>
public sealed class CombatHudView : MonoBehaviour
{
    [Header("Current turn")]
    public Text ActiveUnitText;
    public Text ResourcesText;
    public Text TurnOrderText;

    [Header("Rules and controls")]
    public Text ActionRuleText;
    public Text ControlsText;

    [Header("Feedback")]
    public GameObject FeedbackToast;
    public Image FeedbackBackground;
    public Text FeedbackText;
    [Min(0.1f)] public float FeedbackDuration = 1.75f;

    [Header("Actions")]
    [Tooltip("Optional root for the lower action panel. When unset, the parent of ActionRuleText is used.")]
    public GameObject ActionPanelRoot;
    public RectTransform AbilitiesContainer;
    public Button AbilityButtonTemplate;
    public Button PassButton;

    private readonly List<Button> _abilityButtons = new List<Button>();
    private Action<int, CombatHudInputOrigin> _abilityRequested;
    private Action<CombatHudInputOrigin> _passRequested;
    private ColorBlock _defaultAbilityColors;
    private Coroutine _feedbackHideRoutine;
    private bool _persistentFeedback;

    public bool IsConfigured => ActiveUnitText != null
                                && ResourcesText != null
                                && TurnOrderText != null
                                && ActionRuleText != null
                                && ControlsText != null
                                && FeedbackToast != null
                                && FeedbackBackground != null
                                && FeedbackText != null
                                && AbilitiesContainer != null
                                && AbilityButtonTemplate != null
                                && PassButton != null;

    public IReadOnlyList<Button> AbilityButtons => _abilityButtons;
    public bool IsSelectionVisible { get; private set; }
    public CombatFeedbackTone LastFeedbackTone { get; private set; }
    public string LastFeedbackMessage { get; private set; } = string.Empty;
    public bool HasPersistentFeedback => _persistentFeedback && FeedbackToast != null && FeedbackToast.activeSelf;

    public void Bind(
        Action<int, CombatHudInputOrigin> abilityRequested,
        Action<CombatHudInputOrigin> passRequested)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("CombatHudView is missing serialized UI references.");

        _abilityRequested = abilityRequested;
        _passRequested = passRequested;
        _defaultAbilityColors = AbilityButtonTemplate.colors;
        FeedbackBackground.raycastTarget = false;
        FeedbackText.raycastTarget = false;

        PassButton.onClick.RemoveAllListeners();
        ConfigureInputRelay(
            PassButton,
            origin => _passRequested?.Invoke(origin));
        AbilityButtonTemplate.gameObject.SetActive(false);
        ClearFeedback();
        SetSelectionVisible(false);
    }

    public void Render(CombatHudState state)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("CombatHudView is missing serialized UI references.");
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        SetSelectionVisible(state.HasSelection);

        ActiveUnitText.text = state.ActiveUnit;
        ResourcesText.text = state.Resources;
        TurnOrderText.text = state.TurnOrder;
        ActionRuleText.text = state.ActionRule;
        ControlsText.text = state.Controls;
        PassButton.interactable = state.CanPass;

        EnsureAbilityButtonCount(state.Abilities.Count);
        for (int i = 0; i < _abilityButtons.Count; i++)
        {
            Button button = _abilityButtons[i];
            bool visible = i < state.Abilities.Count;
            button.gameObject.SetActive(visible);
            if (!visible)
                continue;

            CombatHudAbilityState ability = state.Abilities[i];
            // Unavailable abilities stay clickable during the player's turn so the
            // presenter can explain why they are unavailable. Their visual state is
            // still deliberately muted.
            button.interactable = ability.CanAttempt;
            ColorBlock colors = _defaultAbilityColors;
            if (ability.CanAttempt && !ability.IsEnabled)
            {
                colors.normalColor = new Color(0.28f, 0.31f, 0.38f, 1f);
                colors.highlightedColor = new Color(0.38f, 0.42f, 0.5f, 1f);
                colors.pressedColor = new Color(0.22f, 0.24f, 0.3f, 1f);
            }
            button.colors = colors;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.text = ability.CanAttempt && !ability.IsEnabled
                    ? $"{ability.Label}  ({ability.UnavailableMessage})"
                    : ability.Label;

            button.onClick.RemoveAllListeners();
            int stableIndex = ability.Index;
            ConfigureInputRelay(
                button,
                origin => _abilityRequested?.Invoke(stableIndex, origin));
        }
    }

    /// <summary>Shows the lower action panel only while a piece is selected.</summary>
    public void SetSelectionVisible(bool visible)
    {
        IsSelectionVisible = visible;
        GameObject panel = ResolveActionPanelRoot();
        if (panel != null)
        {
            panel.SetActive(visible);
            return;
        }

        // Fallback for lightweight test fixtures without the scene's panel root.
        if (AbilitiesContainer != null)
            AbilitiesContainer.gameObject.SetActive(visible);
        if (PassButton != null)
            PassButton.gameObject.SetActive(visible);
    }

    private GameObject ResolveActionPanelRoot()
    {
        if (ActionPanelRoot != null)
            return ActionPanelRoot;
        if (ActionRuleText != null && ActionRuleText.transform.parent != null)
            return ActionRuleText.transform.parent.gameObject;
        if (AbilitiesContainer != null && AbilitiesContainer.parent != null)
            return AbilitiesContainer.parent.gameObject;
        return null;
    }

    public void ShowCombatResult(string message)
    {
        SetSelectionVisible(false);
        ClearFeedback();
        if (ActiveUnitText != null)
            ActiveUnitText.text = message;
        if (PassButton != null)
            PassButton.interactable = false;
        foreach (Button button in _abilityButtons)
            button.interactable = false;
    }

    public void ShowFeedback(string message, CombatFeedbackTone tone)
    {
        if (FeedbackToast == null || FeedbackBackground == null || FeedbackText == null)
            return;

        LastFeedbackTone = tone;
        LastFeedbackMessage = message ?? string.Empty;
        FeedbackText.text = LastFeedbackMessage;
        FeedbackBackground.color = tone switch
        {
            CombatFeedbackTone.Invalid => new Color(0.55f, 0.06f, 0.2f, 0.94f),
            CombatFeedbackTone.Cancelled => new Color(0.22f, 0.3f, 0.42f, 0.94f),
            _ => new Color(0.08f, 0.32f, 0.42f, 0.94f),
        };
        FeedbackToast.SetActive(true);
        RestartFeedbackTimer();
    }

    public void ShowBossPhaseFeedback(string message)
    {
        if (FeedbackToast == null || FeedbackBackground == null || FeedbackText == null)
            return;

        _persistentFeedback = true;
        LastFeedbackTone = CombatFeedbackTone.Info;
        LastFeedbackMessage = message ?? string.Empty;
        FeedbackText.text = LastFeedbackMessage;
        FeedbackBackground.color = new Color(0.45f, 0.12f, 0.5f, 0.96f);
        FeedbackToast.SetActive(true);
        RestartFeedbackTimer();
    }

    public void ConsumePersistentFeedback()
    {
        if (!_persistentFeedback)
            return;
        _persistentFeedback = false;
        ClearFeedback();
    }

    public void ClearFeedback()
    {
        _persistentFeedback = false;
        if (_feedbackHideRoutine != null)
        {
            StopCoroutine(_feedbackHideRoutine);
            _feedbackHideRoutine = null;
        }
        if (FeedbackText != null)
            FeedbackText.text = string.Empty;
        LastFeedbackMessage = string.Empty;
        if (FeedbackToast != null)
            FeedbackToast.SetActive(false);
    }

    public void ClearTransientFeedback()
    {
        if (_persistentFeedback)
            return;
        ClearFeedback();
    }

    private void EnsureAbilityButtonCount(int count)
    {
        while (_abilityButtons.Count < count)
        {
            Button button = Instantiate(AbilityButtonTemplate, AbilitiesContainer);
            button.name = $"Ability Button {_abilityButtons.Count + 1}";
            _abilityButtons.Add(button);
        }
    }

    private static void ConfigureInputRelay(
        Button button,
        Action<CombatHudInputOrigin> requested)
    {
        CombatHudInputRelay relay = button.GetComponent<CombatHudInputRelay>();
        if (relay == null)
            relay = button.gameObject.AddComponent<CombatHudInputRelay>();
        relay.Bind(requested);
    }

    private void RestartFeedbackTimer()
    {
        if (_feedbackHideRoutine != null)
            StopCoroutine(_feedbackHideRoutine);
        _feedbackHideRoutine = StartCoroutine(HideFeedbackAfterDelay());
    }

    private IEnumerator HideFeedbackAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, FeedbackDuration));
        _feedbackHideRoutine = null;
        _persistentFeedback = false;
        if (FeedbackText != null)
            FeedbackText.text = string.Empty;
        if (FeedbackToast != null)
            FeedbackToast.SetActive(false);
    }
}

internal sealed class CombatHudInputRelay : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private Action<CombatHudInputOrigin> _requested;

    public void Bind(Action<CombatHudInputOrigin> requested)
    {
        _requested = requested;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            _requested?.Invoke(CombatHudInputOrigin.Pointer);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        _requested?.Invoke(CombatHudInputOrigin.Submit);
    }
}
