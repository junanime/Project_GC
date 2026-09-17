using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vampire;
using Object = UnityEngine.Object;

/// <summary>Explicit, repeatable asset migration and regression checks for the five-core UFO.</summary>
public static class Junhan2BossSetup
{
    const string BossPath = "Assets/Junhan/Prefabs/Boss/Test/BossPartDamageTestRoot.prefab";
    static readonly string[] Colors = { "Red", "Orange", "Yellow", "Green", "Blue" };
    static readonly BossCoreTrait[] Traits = { BossCoreTrait.Red, BossCoreTrait.Orange, BossCoreTrait.Yellow, BossCoreTrait.Green, BossCoreTrait.Blue };
    static readonly List<string> Report = new List<string>();

    [MenuItem("Tools/Junhan2/Configure and validate boss and portals")]
    public static void Run()
    {
        try
        {
            ConfigureBoss();
            ConfigurePortals();
            AssetDatabase.SaveAssets();
            Validate();
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/Junhan2-validation.txt", Report);
            Debug.Log("JUNHAN2_VALIDATION_PASS\n" + string.Join("\n", Report));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null) throw new Exception(target.GetType().Name + "." + field + " missing");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureBoss()
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var health = root.GetComponentInChildren<BossPartDamageTestRootController>(true);
            var controller = root.GetComponentInChildren<BossFiveCoreSkillController>(true);
            var cores = Colors.Select(c => root.GetComponentsInChildren<BossPartDamageTestPart>(true)
                .Single(p => p.name == c + "Core")).ToArray();
            var pools = cores.Select(c => new List<BossPatternBase>()).ToArray();
            var all = root.GetComponentsInChildren<BossPatternBase>(true);
            // Preserve every existing configured instance, including legacy variants and their projectile references.
            foreach (var pattern in all)
            {
                int index = pattern is BossFanShotPattern ? 0 : pattern is BossBombLobPattern || pattern is BossCaffeineInjectorPattern ? 2 :
                    pattern is BossHomingMissilePattern ? 4 : pattern is BossAbsorbMinionHealPattern || pattern is BossColaBottleHealPattern ? 3 :
                    pattern is BossChargePattern ? 0 : 1;
                if (pattern.PatternName == "GreenRadial") index = 3;
                if (pattern.PatternName == "OrangeRadial") index = 1;
                bool chargeCombo = pattern is BossChargePattern && pattern.CombinationCore != null;
                bool colaCombo = pattern is BossColaBottleHealPattern && pattern.CombinationCore != null;
                if (colaCombo) index = 2;
                Set(pattern, "ownerPart", cores[index]);
                Set(pattern, "combinationCore", chargeCombo ? cores[1] : colaCombo ? cores[3] : null);
                var so = new SerializedObject(pattern);
                so.FindProperty("coreTraits").intValue = (int)(Traits[index] | (chargeCombo ? Traits[1] : colaCombo ? Traits[3] : BossCoreTrait.None));
                so.FindProperty("coreMatchMode").intValue = chargeCombo || colaCombo ? 1 : 0;
                so.FindProperty("additionalRequiredParts").arraySize = 0;
                if (pattern is BossCaffeineInjectorPattern) so.FindProperty("blockOtherPatternsDuringChallenge").boolValue = true;
                if (pattern is BossColaBottleHealPattern) so.FindProperty("allowOtherPatternsWhileBottleExists").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                pattern.transform.SetParent(cores[index].transform, false);
                pattern.gameObject.SetActive(true);
                pattern.enabled = true;
                pools[index].Add(pattern);
            }
            for (int i = 0; i < cores.Length; ++i)
            {
                var so = new SerializedObject(cores[i]);
                var pool = so.FindProperty("skillPool");
                pool.arraySize = pools[i].Count;
                for (int j = 0; j < pools[i].Count; ++j) pool.GetArrayElementAtIndex(j).objectReferenceValue = pools[i][j];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var slotsSO = new SerializedObject(controller);
            var slots = slotsSO.FindProperty("combinationSlots");
            slots.arraySize = 10;
            int slotIndex = 0;
            for (int a = 0; a < 5; ++a)
                for (int b = a + 1; b < 5; ++b)
                {
                    var slot = slots.GetArrayElementAtIndex(slotIndex++);
                    slot.FindPropertyRelative("label").stringValue = Colors[a] + " + " + Colors[b];
                    slot.FindPropertyRelative("primary").objectReferenceValue = cores[a];
                    slot.FindPropertyRelative("secondary").objectReferenceValue = cores[b];
                    slot.FindPropertyRelative("pattern").objectReferenceValue = all.FirstOrDefault(p => p.OwnerPart == cores[a] && p.CombinationCore == cores[b]);
                }
            slotsSO.ApplyModifiedPropertiesWithoutUndo();
            // The independent cores own activation; the legacy timer/visual synthesis must not run on this prefab.
            foreach (var old in root.GetComponentsInChildren<BossCoreStateController>(true)) old.enabled = false;
            foreach (var old in root.GetComponentsInChildren<BossCoreVisualController>(true)) old.enabled = false;

            var shadow = root.GetComponentInChildren<BossGroundShadow>(true);
            if (shadow == null)
            {
                var go = new GameObject("UFOGroundShadow");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0, -1.05f, 0);
                go.transform.localScale = new Vector3(.65f, .65f, 1);
                shadow = go.AddComponent<BossGroundShadow>();
            }
            Set(shadow, "healthRoot", health);
            var sr = shadow.GetComponent<SpriteRenderer>();
            sr.sprite = MakeShadowSprite();
            sr.color = new Color(0.12f, 0.10f, 0.16f, 0.45f);
            sr.sortingLayerName = "Default";
            sr.sortingOrder = -20;
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Sprite MakeShadowSprite()
    {
        const string path = "Assets/Junhan/Art/UFOGroundShadow.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(480, 180, TextureFormat.RGBA32, false);
            var pixels = new Color[480 * 180];
            for (int y = 0; y < 180; ++y)
                for (int x = 0; x < 480; ++x)
                {
                    float r = Mathf.Sqrt(Mathf.Pow((x + 0.5f - 240) / 240, 2) + Mathf.Pow((y + 0.5f - 90) / 90, 2));
                    pixels[y * 480 + x] = new Color(1, 1, 1, Mathf.Clamp01((1 - r) / 0.12f));
                }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static bool WirePortals(GameObject root)
    {
        var portals = root.GetComponentsInChildren<MiniStageReturnInteractable>(true);
        foreach (var portal in portals)
        {
            var renderer = portal.GetComponent<SpriteRenderer>() ?? portal.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null) throw new Exception("No return portal renderer: " + root.name);
            Set(portal, "portalRenderer", renderer);
            Set(portal, "lockedSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/mini_not_open.png"));
            Set(portal, "unlockedSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/mini_open.png"));
            portal.SetUnlocked(false);
        }
        return portals.Length > 0;
    }

    static void ConfigurePortals()
    {
        string guid = AssetDatabase.AssetPathToGUID("Assets/Scripts/Gameplay/MiniStage/MiniStageReturnInteractable.cs");
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && (p.EndsWith(".prefab") || p.EndsWith(".unity"))))
        {
            if (!File.ReadAllText(path).Contains(guid)) continue;
            if (path.EndsWith(".prefab"))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { if (WirePortals(root)) { PrefabUtility.SaveAsPrefabAsset(root, path); Report.Add("Portal wired: " + path); } }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            else
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= WirePortals(root);
                if (changed) { EditorSceneManager.SaveScene(scene); Report.Add("Scene portals wired: " + path); }
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static void Check(bool pass, string message)
    {
        if (!pass) throw new Exception("FAIL: " + message);
        Report.Add("PASS: " + message);
    }

    public static void Validate()
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var health = root.GetComponentInChildren<BossPartDamageTestRootController>(true);
            var skills = root.GetComponentInChildren<BossFiveCoreSkillController>(true);
            var registered = new HashSet<BossPatternBase>();
            foreach (var core in health.FiveCores)
            {
                Check(core.SkillPool.Any(p => p != null && p.CombinationCore == null), core.name + " single patterns");
                foreach (var pattern in core.SkillPool)
                {
                    Check(pattern.OwnerPart == core && pattern.isActiveAndEnabled, core.name + " -> " + pattern.GetType().Name + " / " + pattern.PatternName);
                    registered.Add(pattern);
                }
            }
            Check(root.GetComponentsInChildren<BossPatternBase>(true).All(registered.Contains), "Every prefab pattern is in a skill pool");
            Check(registered.Select(p => p.GetType()).Distinct().Count() == 8, "All eight implemented Pattern types covered");
            Check(skills.CombinationSlots.Count == 10, "Ten independent two-core registration slots");
            Check(skills.CombinationSlots.Count(s => s.pattern != null) == 2, "Two implemented combinations, eight extension slots");
            foreach (var slot in skills.CombinationSlots.Where(s => s.pattern != null))
            {
                Check(slot.pattern.OwnerPart == slot.primary && slot.pattern.CombinationCore == slot.secondary, slot.label + " references agree");
                Check(!skills.CanSelect(slot.pattern, 1), slot.label + " blocked in phase 1");
                Check(skills.BeginPattern(slot.pattern, 2), slot.label + " starts in phase 2");
                Check(skills.ActivePrimary == slot.primary && skills.ActiveSecondary == slot.secondary && skills.CanContinue(slot.pattern), slot.label + " activates both required cores");
                slot.secondary.gameObject.SetActive(false);
                Check(!skills.CanContinue(slot.pattern) && !skills.CanSelect(slot.pattern, 2), slot.label + " stops when secondary unavailable");
                slot.secondary.gameObject.SetActive(true);
                skills.EndPattern();
            }
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "No missing script: " + transform.name);
            var shadow = root.GetComponentInChildren<BossGroundShadow>();
            Check(shadow != null && shadow.GetComponent<SpriteRenderer>().sprite != null, "Serialized shadow sprite");
            Check(shadow.transform.parent == root.transform, "Shadow independent of floating body");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        TestShadowOverlap();
        TestShadowArea();
        TestPortal();
        Check(BossCoreTraitUtility.IsSingle(BossCoreTrait.Orange) && BossCoreTraitUtility.IsSingle(BossCoreTrait.Green), "Orange and green are independent traits");
        Check(!BossCoreTraitUtility.Matches(BossCoreTrait.Red | BossCoreTrait.Yellow, BossCoreTrait.Orange, BossPatternCoreMatchMode.All), "Red + yellow does not synthesize orange");
    }

    static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);

    static void TestShadowOverlap()
    {
        var a = new GameObject("Shadow A").AddComponent<BossGroundShadow>();
        var b = new GameObject("Shadow B").AddComponent<BossGroundShadow>();
        var sr = new GameObject("Tint test").AddComponent<SpriteRenderer>();
        try
        {
            var original = new Color(.8f, .6f, .4f, .7f); sr.color = original;
            Call(a, "Apply", sr); Color single = sr.color;
            Call(b, "Apply", sr); Check(sr.color.r < single.r, "Overlapping shadows darken together");
            Call(a, "Release", sr); Check(sr.color == single, "Leaving first shadow preserves second tint");
            Call(b, "Release", sr); Check(sr.color == original, "Last shadow restores original RGBA");
            Call(a, "Apply", sr); Call(b, "Apply", sr);
            Call(b, "Release", sr); Call(a, "Release", sr);
            Check(sr.color == original, "Reverse exit order restores original RGBA");
            Call(a, "Apply", sr); sr.color = Color.red; Call(a, "Apply", sr); Call(a, "Release", sr);
            Check(sr.color == Color.red, "External gameplay color survives shadow exit");
        }
        finally { Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject); Object.DestroyImmediate(sr.gameObject); }
    }

    static void TestPortal()
    {
        var go = new GameObject("Portal test");
        try
        {
            var sr = go.AddComponent<SpriteRenderer>();
            var portal = go.AddComponent<MiniStageReturnInteractable>();
            WirePortals(go);
            portal.SetUnlocked(false); Check(sr.sprite != null && sr.sprite.name == "mini_not_open", "Locked portal sprite");
            portal.SetUnlocked(true); Check(sr.sprite != null && sr.sprite.name == "mini_open", "Unlocked portal sprite");
            portal.SetUnlocked(false); Check(sr.sprite.name == "mini_not_open", "Room reuse relocks portal sprite");
        }
        finally { Object.DestroyImmediate(go); }
    }

    static void TestShadowArea()
    {
        var a = new GameObject("Area A").AddComponent<BossGroundShadow>();
        var b = new GameObject("Area B").AddComponent<BossGroundShadow>();
        var actor = new GameObject("Area monster");
        try
        {
            actor.AddComponent<Monster>();
            actor.AddComponent<CircleCollider2D>();
            actor.AddComponent<BoxCollider2D>();
            var sr = actor.AddComponent<SpriteRenderer>();
            var original = new Color(.7f, .8f, .9f, .6f); sr.color = original;
            Physics2D.SyncTransforms();
            Call(a, "LateUpdate"); Color single = sr.color;
            Check(single.r < original.r && single.a == original.a, "Physics area detects monster with multiple colliders");
            Call(b, "LateUpdate");
            Call(a, "OnDisable"); Check(sr.color == single, "Disabling one overlapping area retains other tint");
            actor.transform.position = Vector3.one * 100;
            Physics2D.SyncTransforms(); Call(b, "LateUpdate");
            Check(sr.color == original, "Teleport out restores original color without exit event");
            actor.transform.position = Vector3.zero;
            Physics2D.SyncTransforms(); Call(b, "LateUpdate");
            actor.SetActive(false); Physics2D.SyncTransforms(); Call(b, "LateUpdate");
            Check(sr.color == original, "Pooled actor disable restores color");
            Object.DestroyImmediate(actor.GetComponent<Monster>());
            actor.AddComponent<Character>(); actor.SetActive(true);
            Physics2D.SyncTransforms(); Call(b, "LateUpdate");
            Check(sr.color.r < original.r, "Physics area detects player");
            Call(b, "OnDisable"); Check(sr.color == original, "Shadow disable restores player color");
        }
        finally { Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject); Object.DestroyImmediate(actor); }
    }

    public static void ValidateAndPreview()
    {
        Validate();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossPath));
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        var rt = new RenderTexture(1000, 800, 24);
        var texture = new Texture2D(1000, 800, TextureFormat.RGB24, false);
        try
        {
            var body = root.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "UFOBody");
            camera.transform.position = new Vector3(body.bounds.center.x, body.bounds.center.y - .5f, -10);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(body.bounds.size.y, body.bounds.size.x * .5f) + 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.72f, .73f, .69f);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); texture.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/UFO-shadow-preview.png", texture.EncodeToPNG());
            File.WriteAllLines("Logs/Junhan2-validation.txt", Report);
            Debug.Log("JUNHAN2_VALIDATION_PASS");
        }
        finally
        {
            RenderTexture.active = null; camera.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
            Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(root);
        }
    }
}
