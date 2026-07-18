using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Tests
{
    public sealed class CombatFeedbackViewTests
    {
        private const string PiecePrefabPath = "Assets/Prefabs/Piece.prefab";

        [Test]
        public void PiecePrefab_RefreshVitals_UsesEffectiveMaxHpAndSerializedManaReferences()
        {
            GameObject instance = InstantiatePiecePrefab();
            try
            {
                PieceView view = instance.GetComponent<PieceView>();
                var piece = new Piece(
                    "vitals", Team.Player, maxHp: 10, damage: 1,
                    attackRange: 1, moveRange: 1, initiative: 1,
                    maxMana: 8);
                piece.AddBonusMaxHp(5);
                piece.TakeDamage(3);
                piece.SpendMana(2);
                view.Piece = piece;

                view.RefreshVitals();

                Assert.That(view.HasVitalReferences, Is.True,
                    "The production Piece prefab must serialize both HP and mana bars.");
                Assert.That(view.HpFillRatio, Is.EqualTo(12f / 15f).Within(0.0001f));
                Assert.That(view.ManaFillRatio, Is.EqualTo(6f / 8f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ConfigureHighlight_WithoutPrefab_CreatesOneReusableIndicatorWithoutMaterialInstances()
        {
            GameObject instance = InstantiatePiecePrefab();
            try
            {
                PieceView view = instance.GetComponent<PieceView>();
                Material sharedMaterial = instance.GetComponent<Renderer>().sharedMaterial;

                view.ConfigureHighlight(
                    null, sharedMaterial, sharedMaterial, sharedMaterial);
                view.SetHighlight(PieceHighlight.Attackable);
                view.ConfigureHighlight(
                    null, sharedMaterial, sharedMaterial, sharedMaterial);
                view.SetHighlight(PieceHighlight.Invalid);

                Transform[] indicators = instance.GetComponentsInChildren<Transform>(true)
                    .Where(child => child.name == "Target Indicator")
                    .ToArray();
                Assert.That(indicators, Has.Length.EqualTo(1));
                Renderer indicatorRenderer = indicators[0].GetComponentInChildren<Renderer>(true);
                Assert.That(indicatorRenderer, Is.Not.Null);
                Assert.That(indicatorRenderer.sharedMaterial, Is.SameAs(sharedMaterial),
                    "Highlighting must use shared materials plus property blocks, never Renderer.material.");
                Assert.That(indicators[0].GetComponentInChildren<Collider>(true), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject InstantiatePiecePrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PiecePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
    }
}
