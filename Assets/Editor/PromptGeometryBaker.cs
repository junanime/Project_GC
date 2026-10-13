using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vampire;

public static class PromptGeometryBaker
{
    [MenuItem("Tools/Junhan2/Bake interaction prompt bounds")]
    public static void Bake()
    {
        var sprites = new HashSet<Sprite>();
        void Collect(GameObject go)
        {
            foreach (var body in go.GetComponentsInChildren<SpriteRenderer>(true))
                if (body.sprite != null) sprites.Add(body.sprite);
        }
        bool Interaction(GameObject go) => go.GetComponentInChildren<InteractableEventObject>(true) != null ||
            go.GetComponentInChildren<Chest>(true) != null || go.GetComponentInChildren<MerchantNPC>(true) != null ||
            go.GetComponentInChildren<BloodClotMiniStagePortal>(true) != null || go.GetComponentInChildren<MiniStageReturnInteractable>(true) != null ||
            go.GetComponentInChildren<MiniStageMysteryChestInteractable>(true) != null || go.GetComponentInChildren<ExitPortalInteractable>(true) != null;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && Interaction(prefab)) Collect(prefab);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ChestBlueprint"))
        {
            var chest = AssetDatabase.LoadAssetAtPath<ChestBlueprint>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var sprite in new[] { chest.closedChest, chest.openingChest, chest.openChest }) if (sprite != null) sprites.Add(sprite);
        }
        foreach (var owner in Object.FindObjectsOfType<MonoBehaviour>(true))
            if (owner is InteractableEventObject || owner is BloodClotMiniStagePortal || owner is MiniStageReturnInteractable || owner is ExitPortalInteractable)
                Collect(owner.gameObject);
        // Dynamic field props and animation frames, loaded by their existing Resources keys.
        foreach (var folder in new[] { "Assets/Resources/OctoberUI", "Assets/Resources/ToadUpdate", "Assets/Resources/Stage2Snails" })
            if (AssetDatabase.IsValidFolder(folder))
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                    foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>()) sprites.Add(sprite);
        var entries = new List<PromptSpriteGeometry.Entry>();
        foreach (var group in sprites.GroupBy(AssetDatabase.GetAssetPath))
        {
            if (!group.Key.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
            var texture = new Texture2D(2, 2); texture.LoadImage(File.ReadAllBytes(group.Key));
            var pixels = texture.GetPixels32();
            foreach (var sprite in group)
            {
                var r = sprite.rect; float sx = (float)texture.width / sprite.texture.width, sy = (float)texture.height / sprite.texture.height;
                int x0 = Mathf.FloorToInt(r.x * sx), y0 = Mathf.FloorToInt(r.y * sy), x1 = Mathf.Min(texture.width, Mathf.CeilToInt(r.xMax * sx)), y1 = Mathf.Min(texture.height, Mathf.CeilToInt(r.yMax * sy));
                int minX = x1, minY = y1, maxX = x0 - 1, maxY = y0 - 1;
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                    if (pixels[y * texture.width + x].a > 32) { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                if (maxX < minX) continue;
                entries.Add(new PromptSpriteGeometry.Entry { key = PromptSpriteGeometry.Key(sprite), assetPath = group.Key, spriteName = sprite.name, bounds = new Rect(
                    (minX / sx - r.x - sprite.pivot.x) / sprite.pixelsPerUnit, (minY / sy - r.y - sprite.pivot.y) / sprite.pixelsPerUnit,
                    (maxX - minX + 1) / (sx * sprite.pixelsPerUnit), (maxY - minY + 1) / (sy * sprite.pixelsPerUnit)) });
            }
            Object.DestroyImmediate(texture);
        }
        const string path = "Assets/Junhan/Resources/Interaction/PromptGeometry.asset";
        var data = AssetDatabase.LoadAssetAtPath<PromptSpriteGeometry>(path);
        if (data == null) { data = ScriptableObject.CreateInstance<PromptSpriteGeometry>(); AssetDatabase.CreateAsset(data, path); }
        foreach (var group in entries.GroupBy(e => e.key))
            if (group.Select(e => e.bounds).Distinct().Count() > 1)
                throw new System.InvalidOperationException("Interaction sprite key collision: " + group.Key);
        data.entries = entries.OrderBy(e => e.assetPath).ThenBy(e => e.spriteName).ToArray();
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
        Debug.Log("PROMPT_GEOMETRY_BAKED " + entries.Count);
    }
}
