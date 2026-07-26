using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class SceneSetup
{
    private const string Combat = "Assets/Scenes/Combat.unity";
    private const string Reward = "Assets/Scenes/Reward.unity";
    private const string Map = "Assets/Scenes/Map.unity";
    private const string Sample = "Assets/Scenes/SampleScene.unity";
    private const string GameOver = "Assets/Scenes/GameOver.unity";

    [MenuItem("Tools/TacticalRogue/Create GameOver Scene")]
    public static void CreateGameOverSceneMenu() => CreateGameOverScene();
    public static void ConfigureAllScenes()
    {
        CreateRewardScene();
        CreateMapScene();
        CreateGameOverScene();
        ConfigureExistingScene(Combat, "Combat");
    }
    public static void CreateGameOverScene()
    {
        var scene = OpenOrCreate(GameOver, "GameOver");
        ConfigureGameOverScene(scene);
        EditorSceneManager.SaveScene(scene, GameOver); UpdateBuildSettings();
    }

    public static void ConfigureGameOverScene(Scene scene)
    {
        RemoveLegacyUi(scene);
        EnsureCamera(scene);
        var screens = Find<DefeatScreen>(scene);
        var screen = screens.Count > 0 ? screens[0] : CreateRoot(scene, "DefeatScreen").AddComponent<DefeatScreen>();
        for (var i = 1; i < screens.Count; i++) Object.DestroyImmediate(screens[i].gameObject);
        var document = screen.GetComponent<UIDocument>() ?? screen.gameObject.AddComponent<UIDocument>();
        document.panelSettings ??= LoadPanelSettings();
        screen.Document = document;
    }

    public static void CreateRewardScene() => ConfigureToolkitScene(Reward, "Reward", "RewardScreen");
    public static void CreateMapScene() => ConfigureToolkitScene(Map, "Map", "MapView");

    private static void ConfigureExistingScene(string path, string name)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return;
        var scene = EditorSceneManager.OpenScene(path);
        RemoveLegacyUi(scene);
        var views = Find<CombatHudView>(scene);
        var view = views.Count > 0
            ? views[0]
            : CreateRoot(scene, "Combat HUD").AddComponent<CombatHudView>();
        for (var i = 1; i < views.Count; i++)
            Object.DestroyImmediate(views[i].gameObject);

        var document = view.GetComponent<UIDocument>() ?? view.gameObject.AddComponent<UIDocument>();
        document.panelSettings ??= LoadPanelSettings();
        view.Document = document;

        foreach (var input in Find<PlayerInputController>(scene))
            input.CombatHud = view;
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void ConfigureToolkitScene(string path, string name, string componentName)
    {
        var scene = OpenOrCreate(path, name); RemoveLegacyUi(scene); EnsureCamera(scene);
        if (componentName == "RewardScreen") EnsureComponent<RewardScreen>(scene, componentName);
        else EnsureComponent<MapView>(scene, componentName);
        EditorSceneManager.SaveScene(scene, path); UpdateBuildSettings();
    }
    private static T EnsureComponent<T>(Scene scene, string name) where T : Component
    {
        var existing = Find<T>(scene); var component = existing.Count > 0 ? existing[0] : CreateRoot(scene, name).AddComponent<T>();
        var document = component.GetComponent<UIDocument>() ?? component.gameObject.AddComponent<UIDocument>();
        document.panelSettings ??= LoadPanelSettings();
        return component;
    }
    private static Scene OpenOrCreate(string path, string name) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null ? EditorSceneManager.OpenScene(path) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    private static PanelSettings LoadPanelSettings() => AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset") ?? ScriptableObject.CreateInstance<PanelSettings>();
    private static void EnsureCamera(Scene scene) { if (Find<Camera>(scene).Count > 0) return; var go = CreateRoot(scene, "Main Camera"); go.tag = "MainCamera"; go.AddComponent<Camera>(); }
    private static void RemoveLegacyUi(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
            foreach (var component in go.GetComponentsInChildren<Component>(true))
                if (component is UnityEngine.Canvas || component is UnityEngine.EventSystems.EventSystem)
                    Object.DestroyImmediate(component.gameObject);
    }
    private static GameObject CreateRoot(Scene scene, string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
    private static List<T> Find<T>(Scene scene) where T : Component { var result = new List<T>(); foreach (var root in scene.GetRootGameObjects()) result.AddRange(root.GetComponentsInChildren<T>(true)); return result; }
    private static void UpdateBuildSettings() { var paths = new[] { Sample, Combat, Reward, Map, GameOver }; var scenes = new List<EditorBuildSettingsScene>(); foreach (var path in paths) scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
}
