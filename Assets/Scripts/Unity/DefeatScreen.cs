using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the run outcome (VICTORY or DEFEAT) on the GameOver scene.
/// Follows the <see cref="RewardScreen"/> pattern: finds <see cref="RunManager.Instance"/>
/// on OnEnable to read the outcome, and provides a button to return to the main menu.
/// </summary>
public class DefeatScreen : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text element that displays VICTORY or DEFEAT.")]
    public Text TitleText;

    [Tooltip("Button that starts a fresh run.")]
    public Button MainMenuButton;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        var mgr = RunManager.Instance;

        if (TitleText != null)
        {
            if (mgr != null)
                TitleText.text = mgr.LastRunWasVictory ? "VICTORY" : "DEFEAT";
            else
                TitleText.text = "GAME OVER";
        }

        if (MainMenuButton != null)
        {
            MainMenuButton.onClick.RemoveAllListeners();
            MainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnMainMenuClicked()
    {
        var mgr = RunManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("DefeatScreen: Cannot restart because RunManager.Instance is missing.");
            return;
        }

        mgr.RestartRun();
    }
}
