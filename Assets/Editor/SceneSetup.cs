using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Batch-mode scene setup tool. Creates Reward.unity and registers all scenes in Build Settings.
/// Run via: Unity -batchMode -executeMethod SceneSetup.CreateRewardScene
/// </summary>
public static class SceneSetup
{
    private const string CombatScenePath = "Assets/Scenes/Combat.unity";
    private const string RewardScenePath = "Assets/Scenes/Reward.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    public static void CreateRewardScene()
    {
        // Check if Reward scene already exists
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RewardScenePath) != null)
        {
            Debug.Log("Reward scene already exists. Updating...");
            EditorSceneManager.OpenScene(RewardScenePath, OpenSceneMode.Single);
        }
        else
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Reward";
        }

        // Find or create root objects
        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        // Ensure Camera exists
        bool hasCamera = false;
        bool hasEventSystem = false;
        foreach (var go in rootObjects)
        {
            if (go.GetComponent<Camera>() != null) hasCamera = true;
            if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) hasEventSystem = true;
        }

        Camera cam;
        if (!hasCamera)
        {
            var camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        }

        // Ensure EventSystem
        if (!hasEventSystem)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Create Canvas
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create RewardScreen GameObject
        var rewardGO = new GameObject("RewardScreen");
        rewardGO.transform.SetParent(canvasGO.transform, false);
        var rewardScreen = rewardGO.AddComponent<RewardScreen>();

        // Title text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var titleText = titleGO.AddComponent<Text>();
        titleText.text = "CHOOSE A REWARD";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 48;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.85f);
        titleRT.anchorMax = new Vector2(1, 0.95f);
        titleRT.pivot = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = Vector2.zero;
        rewardScreen.TitleText = titleText;

        // Create 3 card buttons
        for (int i = 0; i < 3; i++)
        {
            var cardGO = new GameObject($"Card{i}");
            cardGO.transform.SetParent(canvasGO.transform, false);
            var cardRT = cardGO.AddComponent<RectTransform>();
            float xPos = (i - 1) * 220f; // -220, 0, 220
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(180, 240);
            cardRT.anchoredPosition = new Vector2(xPos, -20);

            // Card background (Image for visual)
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(cardGO.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.3f);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // Card text
            var textGO = new GameObject("CardText");
            textGO.transform.SetParent(cardGO.transform, false);
            var cardText = textGO.AddComponent<Text>();
            cardText.text = "Reward";
            cardText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cardText.fontSize = 24;
            cardText.alignment = TextAnchor.MiddleCenter;
            cardText.color = Color.white;
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            // Button component
            var button = cardGO.AddComponent<Button>();
            button.targetGraphic = bgImg;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.5f);
            button.colors = colors;

            // Assign to RewardScreen
            switch (i)
            {
                case 0:
                    rewardScreen.CardButton0 = button;
                    rewardScreen.CardText0 = cardText;
                    break;
                case 1:
                    rewardScreen.CardButton1 = button;
                    rewardScreen.CardText1 = cardText;
                    break;
                case 2:
                    rewardScreen.CardButton2 = button;
                    rewardScreen.CardText2 = cardText;
                    break;
            }
        }

        // Save scene
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), RewardScenePath);
        Debug.Log($"Reward scene created at: {RewardScenePath}");

        // Update Build Settings
        UpdateBuildSettings();
    }

    private static void UpdateBuildSettings()
    {
        var buildScenes = EditorBuildSettings.scenes;

        // Check if scenes are already registered
        bool hasSample = false;
        bool hasCombat = false;
        bool hasReward = false;

        foreach (var s in buildScenes)
        {
            if (s.path == SampleScenePath) hasSample = true;
            if (s.path == CombatScenePath) hasCombat = true;
            if (s.path == RewardScenePath) hasReward = true;
        }

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // SampleScene must be index 0
        scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
        // Combat at index 1
        if (hasCombat || AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatScenePath) != null)
            scenes.Add(new EditorBuildSettingsScene(CombatScenePath, true));
        // Reward at index 2
        scenes.Add(new EditorBuildSettingsScene(RewardScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Build Settings updated: {scenes.Count} scenes registered.");
    }
}
