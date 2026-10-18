using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Vampire.Editor
{
    [InitializeOnLoad]
    public static class CharacterExitChecks
    {
        const string Key = "CharacterExitChecks", Output = "Library/CharacterExitProof";
        static readonly string[] Names = { "ashi", "ari", "hyuki", "shini" };
        static readonly string[] Blueprints = { "Main", "Ari", "Hyuki", "Shini" };
        static bool started;
        static double deadline;
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        static CharacterExitChecks() { if (SessionState.GetBool(Key, false)) Attach(); }

        [MenuItem("Tools/Character exits/Import and verify all four in Level 1")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            foreach (var name in Names)
            {
                string path = "Assets/Resources/CharacterExits/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default; imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 2048;
                imp.mipmapEnabled = false; imp.filterMode = FilterMode.Point; imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = true; imp.isReadable = false;
                imp.SaveAndReimport();
            }
            Restore();
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys = catalog.entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct();
            File.WriteAllText(Output + "/prefs.json", JsonUtility.ToJson(new Backup { values = keys.Select(k => new Saved { key = k, exists = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            foreach (var entry in catalog.entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(entry.id), 1);
            File.WriteAllText(Output + "/checks.txt", "");
            File.WriteAllText(Output + "/errors.txt", "");
            SessionState.SetInt(Key + "Index", 0); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            SelectCharacter(0);
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position = new Rect(0, 0, 1280, 761);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void SelectCharacter(int i)
        {
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/" + Blueprints[i] + " Character Blueprint.asset");
            CrossSceneData.ClearStartingLobbyItems();
        }
        static void Restore()
        {
            if (!File.Exists(Output + "/prefs.json")) return;
            foreach (var s in JsonUtility.FromJson<Backup>(File.ReadAllText(Output + "/prefs.json")).values)
            { if (s.exists) PlayerPrefs.SetInt(s.key, s.value); else PlayerPrefs.DeleteKey(s.key); }
            PlayerPrefs.Save(); File.Delete(Output + "/prefs.json");
        }
        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 300; started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", text + "\n" + stack + "\n");
            SessionState.SetBool(Key + "Failed", true); SessionState.SetBool(Key + "Done", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { Log("Exit checks timed out", "", LogType.Error); }
            if (SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Restore(); SessionState.SetBool(Key, false);
                bool failed = SessionState.GetBool(Key + "Failed", false);
                File.WriteAllText(Output + "/result.txt", failed ? "FAIL" : "PASS");
                Debug.Log("[CharacterExit] FINISHED failed=" + failed);
                EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (!started && EditorApplication.isPlaying && level != null && level.PlayerCharacter != null &&
                CharacterExitSequence.ClipName(level.PlayerCharacter) == Names[SessionState.GetInt(Key + "Index", 0)] && level.GetComponent<FiniteStomachMap>() != null)
            { started = true; level.StartCoroutine(CheckCharacter(level, SessionState.GetInt(Key + "Index", 0))); }
        }
        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("[CharacterExit] FAIL " + message);
            File.AppendAllText(Output + "/checks.txt", "PASS " + message + "\n");
        }
        static void Set(Character actor, string field, object value)
        { typeof(Character).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, value); }
        static void ReadyForDamage(Character actor)
        { Set(actor, "isInvincible", false); Set(actor, "hasShield", false); Set(actor, "invincibleDuringDash", false); Set(actor, "dashInvincibleEndTime", -1f); }
        static void Capture(Camera camera, string file, int w = 480, int h = 480)
        {
            var target = RenderTexture.GetTemporary(w, h, 24); var prev = camera.targetTexture; var active = RenderTexture.active;
            var texture = new Texture2D(w, h, TextureFormat.RGB24, false);
            try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, w, h), 0, 0); texture.Apply(); File.WriteAllBytes(file, texture.EncodeToPNG()); }
            finally { camera.targetTexture = prev; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); Object.Destroy(texture); }
        }
        static IEnumerator CheckCharacter(LevelManager level, int index)
        {
            Application.runInBackground = true;
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(2);
            Time.timeScale = 1;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            foreach (var b in Object.FindObjectsOfType<MonoBehaviour>())
                if (b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner || b is SupplyCapsuleDirector) { b.enabled = false; b.StopAllCoroutines(); }
            level.enabled = false; level.EntityManager.enabled = false; level.EntityManager.StopAllCoroutines();
            foreach (var m in level.EntityManager.LivingMonsters.ToArray()) m.gameObject.SetActive(false);
            foreach (var c in Object.FindObjectsOfType<Collectable>()) c.gameObject.SetActive(false);
            var actor = level.PlayerCharacter; actor.Move(Vector2.zero);
            var original = actor.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
            Check(CharacterExitSequence.ClipName(actor) == Names[index], Names[index] + " selects correct clip");
            if (index == 2) Check(actor.Blueprint.idleSpriteSequence[0].texture.name == "Hyuki_ColdIdle", "Hyuki uses Level1 ColdIdle reference");
            actor.AddReviveCount(1); ReadyForDamage(actor); actor.TakeDamage(100000);
            Check(actor.IsAlive && actor.CurrentHealth > 0 && actor.GetComponent<CharacterExitSequence>() == null && !level.IsLevelEnded, Names[index] + " revival precedes exit sequence");
            yield return new WaitForSecondsRealtime(.7f);
            int completed = 0; actor.OnDeath.AddListener(() => completed++);
            if (index == 1) actor.Skills.TryActivate();
            if (index == 0) { actor.AddDashCharge(1); actor.TryDash(); }
            var before = actor.transform.position; var originalScale = actor.transform.localScale;
            string folder = Output + "/" + Names[index]; Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(folder + "/before.png"); yield return null; yield return null;
            before = actor.transform.position;
            ReadyForDamage(actor); actor.TakeDamage(100000);
            yield return null;
            var exit = actor.GetComponent<CharacterExitSequence>();
            Check(!actor.IsAlive && actor.CurrentHealth == 0 && exit != null && exit.IsPlaying && !level.IsLevelEnded && completed == 0, Names[index] + " HP0 begins exit before result");
            level.GameOver(); level.LevelPassed(null);
            Check(!level.IsLevelEnded, Names[index] + " early result and simultaneous clear cannot bypass exit");
            actor.TakeDamage(100000); actor.GainHealth(100);
            Check(actor.CurrentHealth == 0 && !actor.TryDash() && !actor.Skills.TryActivate(), Names[index] + " dead input and healing blocked");
            Check(Object.FindObjectsOfType<SyringeProjectile>().Length == 0, Names[index] + " lingering player needles released before exit");
            var cameraObject = new GameObject("Exit proof camera"); var camera = cameraObject.AddComponent<Camera>(); camera.CopyFrom(Camera.main); camera.enabled = false;
            camera.orthographicSize = Mathf.Max(.9f, exit.BodyHeight * 1.7f); camera.aspect = 1;
            camera.transform.position = new Vector3(before.x, before.y + exit.BodyHeight * .5f, Camera.main.transform.position.z);
            var visualScale = exit.Visual.transform.localScale; int frame = 0; float start = Time.realtimeSinceStartup, next = start;
            while (!exit.Completed)
            {
                Check(!level.IsLevelEnded && completed == 0, Names[index] + " result waits at frame " + exit.FrameIndex);
                if (Time.realtimeSinceStartup >= next)
                { Capture(camera, folder + "/frame-" + (frame++).ToString("D3") + ".png"); next = Time.realtimeSinceStartup + .1f; }
                Check(exit.Visual.transform.localScale == visualScale, Names[index] + " fixed clip scale " + exit.FrameIndex);
                if (Time.realtimeSinceStartup - start > 8) throw new Exception("Exit animation stalled");
                yield return null;
            }
            yield return null;
            Check(level.IsLevelEnded && completed == 1 && ApothecaryUI.Instance.Page == "result", Names[index] + " completed exit opens failure panel exactly once");
            Check(!original.enabled && actor.transform.localScale == originalScale && Vector3.Distance(before, actor.transform.position) < .01f, Names[index] + " original hidden and player scale/anchor preserved");
            Check(exit.Visual.enabled == (index == 0 || index == 2), Names[index] + " surrender/ice remains; bird/fire exits disappear");
            Check(!actor.transform.Find("Weapon").gameObject.activeSelf, Names[index] + " held weapon cannot obscure exit");
            var shadow = actor.transform.Find("Circle").GetComponent<SpriteRenderer>();
            Check(index == 0 || index == 2 || !shadow.enabled, Names[index] + " disappearing body leaves no shadow");
            ScreenCapture.CaptureScreenshot(folder + "/result.png"); yield return null; yield return null;
            File.WriteAllText(folder + "/measurements.txt", "Body world height=" + exit.BodyHeight + "\nExit scale=" + visualScale + "\nElapsed=" + exit.Elapsed + "\n");
            Object.Destroy(cameraObject);
            if (index == 3) SessionState.SetBool(Key + "Done", true);
            else { SessionState.SetInt(Key + "Index", index + 1); SelectCharacter(index + 1); started = false; Time.timeScale = 1; SceneManager.LoadScene("Level 1"); }
        }
    }
}
