using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controls the Main Menu scene built with UI Toolkit.
/// Handles the Play button to start a new run.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public sealed class MainMenuController : MonoBehaviour
{
    private UIDocument _uiDocument;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;
        if (root == null) return;

        var playButton = root.Q<Button>("PlayButton");
        if (playButton != null)
            playButton.clicked += OnPlayClicked;
    }

    private void OnDisable()
    {
        if (_uiDocument == null) return;

        var root = _uiDocument.rootVisualElement;
        if (root == null) return;

        var playButton = root.Q<Button>("PlayButton");
        if (playButton != null)
            playButton.clicked -= OnPlayClicked;
    }

    private void OnPlayClicked()
    {
        var mgr = RunManager.Instance;
        if (mgr != null)
        {
            Debug.Log("MainMenu: Starting new run...");
            mgr.StartNewRun();
        }
        else
        {
            Debug.LogError("MainMenu: RunManager.Instance is missing! " +
                           "Ensure RunManager is in the scene.");
        }
    }
}
