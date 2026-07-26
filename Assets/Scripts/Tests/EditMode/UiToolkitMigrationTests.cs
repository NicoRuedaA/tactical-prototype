using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UiToolkitMigrationTests
{
    [Test]
    public void RuntimeViewsExposeOnlyToolkitDocuments()
    {
        var go = new GameObject("Views");
        try
        {
            Assert.That(go.AddComponent<CombatHudView>().GetComponent<UIDocument>(), Is.Not.Null);
            var map = go.AddComponent<MapView>();
            Assert.That(map.GetComponent<UIDocument>(), Is.Not.Null);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void ToolkitViewBuildsNamedControls()
    {
        var go = new GameObject("Combat HUD");
        try
        {
            var view = go.AddComponent<CombatHudView>();
            var root = view.Document.rootVisualElement;
            Assert.That(root.Q<Button>(), Is.Not.Null);
            Assert.That(root.Q<Label>(), Is.Not.Null);
        }
        finally { Object.DestroyImmediate(go); }
    }
}
