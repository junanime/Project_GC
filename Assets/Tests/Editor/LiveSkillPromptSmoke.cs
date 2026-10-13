using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class LiveSkillPromptSmoke
    {
        const string Key = "LiveSkillPromptSmoke";
        const string Output = "Library/LiveSkillPromptProof";
        static double deadline;
        static bool started;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static LiveSkillPromptSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if(File.Exists(Output+"/prefs-backup.json") && !File.Exists(Output+"/result.txt"))
                foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs-backup.json")).values)
                { if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key); }
            if(File.Exists(Output+"/result.txt"))File.Delete(Output+"/result.txt");
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys = catalog.entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct().ToArray();
            File.WriteAllText(Output + "/prefs-backup.json", JsonUtility.ToJson(new Backup { values = keys.Select(k => new Saved { key = k, exists = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            foreach (var e in catalog.entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id), 1);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            var view = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(view).position = new Rect(0, 0, 1280, 741);
            if(!File.Exists("Assets/Junhan/Resources/Interaction/PromptGeometry.asset")) PromptGeometryBaker.Bake();
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 240; started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { SessionState.SetBool(Key + "Failed", true); SessionState.SetBool(Key + "Done", true); } }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { SessionState.SetBool(Key + "Failed", true); SessionState.SetBool(Key + "Done", true); }
            if (SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(Output + "/prefs-backup.json"));
                foreach (var s in backup.values) { if (s.exists) PlayerPrefs.SetInt(s.key, s.value); else PlayerPrefs.DeleteKey(s.key); }
                PlayerPrefs.Save(); SessionState.SetBool(Key, false);
                bool failed = SessionState.GetBool(Key + "Failed", false);
                Debug.Log("[LiveSkillPrompt] FINISHED failed=" + failed); EditorApplication.Exit(failed ? 1 : 0); return;
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (!started && EditorApplication.isPlaying && level != null && level.PlayerCharacter != null && level.CurrentLevelTime > .1f)
            { started = true; level.StartCoroutine(Checks(level)); }
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("[LiveSkillPrompt] FAIL " + message); Debug.Log("[LiveSkillPrompt] PASS " + message); }
        static FieldInfo Field(object obj, string name)
        {
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new MissingFieldException(name);
        }
        static object Get(object o, string f) => Field(o, f).GetValue(o);
        static void Set(object o, string f, object value) => Field(o, f).SetValue(o, value);
        static object Call(object o, string name, params object[] args)
        { return o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, args); }
        static void Capture(string name)
        {
            var cam = Camera.main; var oldTarget = cam.targetTexture; var oldActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
            cam.targetTexture = oldTarget; RenderTexture.active = oldActive; RenderTexture.ReleaseTemporary(rt); Object.Destroy(tex);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            Application.runInBackground = true; Time.timeScale = 1; Time.captureDeltaTime = 1f / 60f;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            foreach (var b in Object.FindObjectsOfType<MonoBehaviour>())
                if (b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner) { b.enabled = false; b.StopAllCoroutines(); }
            level.enabled = false; var entity = level.EntityManager; entity.enabled = false; entity.StopAllCoroutines();
            foreach (var m in entity.LivingMonsters.ToArray()) m.gameObject.SetActive(false);
            var player = level.PlayerCharacter; Set(player, "isInvincible", true);
            var needle = SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());
            var skill = player.Skills; skill.Restore(0, 0, 0); skill.RestoreLevels(1, 1);
            float move = player.CurrentMoveSpeed; int count = needle.GetEffectiveProjectileCount();
            Check(skill.TryActivate() && skill.Active && skill.IsSummoning && !skill.IsCutin && Time.timeScale == 1, "Ashi activates instantly without cut-in pause");
            Check(!needle.AttacksBlocked && needle.GetEffectiveProjectileCount() == count * 2, "active immediately doubles projectiles without blocking attacks");
            Check(Mathf.Abs(player.CurrentMoveSpeed - move * 2) < .01f, "movement buff applies immediately");
            Check(!skill.TryActivate(), "summon cannot be restarted while cooling down");
            float clock = Time.time; Vector3 position = player.transform.position;
            player.Move(Vector2.right);
            yield return new WaitForSeconds(.6f);
            player.Move(Vector2.zero);
            Check(Time.time > clock + .5f && Vector3.Distance(position, player.transform.position) > .05f, "world clock and movement continue during phoenix summon");
            Check(Object.FindObjectsOfType<SyringeProjectile>().Any(p => p.IsFlying), "basic attacks continue during summon");
            var phoenix = player.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Phoenix current pose");
            Check(phoenix.enabled && phoenix.color.a > .1f, "wind phoenix visible during live combat");
            var ribbons = player.GetComponentsInChildren<LineRenderer>().Where(r => r.name.StartsWith("Wind wrap ")).ToArray();
            Check(ribbons.Length == 10 && ribbons.All(r => r.enabled), "wind wraps follow live character"); Capture("ashi-live-phoenix");
            float elapsed = skill.CutinElapsed, remaining = skill.ActiveRemaining;
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Approximately(elapsed, skill.CutinElapsed) && Mathf.Approximately(remaining, skill.ActiveRemaining), "real game pause freezes both summon and buff");
            Time.timeScale = 1;
            yield return new WaitForSeconds(1.6f);
            Check(!skill.IsSummoning && skill.Active && !phoenix.enabled && ribbons.All(r => !r.enabled), "summon ends naturally while active buff continues");
            skill.Restore(0, 0, 0); Time.timeScale = .75f;
            Check(skill.TryActivate() && Time.timeScale == .75f, "activation preserves an existing gameplay time scale");
            Time.timeScale = 0; skill.enabled = false;
            Check(Time.timeScale == 0 && !skill.IsSummoning, "cancel during pause cannot unpause the game");
            skill.enabled = true; Time.timeScale = 1; needle.enabled = false; needle.StopAllCoroutines();
            // Actual pooled chest, including its runtime resized Sprite.Create copy.
            Vector2 area = (Vector2)player.transform.position + Vector2.down * 8;
            player.transform.position = area; player.GetComponent<Rigidbody2D>().position = area;
            var chest = entity.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint, area + Vector2.right * .7f);
            yield return new WaitForSeconds(.9f);
            PixelInteractionPrompt.Show(chest, true);
            var prompt = chest.GetComponent<PixelInteractionPrompt>();
            var key = chest.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Pixel E Interaction Prompt");
            var body = chest.GetComponentsInChildren<SpriteRenderer>().First(r => r != key && r.sprite != null && !r.name.ToLowerInvariant().Contains("shadow"));
            var visible = PromptSpriteGeometry.WorldBounds(body);
            Check(Mathf.Abs((key.transform.position.y - visible.max.y) - PixelInteractionPrompt.ObjectGap) < .04f, "item chest keycap sits half an Ashi width above visible art");
            Check(Mathf.Abs(key.transform.position.x - visible.center.x) < .01f, "keycap centers over the visible chest rather than padded sprite pivot");
            Debug.Log("[LiveSkillPrompt] CHEST art=" + body.sprite.name + " rendererTop=" + body.bounds.max.y + " alphaTop=" + visible.max.y + " key=" + key.transform.position);
            Capture("chest-keycap");
            Check(key.enabled, "interaction eligibility still shows chest keycap");
            PixelInteractionPrompt.Show(chest, false); Check(!key.enabled, "keycap hides when interaction is unavailable");
            // Exercise the shared layout with every imported interaction sprite, including
            // negative/non-uniform parent scales and large transparent padding.
            var data = Resources.Load<PromptSpriteGeometry>("Interaction/PromptGeometry");
            Check(data != null && data.entries.Length > 0, "baked prompt alpha bounds exist");
            var sample = new GameObject("Prompt layout sample"); sample.transform.position = (Vector3)area + Vector3.up * 4;
            var sampleBody = sample.AddComponent<SpriteRenderer>(); sample.transform.localScale = new Vector3(-1.7f, .65f, 1);
            foreach (var entry in data.entries)
            {
                sampleBody.sprite = AssetDatabase.LoadAllAssetsAtPath(entry.assetPath).OfType<Sprite>().First(s => s.name == entry.spriteName);
                PixelInteractionPrompt.Show(sampleBody, true);
                var sampleKey = sample.GetComponentsInChildren<SpriteRenderer>().First(r => r != sampleBody);
                var bounds = PromptSpriteGeometry.WorldBounds(sampleBody);
                if (Mathf.Abs(sampleKey.transform.position.y - bounds.max.y - .41f) > .04f || Mathf.Abs(sampleKey.transform.position.x - bounds.center.x) > .01f)
                    throw new Exception("Prompt layout mismatch: " + entry.spriteName);
            }
            Check(true, "all " + data.entries.Length + " baked interaction sprites use the same close spacing with mirrored/scaled owners");
            Object.Destroy(sample);
            yield return CheckAugmentOpacity(player);
            File.WriteAllText(Output + "/result.txt", "PASS live Ashi movement/attacks/phoenix, pause and cancellation, actual chest and " + data.entries.Length + " prompt sprite layouts; augment VFX opacity preview, new/pooled sprite and mesh effects, field exclusion, cancel and persistence.");
            SessionState.SetBool(Key + "Done", true);
        }
        static IEnumerator CheckAugmentOpacity(Character player)
        {
            var original = GamePreferences.Current.Copy();
            var legacy = new GamePreferenceValues(); JsonUtility.FromJsonOverwrite("{\"master\":0.5}", legacy);
            Check(legacy.augmentVfxOpacity == 1 && GamePreferences.Defaults().augmentVfxOpacity == 1, "old saves and defaults start fully opaque");
            var invalid = original.Copy(); invalid.augmentVfxOpacity = float.NaN; GamePreferences.Sanitize(invalid);
            Check(invalid.augmentVfxOpacity == 1, "invalid opacity is sanitized");
            var vfx = SyringeAugmentVfx.Play("DigestiveAcidSacNeedle", player.transform.position + Vector3.right);
            var sprite = vfx.GetComponent<SpriteRenderer>();
            Check(sprite.color.a == 1 && vfx.GetComponent<AugmentVfxTag>() != null, "digestive acid augment puddle is vivid and tagged");
            var noble = SyringeAugmentVfx.Play("MucosalFortress", player.transform.position, player.GetComponentInChildren<SpriteAnimator>().SpriteRenderer);
            var field = new GameObject("Untagged field event fixture"); var fieldSprite = field.AddComponent<SpriteRenderer>(); fieldSprite.sprite = sprite.sprite; fieldSprite.color = new Color(1, 1, 1, .6f);
            var block = new MaterialPropertyBlock();
            var ui = ApothecaryUI.Instance; ui.OpenSettings(); Call(ui, "SwitchTab", 0);
            yield return null;
            var slider = ui.GetComponentsInChildren<UnityEngine.UI.Slider>().Single(s => s.name == "Augment VFX opacity slider");
            slider.value = .25f; yield return null;
            Check(Mathf.Approximately(GamePreferences.Current.augmentVfxOpacity, .25f), "settings slider previews opacity while game is paused");
            sprite.GetPropertyBlock(block); Check(Mathf.Approximately(block.GetFloat("_AugmentOpacity"), .25f), "live pooled sprite uses current slider opacity");
            var mesh = noble.GetComponent<PhoenixRadianceVisual>().FirstRenderer;
            mesh.GetPropertyBlock(block); Check(Mathf.Approximately(block.GetFloat("_AugmentOpacity"), .25f), "painted noble mesh also follows shared tag");
            Check(fieldSprite.color.a == .6f && field.GetComponent<AugmentVfxTag>() == null, "field events remain untouched");
            ScreenCapture.CaptureScreenshot(Output + "/settings-opacity.png"); yield return null; yield return null;
            slider.value = 0; yield return null;
            sprite.GetPropertyBlock(block); Check(block.GetFloat("_AugmentOpacity") == 0 && vfx.gameObject.activeSelf, "zero opacity hides only art without disabling gameplay owner");
            var fresh = SyringeAugmentVfx.Play("Poison", player.transform.position);
            fresh.GetComponent<SpriteRenderer>().GetPropertyBlock(block); Check(block.GetFloat("_AugmentOpacity") == 0, "new effects inherit current value immediately");
            Call(ui, "CancelSettings"); yield return null;
            Check(GamePreferences.Current.augmentVfxOpacity == original.augmentVfxOpacity, "cancel restores pre-preview opacity");
            bool existed = PlayerPrefs.HasKey(GamePreferences.SaveKey); string saved = PlayerPrefs.GetString(GamePreferences.SaveKey);
            try
            {
                var chosen = original.Copy(); chosen.augmentVfxOpacity = .4f; GamePreferences.Apply(chosen, true, false);
                Check(Mathf.Approximately(GamePreferences.Read().augmentVfxOpacity, .4f), "opacity persists and reloads from saved preferences");
            }
            finally
            {
                if(existed)PlayerPrefs.SetString(GamePreferences.SaveKey,saved);else PlayerPrefs.DeleteKey(GamePreferences.SaveKey);
                PlayerPrefs.Save(); GamePreferences.Apply(original,false,false);
            }
            vfx.Release(); noble.Release(); fresh.Release(); Object.Destroy(field);
        }
    }
}
