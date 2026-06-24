using UnityEngine;
using UnityEngine.SceneManagement;
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

    [Tooltip("Button that returns to SampleScene (main menu).")]
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
        SceneManager.LoadScene("SampleScene");
    }
}
