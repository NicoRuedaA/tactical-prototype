using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Batch-mode scene setup tool. Creates Reward.unity and Map.unity, and registers all
/// scenes in Build Settings. Run via:
///   Unity -batchMode -executeMethod SceneSetup.CreateRewardScene
///   Unity -batchMode -executeMethod SceneSetup.CreateMapScene
///
/// Build scene order: SampleScene(0), Combat(1), Reward(2), Map(3), GameOver(4).
/// </summary>
public static class SceneSetup
{
    [MenuItem("Tools/TacticalRogue/Create GameOver Scene")]
    public static void CreateGameOverSceneMenu()
    {
        CreateGameOverScene();
    }
    private const string CombatScenePath = "Assets/Scenes/Combat.unity";
    private const string RewardScenePath = "Assets/Scenes/Reward.unity";
    private const string MapScenePath = "Assets/Scenes/Map.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string GameOverScenePath = "Assets/Scenes/GameOver.unity";

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
            esGO.AddComponent<InputSystemUIInputModule>();
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

    public static void CreateMapScene()
    {
        // Check if Map scene already exists
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath) != null)
        {
            Debug.Log("Map scene already exists. Updating...");
            EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
        }
        else
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Map";
        }

        var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        // Ensure Camera exists with orthographic settings
        Camera cam = null;
        foreach (var go in rootObjects)
        {
            cam = go.GetComponent<Camera>();
            if (cam != null) break;
        }

        if (cam == null)
        {
            var camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);

        // Ensure EventSystem exists
        bool hasEventSystem = false;
        foreach (var go in rootObjects)
        {
            if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
            {
                hasEventSystem = true;
                break;
            }
        }

        if (!hasEventSystem)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }

        // Create Canvas with ScrollRect
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create Scroll View (root of the scrollable area)
        var scrollGO = new GameObject("Scroll View");
        scrollGO.transform.SetParent(canvasGO.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.sizeDelta = Vector2.zero;
        scrollRT.anchoredPosition = Vector2.zero;
        scrollRT.pivot = new Vector2(0.5f, 0.5f);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.135f;
        scrollRect.scrollSensitivity = 20f;
        scrollRect.elasticity = 0.1f;

        var scrollImage = scrollGO.AddComponent<Image>();
        scrollImage.color = new Color(0.1f, 0.1f, 0.12f, 1f);

        // Create Viewport (masks the content)
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportRT.pivot = new Vector2(0.5f, 0.5f);
        viewportGO.AddComponent<CanvasRenderer>();

        var mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.color = Color.white;
        viewportImage.raycastTarget = false;

        // Create Content (parent for node buttons)
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.sizeDelta = new Vector2(0, 0);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;

        var layoutGroup = contentGO.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childScaleWidth = false;
        layoutGroup.childScaleHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 20f;
        layoutGroup.padding = new RectOffset(50, 50, 50, 50);

        var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire up ScrollRect references
        scrollRect.viewport = viewportRT;
        scrollRect.content = contentRT;

        // Create MapView root GameObject
        var mapViewGO = new GameObject("MapView");
        mapViewGO.transform.SetParent(null);
        mapViewGO.transform.position = Vector3.zero;

        var mapView = mapViewGO.AddComponent<MapView>();
        mapView.ContentContainer = contentRT;
        // Note: NodeButtonPrefab and LineRendererPrefab must be assigned in the Inspector

        // Save scene
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), MapScenePath);
        Debug.Log($"Map scene created at: {MapScenePath}");

        // Update Build Settings
        UpdateBuildSettings();
    }

    public static void CreateGameOverScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameOverScenePath) != null)
        {
            Debug.Log("GameOver scene already exists. Updating...");
            EditorSceneManager.OpenScene(GameOverScenePath, OpenSceneMode.Single);
        }
        else
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "GameOver";
        }

        ConfigureGameOverScene(SceneManager.GetActiveScene());

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), GameOverScenePath);
        Debug.Log($"GameOver scene created at: {GameOverScenePath}");

        UpdateBuildSettings();
    }

    /// <summary>
    /// Ensures the supplied scene has exactly one canonical GameOver UI hierarchy.
    /// Safe to call repeatedly; existing serialized references are reused.
    /// </summary>
    public static void ConfigureGameOverScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            throw new System.ArgumentException("GameOver scene must be valid and loaded.", nameof(scene));

        EnsureGameOverCamera(scene);
        EnsureGameOverEventSystem(scene);

        var screens = FindInScene<DefeatScreen>(scene);
        DefeatScreen defeatScreen = screens.Count > 0 ? screens[0] : null;
        Canvas primaryCanvas = defeatScreen != null ? defeatScreen.GetComponentInParent<Canvas>() : null;

        if (primaryCanvas == null)
        {
            var canvases = FindInScene<Canvas>(scene);
            primaryCanvas = canvases.Count > 0 ? canvases[0] : CreateCanvas(scene);
        }

        if (defeatScreen == null)
        {
            var defeatGO = new GameObject("DefeatScreen");
            defeatGO.transform.SetParent(primaryCanvas.transform, false);
            defeatScreen = defeatGO.AddComponent<DefeatScreen>();
        }
        else if (defeatScreen.transform.parent != primaryCanvas.transform)
        {
            defeatScreen.transform.SetParent(primaryCanvas.transform, false);
        }

        for (int i = 1; i < screens.Count; i++)
        {
            var duplicateCanvas = screens[i].GetComponentInParent<Canvas>();
            if (duplicateCanvas != null && duplicateCanvas != primaryCanvas)
                Object.DestroyImmediate(duplicateCanvas.gameObject);
            else if (screens[i] != null)
                Object.DestroyImmediate(screens[i].gameObject);
        }

        foreach (var canvas in FindInScene<Canvas>(scene))
        {
            if (canvas != primaryCanvas)
                Object.DestroyImmediate(canvas.gameObject);
        }

        primaryCanvas.name = "Canvas";
        primaryCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        if (primaryCanvas.GetComponent<CanvasScaler>() == null)
            primaryCanvas.gameObject.AddComponent<CanvasScaler>();
        if (primaryCanvas.GetComponent<GraphicRaycaster>() == null)
            primaryCanvas.gameObject.AddComponent<GraphicRaycaster>();

        defeatScreen.TitleText = EnsureGameOverTitle(primaryCanvas.transform, defeatScreen.TitleText);
        defeatScreen.MainMenuButton = EnsureRestartButton(primaryCanvas.transform, defeatScreen.MainMenuButton);
    }

    private static void EnsureGameOverCamera(Scene scene)
    {
        if (FindInScene<Camera>(scene).Count > 0)
            return;

        var cameraGO = CreateRoot(scene, "Main Camera");
        var camera = cameraGO.AddComponent<Camera>();
        cameraGO.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
    }

    private static void EnsureGameOverEventSystem(Scene scene)
    {
        var eventSystems = FindInScene<EventSystem>(scene);
        EventSystem eventSystem;

        if (eventSystems.Count == 0)
        {
            var eventGO = CreateRoot(scene, "EventSystem");
            eventSystem = eventGO.AddComponent<EventSystem>();
        }
        else
        {
            eventSystem = eventSystems[0];
            for (int i = 1; i < eventSystems.Count; i++)
                Object.DestroyImmediate(eventSystems[i].gameObject);
        }

        var inputModules = eventSystem.GetComponents<BaseInputModule>();
        InputSystemUIInputModule inputSystemModule = null;
        foreach (var module in inputModules)
        {
            if (module is InputSystemUIInputModule current && inputSystemModule == null)
                inputSystemModule = current;
            else
                Object.DestroyImmediate(module);
        }

        if (inputSystemModule == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static Canvas CreateCanvas(Scene scene)
    {
        var canvasGO = CreateRoot(scene, "Canvas");
        return canvasGO.AddComponent<Canvas>();
    }

    private static Text EnsureGameOverTitle(Transform parent, Text title)
    {
        if (title == null)
        {
            var titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleGO.transform.SetParent(parent, false);
            title = titleGO.GetComponent<Text>();
        }

        title.name = "TitleText";
        title.text = "GAME OVER";
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.fontSize = 64;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;

        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0, 0.6f);
        titleRT.anchorMax = new Vector2(1, 0.8f);
        titleRT.pivot = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = Vector2.zero;
        return title;
    }

    private static Button EnsureRestartButton(Transform parent, Button button)
    {
        if (button == null)
        {
            var buttonGO = new GameObject("MainMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(parent, false);
            button = buttonGO.GetComponent<Button>();
        }

        button.name = "MainMenuButton";
        var buttonRT = button.GetComponent<RectTransform>();
        buttonRT.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRT.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRT.pivot = new Vector2(0.5f, 0.5f);
        buttonRT.sizeDelta = new Vector2(200, 50);
        buttonRT.anchoredPosition = new Vector2(0, -50);

        var image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
        image.color = new Color(0.3f, 0.3f, 0.5f);
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(0.5f, 0.5f, 0.7f);
        button.colors = colors;

        var label = button.GetComponentInChildren<Text>();
        if (label == null)
        {
            var labelGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelGO.transform.SetParent(button.transform, false);
            label = labelGO.GetComponent<Text>();
        }

        label.text = "Play Again";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 28;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.sizeDelta = Vector2.zero;
        return button;
    }

    private static GameObject CreateRoot(Scene scene, string name)
    {
        var gameObject = new GameObject(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        return gameObject;
    }

    private static System.Collections.Generic.List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var results = new System.Collections.Generic.List<T>();
        foreach (var root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results;
    }

    private static void UpdateBuildSettings()
    {
        var buildScenes = EditorBuildSettings.scenes;

        // Check if scenes are already registered
        bool hasSample = false;
        bool hasCombat = false;
        bool hasReward = false;
        bool hasMap = false;
        bool hasGameOver = false;

        foreach (var s in buildScenes)
        {
            if (s.path == SampleScenePath) hasSample = true;
            if (s.path == CombatScenePath) hasCombat = true;
            if (s.path == RewardScenePath) hasReward = true;
            if (s.path == MapScenePath) hasMap = true;
            if (s.path == GameOverScenePath) hasGameOver = true;
        }

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // SampleScene must be index 0
        scenes.Add(new EditorBuildSettingsScene(SampleScenePath, true));
        // Combat at index 1
        if (hasCombat || AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatScenePath) != null)
            scenes.Add(new EditorBuildSettingsScene(CombatScenePath, true));
        // Reward at index 2
        if (hasReward || AssetDatabase.LoadAssetAtPath<SceneAsset>(RewardScenePath) != null)
            scenes.Add(new EditorBuildSettingsScene(RewardScenePath, true));
        // Map at index 3
        if (hasMap || AssetDatabase.LoadAssetAtPath<SceneAsset>(MapScenePath) != null)
            scenes.Add(new EditorBuildSettingsScene(MapScenePath, true));
        // GameOver at index 4
        if (hasGameOver || AssetDatabase.LoadAssetAtPath<SceneAsset>(GameOverScenePath) != null)
            scenes.Add(new EditorBuildSettingsScene(GameOverScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Build Settings updated: {scenes.Count} scenes registered.");
    }
}
