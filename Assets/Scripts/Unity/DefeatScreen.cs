using UnityEngine;
using UnityEngine.UIElements;

public sealed class DefeatScreen : MonoBehaviour
{
    public UIDocument Document;
    private UIDocument _document;
    private Label _title;

    private void OnEnable()
    {
        BuildUi();
        var mgr = RunManager.Instance;
        _title.text = mgr == null ? "GAME OVER" : (mgr.LastRunWasVictory ? "VICTORY" : "DEFEAT");
    }

    private void BuildUi()
    {
        _document = Document != null ? Document : GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
        Document = _document;
        _document.panelSettings ??= ScriptableObject.CreateInstance<PanelSettings>();
        var root = _document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        _title = new Label { name = "Title", text = "GAME OVER" };
        _title.style.fontSize = 48;
        _title.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(_title);

        var mainMenu = new Button(OnMainMenuClicked) { name = "MainMenuButton", text = "MAIN MENU" };
        mainMenu.style.marginTop = 24;
        root.Add(mainMenu);
    }

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
