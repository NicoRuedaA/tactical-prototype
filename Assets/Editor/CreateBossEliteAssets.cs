using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility to create BD_InfernalKing and ED_ShadowAssassin asset files.
/// Run via: Tools -> TacticalRogue -> Create Boss & Elite Assets
///
/// This is the canonical way to create these assets. The .asset YAML files
/// at Assets/Data/Characters/ are pre-created by this script.
/// If they need to be refreshed, run this menu item again.
/// </summary>
public static class CreateBossEliteAssets
{
    private const string CharactersDir = "Assets/Data/Characters";

    [MenuItem("Tools/TacticalRogue/Create Boss & Elite Assets")]
    public static void CreateAll()
    {
        CreateInfernalKing();
        CreateShadowAssassin();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Boss and Elite assets created / updated.");
    }

    private static void CreateInfernalKing()
    {
        var existing = AssetDatabase.LoadAssetAtPath<BossData>(
            $"{CharactersDir}/BD_InfernalKing.asset");
        if (existing != null)
        {
            Debug.Log("BD_InfernalKing already exists. Skipping.");
            return;
        }

        var data = ScriptableObject.CreateInstance<BossData>();
        data.name = "BD_InfernalKing";
        data.displayName = "Infernal King";
        data.maxHp = 30;
        data.maxMana = 15;
        data.damage = 5;
        data.attackRange = 2;
        data.moveRange = 1;
        data.initiative = 8;
        data.isQueen = true;
        data.damageBuff = 2;
        data.phaseThresholdPercent = 50;

        // Load ability references
        data.phaseAbility = AssetDatabase.LoadAssetAtPath<AbilityData>(
            "Assets/Data/Abilities/AB_PowerStrike.asset");
        data.abilities = new[]
        {
            AssetDatabase.LoadAssetAtPath<AbilityData>(
                "Assets/Data/Abilities/AB_Fireball.asset"),
        };

        string path = $"{CharactersDir}/BD_InfernalKing.asset";
        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"Created: {path}");
    }

    private static void CreateShadowAssassin()
    {
        var existing = AssetDatabase.LoadAssetAtPath<EliteData>(
            $"{CharactersDir}/ED_ShadowAssassin.asset");
        if (existing != null)
        {
            Debug.Log("ED_ShadowAssassin already exists. Skipping.");
            return;
        }

        var data = ScriptableObject.CreateInstance<EliteData>();
        data.name = "ED_ShadowAssassin";
        data.displayName = "Shadow Assassin";
        data.maxHp = 12;
        data.maxMana = 8;
        data.damage = 3;
        data.attackRange = 2;
        data.moveRange = 2;
        data.initiative = 6;
        data.isQueen = false;

        // Load ability references
        data.elitePassive = AssetDatabase.LoadAssetAtPath<AbilityData>(
            "Assets/Data/Abilities/AB_Thorns.asset");
        data.abilities = new[]
        {
            AssetDatabase.LoadAssetAtPath<AbilityData>(
                "Assets/Data/Abilities/AB_PowerStrike.asset"),
        };

        string path = $"{CharactersDir}/ED_ShadowAssassin.asset";
        AssetDatabase.CreateAsset(data, path);
        Debug.Log($"Created: {path}");
    }
}
