using System.Collections.Generic;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Validates the minimum scene and data wiring required by the playable run.
/// The same validation is available from the Tools menu and runs before builds.
/// </summary>
public sealed class ProjectBaselineValidator : IPreprocessBuildWithReport
{
    private static readonly string[] RequiredScenePaths =
    {
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scenes/Combat.unity",
        "Assets/Scenes/Reward.unity",
        "Assets/Scenes/Map.unity",
        "Assets/Scenes/GameOver.unity",
    };

    public int callbackOrder => -1000;

    /// <summary>Returns the exact, ordered scene list required by player builds.</summary>
    public static string[] GetRequiredScenePaths()
    {
        return (string[])RequiredScenePaths.Clone();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        var errors = CollectErrors();
        if (errors.Count > 0)
            throw new BuildFailedException(FormatErrors(errors));
    }

    [MenuItem("Tools/TacticalRogue/Validate Project Baseline")]
    public static void ValidateFromMenu()
    {
        var errors = CollectErrors();
        if (errors.Count > 0)
        {
            Debug.LogError(FormatErrors(errors));
            return;
        }

        Debug.Log("Project baseline validation passed.");
    }

    public static IReadOnlyList<string> CollectErrors()
    {
        var errors = new List<string>();
        ValidateBuildScenes(errors);

        foreach (string path in RequiredScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                continue;

            ValidateSceneAsset(path, errors);
        }

        return errors;
    }

    /// <summary>
    /// Validates an already loaded scene without opening, saving, or closing it.
    /// The optional logical name supports validation of unsaved test scenes.
    /// </summary>
    public static IReadOnlyList<string> CollectSceneErrors(Scene scene, string logicalSceneName = null)
    {
        var errors = new List<string>();
        ValidateLoadedScene(scene, logicalSceneName ?? scene.name, errors);
        return errors;
    }

    private static void ValidateBuildScenes(List<string> errors)
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToList();

        foreach (string path in RequiredScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                errors.Add($"Required scene asset is missing: {path}");
            else if (!enabledScenes.Contains(path))
                errors.Add($"Required scene is not enabled in Build Settings: {path}");
        }

