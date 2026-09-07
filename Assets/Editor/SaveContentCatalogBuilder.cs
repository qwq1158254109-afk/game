using System.Linq;
using UnityEditor;
using UnityEngine;
using StudentGrowth;

public static class SaveContentCatalogBuilder
{
    [MenuItem("Tools/Student Growth/Rebuild Save Content Catalog")]
    public static void Rebuild()
    {
        const string path = "Assets/Resources/StudentGrowth/SaveContentCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<SaveContentCatalog>(path);
        if (catalog == null) { catalog = ScriptableObject.CreateInstance<SaveContentCatalog>(); AssetDatabase.CreateAsset(catalog,path); }
        catalog.entries = new[] { "CardData","ItemData","StageData","ActivityData","EndingData" }
            .SelectMany(type => AssetDatabase.FindAssets("t:" + type,new[] { "Assets" }))
            .Distinct().Select(guid => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(value => value != null && !string.IsNullOrEmpty(SaveContentResolver.Id(value)))
            .OrderBy(value => AssetDatabase.GetAssetPath(value)).ToList();
        catalog.saveKeys = catalog.entries.Select(value => "asset:" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))).ToList();
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        Debug.Log("Save content catalog entries: " + catalog.entries.Count);
    }
}
