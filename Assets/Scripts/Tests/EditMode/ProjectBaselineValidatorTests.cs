using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ProjectBaselineValidatorTests
{
    [TearDown]
    public void TearDown()
    {
        if (RunManager.Instance != null)
            UnityEngine.Object.DestroyImmediate(RunManager.Instance.gameObject);
    }

    [Test]
    public void CurrentProject_PassesBaselineValidation()
    {
        Assert.That(ProjectBaselineValidator.CollectErrors(), Is.Empty);
    }

    [Test]
    public void ConfigureGameOverScene_CalledTwice_RemainsValidAndUnique()
    {
        Scene testScene = EditorSceneManager.NewPreviewScene();

        try
        {
            SceneSetup.ConfigureGameOverScene(testScene);
            SceneSetup.ConfigureGameOverScene(testScene);

            Assert.That(FindInScene<Canvas>(testScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<DefeatScreen>(testScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<EventSystem>(testScene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<InputSystemUIInputModule>(testScene), Has.Count.EqualTo(1));

            var screen = FindInScene<DefeatScreen>(testScene)[0];
            Assert.That(screen.TitleText, Is.Not.Null);
            Assert.That(screen.MainMenuButton, Is.Not.Null);
            Assert.That(ProjectBaselineValidator.CollectSceneErrors(testScene, "GameOver"), Is.Empty);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(testScene);
        }
    }

    [Test]
    public void RestartRun_WithoutSceneTransition_ResetsStateWithoutDestroyingManager()
    {
        var managerObject = new GameObject("RunManager Restart Test");
        var manager = managerObject.AddComponent<RunManager>();

        SetProperty(manager, nameof(RunManager.CurrentPhase), RunManager.RunPhase.Defeat);
        SetProperty(manager, nameof(RunManager.LastRunWasVictory), true);
        SetField(manager, "_currentCombatIndex", 4);
        SetField(manager, "_currentNodeType", MapNodeType.Elite);

        typeof(RunManager).GetMethod(
                nameof(RunManager.RestartRun),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool) },
                null)
            ?.Invoke(manager, new object[] { false });

        Assert.That(manager.CurrentPhase, Is.EqualTo(RunManager.RunPhase.None));
        Assert.That(manager.LastRunWasVictory, Is.False);
        Assert.That(GetField<int>(manager, "_currentCombatIndex"), Is.Zero);
        Assert.That(GetField<MapNodeType>(manager, "_currentNodeType"), Is.EqualTo(default(MapNodeType)));
        Assert.That(manager, Is.Not.Null);
    }

    private static System.Collections.Generic.List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var results = new System.Collections.Generic.List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<T>(true));
        return results;
    }

    private static void SetProperty<T>(RunManager manager, string name, T value)
    {
        typeof(RunManager).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?.SetValue(manager, value);
    }

    private static void SetField<T>(RunManager manager, string name, T value)
    {
        typeof(RunManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(manager, value);
    }

    private static T GetField<T>(RunManager manager, string name)
    {
        return (T)typeof(RunManager)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(manager);
    }
}
