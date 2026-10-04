using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vampire;

public static class MonsterShadowFootprintInstaller
{
    [Serializable] private class Catalog { public Entry[] entries; }
    [Serializable] private class Entry { public string asset; public float[] center, size; public bool preserveHeight; }

    [MenuItem("Tools/24tu/Apply reviewed monster shadow footprints")]
    public static void Install()
    {
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Tools/MonsterShadows/footprints.json"));
        foreach (var entry in catalog.entries)
        {
            string path = "Assets/Prefabs/Monsters/" + entry.asset;
            var blueprint = AssetDatabase.LoadAssetAtPath<MonsterBlueprint>(path);
            if (blueprint == null || entry.center.Length != 2 || entry.size.Length != 2)
                throw new InvalidOperationException("Invalid shadow footprint: " + path);
            blueprint.useGroundShadowFootprint = true;
            blueprint.groundShadowCenterUV = new Vector2(entry.center[0], entry.center[1]);
            blueprint.groundShadowSizeUV = new Vector2(entry.size[0], entry.size[1]);
            blueprint.preserveGroundShadowHeight = entry.preserveHeight;
            EditorUtility.SetDirty(blueprint);
            AssetDatabase.SaveAssetIfDirty(blueprint);
        }
        Debug.Log("[MonsterShadowReview] Applied " + catalog.entries.Length + " reviewed footprints");
    }
}