        if (!enabledScenes.SequenceEqual(RequiredScenePaths))
        {
            errors.Add(
                "Enabled Build Settings scenes must exactly match the required order. " +
                $"Expected: {string.Join(", ", RequiredScenePaths)}. " +
                $"Actual: {string.Join(", ", enabledScenes)}.");
        }
    }

    private static void ValidateSceneAsset(string path, List<string> errors)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

        try
        {
            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            ValidateLoadedScene(scene, scene.name, errors);
        }
        catch (System.Exception exception)
        {
            errors.Add($"Could not validate {path}: {exception.Message}");
        }
        finally
        {
            if (openedForValidation && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ValidateLoadedScene(Scene scene, string logicalSceneName, List<string> errors)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add($"Cannot validate unloaded or invalid scene '{logicalSceneName}'.");
            return;
        }

        switch (logicalSceneName)
        {
            case "SampleScene":
                ValidateSampleScene(scene, errors);
                break;
            case "Combat":
                ValidateCombatScene(scene, errors);
                break;
            case "Map":
                ValidateMapScene(scene, errors);
                break;
            case "Reward":
                ValidateRewardScene(scene, errors);
                break;
            case "GameOver":
                ValidateGameOverScene(scene, errors);
                break;
            default:
                errors.Add($"No baseline validation rules exist for scene '{logicalSceneName}'.");
                break;
        }
    }

    private static void ValidateSampleScene(Scene scene, List<string> errors)
    {
        var managers = FindInScene<RunManager>(scene);
        var bootstrappers = FindInScene<RunBootstrapper>(scene);

        RequireSingle(managers, scene.name, nameof(RunManager), errors);
        RequireSingle(bootstrappers, scene.name, nameof(RunBootstrapper), errors);
        if (managers.Count != 1 || bootstrappers.Count != 1)
            return;

        var manager = managers[0];
        var bootstrapper = bootstrappers[0];

        ValidateCharacterArray(manager.PlayerTeam, "SampleScene RunManager.PlayerTeam", errors);
        ValidateEnemyPool(manager, MapNodeType.Combat, errors);
        ValidateEnemyPool(manager, MapNodeType.Elite, errors);
        ValidateEnemyPool(manager, MapNodeType.Boss, errors);

        if (!bootstrapper.AutoStart)
            errors.Add("SampleScene RunBootstrapper.AutoStart must be enabled.");
        if (bootstrapper.RunManager != manager)
            errors.Add("SampleScene RunBootstrapper.RunManager must reference the scene RunManager.");
    }

    private static void ValidateEnemyPool(RunManager manager, MapNodeType nodeType, List<string> errors)
    {
        var rosters = manager.enemyTeamPools == null
            ? new TeamRoster[0]
            : manager.enemyTeamPools.Where(pool => pool.nodeType == nodeType).ToArray();

        if (rosters.Length == 0)
        {
            errors.Add($"SampleScene RunManager needs at least one {nodeType} enemy roster.");
            return;
        }

        for (int i = 0; i < rosters.Length; i++)
            ValidateCharacterArray(rosters[i].enemies, $"SampleScene {nodeType} roster {i}", errors);
    }

    private static void ValidateCombatScene(Scene scene, List<string> errors)
    {
        var runners = FindInScene<CombatRunner>(scene);
        var views = FindInScene<CombatView>(scene);
        var inputs = FindInScene<PlayerInputController>(scene);
        var huds = FindInScene<CombatHudView>(scene);

        RequireSingle(runners, scene.name, nameof(CombatRunner), errors);
        RequireSingle(views, scene.name, nameof(CombatView), errors);
        RequireSingle(inputs, scene.name, nameof(PlayerInputController), errors);
        RequireSingle(huds, scene.name, nameof(CombatHudView), errors);
        ValidateInputSystemEvent(scene, errors);
        if (runners.Count != 1 || views.Count != 1 || inputs.Count != 1 || huds.Count != 1)
            return;

        var runner = runners[0];
        var view = views[0];
        var input = inputs[0];
        var hud = huds[0];

        if (runner.Width <= 0 || runner.Height <= 0)
            errors.Add("Combat CombatRunner board dimensions must be positive.");
        ValidateCharacterArray(
            new[] { runner.PlayerQueenData, runner.PlayerPawnData, runner.EnemyQueenData, runner.EnemyPawnData },
            "Combat CombatRunner direct-scene fallback characters",
            errors);

        if (runner.CombatView != view || runner.PlayerInput != input ||
            view.Runner != runner || input.Runner != runner || input.CombatView != view)
            errors.Add("Combat runner, view, and input references are not wired to each other.");
        if (input.CombatHud != hud || !hud.IsConfigured)
            errors.Add("Combat serialized HUD is missing references or is not wired to player input.");
        if (view.BoardRoot == null || view.PiecesRoot == null || view.TilePrefab == null || view.PiecePrefab == null)
            errors.Add("Combat CombatView is missing board roots or piece/tile prefabs.");
        if (view.TileNormal == null || view.TileReachable == null || view.TileAttackable == null ||
            view.TileSelected == null || view.TileAbilityRange == null ||
            view.PiecePlayerMat == null || view.PieceEnemyMat == null)
            errors.Add("Combat CombatView is missing one or more required materials.");
        if (input.TargetCamera == null)
            errors.Add("Combat PlayerInputController.TargetCamera is not assigned.");
    }

    private static void ValidateMapScene(Scene scene, List<string> errors)
    {
        RequireSingle(FindInScene<MapView>(scene), scene.name, nameof(MapView), errors);
        ValidateInputSystemEvent(scene, errors);

        // MapView intentionally supports null prefab/content references by creating
        // runtime buttons and a Canvas. LineRendererPrefab is optional.
    }

    private static void ValidateRewardScene(Scene scene, List<string> errors)
    {
        var screens = FindInScene<RewardScreen>(scene);
        RequireSingle(screens, scene.name, nameof(RewardScreen), errors);
        ValidateInputSystemEvent(scene, errors);

        if (screens.Count != 1)
            return;

        var screen = screens[0];
        if (screen.TitleText == null ||
            screen.CardButton0 == null || screen.CardButton1 == null || screen.CardButton2 == null ||
            screen.CardText0 == null || screen.CardText1 == null || screen.CardText2 == null)
            errors.Add("Reward RewardScreen is missing one or more required UI references.");
    }

    private static void ValidateGameOverScene(Scene scene, List<string> errors)
    {
        var screens = FindInScene<DefeatScreen>(scene);
        RequireSingle(screens, scene.name, nameof(DefeatScreen), errors);
        ValidateInputSystemEvent(scene, errors);

        if (screens.Count == 1 && (screens[0].TitleText == null || screens[0].MainMenuButton == null))
            errors.Add("GameOver DefeatScreen must reference its title and restart button.");
    }

    private static void ValidateInputSystemEvent(Scene scene, List<string> errors)
    {
        if (FindInScene<EventSystem>(scene).Count != 1)
            errors.Add($"{scene.name} must contain exactly one EventSystem.");
        if (FindInScene<InputSystemUIInputModule>(scene).Count != 1)
            errors.Add($"{scene.name} must contain exactly one InputSystemUIInputModule.");
    }

    private static void ValidateCharacterArray(CharacterData[] characters, string label, List<string> errors)
    {
        if (characters == null || characters.Length == 0)
        {
            errors.Add($"{label} is null or empty.");
            return;
        }

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == null)
                errors.Add($"{label}[{i}] is not assigned.");
        }
    }

    private static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results;
    }

    private static void RequireSingle<T>(IReadOnlyCollection<T> components, string sceneName, string typeName, List<string> errors)
    {
        if (components.Count != 1)
            errors.Add($"{sceneName} must contain exactly one {typeName}; found {components.Count}.");
    }

    private static string FormatErrors(IReadOnlyList<string> errors)
    {
        return "Project baseline validation failed:\n- " + string.Join("\n- ", errors);
    }
}
