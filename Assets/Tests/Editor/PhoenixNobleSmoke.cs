using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static partial class PhoenixNobleSmoke
    {
        const string Key = "PhoenixNobleSmoke";
        static bool started;
        static int checks;
        static double deadline;
        static PhoenixNobleSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Fail", false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string trace, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) SessionState.SetBool(Key + "Fail", true); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) SessionState.SetBool(Key + "Fail", true);
            if (SessionState.GetBool(Key + "Done", false) || SessionState.GetBool(Key + "Fail", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool fail = SessionState.GetBool(Key + "Fail", false); SessionState.SetBool(Key, false);
                Debug.Log("PHOENIX_FINISHED failed=" + fail + " checks=" + checks); EditorApplication.Exit(fail ? 1 : 0); return;
            }
            if (!started && EditorApplication.isPlaying && ApothecaryUI.Instance != null)
            {
                started = true;
                var host = new GameObject("Phoenix QA").AddComponent<PhoenixNobleTestHost>();
                Object.DontDestroyOnLoad(host); host.StartCoroutine(Test());
            }
        }
        static void Check(bool ok, string message)
        { if (!ok) throw new Exception("PHOENIX_FAIL " + message); checks++; Debug.Log("PHOENIX_PASS " + message); }
        static IEnumerator Test()
        {
            Application.runInBackground = true;
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            Time.timeScale = 1;
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            Check(PhoenixNobleTheme.Keys.Length == 11, "eleven active noble augments");
            foreach (var key in PhoenixNobleTheme.Keys)
            {
                Check(PhoenixNobleTheme.NameFor(key) != key && !string.IsNullOrEmpty(PhoenixNobleTheme.DescriptionFor(key)), key + " name/description");
                Check(config.augments.Any(a => a.title == PhoenixNobleTheme.NameFor(key)), key + " catalog title");
            }
            Check(PhoenixNobleTheme.DescriptionFor("CursorControl").Contains("8자"), "actual figure-eight gameplay description");
            Check(!PhoenixNobleVfx.Supports("Explosion") && !PhoenixNobleVfx.Supports("Poison") && !PhoenixNobleVfx.Supports("HungerNeedle"), "ordinary weapon VFX untouched");
            var effects = new System.Collections.Generic.List<SyringeAugmentVfx>();
            string[] names = { "LifeBurn", "CloneCulture", "HedgehogBurst", "HeavySnipe", "CursorControl", "NeuralBlock", "OrganCompression", "GastricPeristalsisWave", "MucosalFortress", "HungrySpirit", "NeedleShotgun" };
            var sortObject = new GameObject("QA sort anchor"); var sort = sortObject.AddComponent<SpriteRenderer>();
            sort.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/mini_open.png");
            sort.sortingOrder = 18;
            for (int i = 0; i < names.Length; i++)
            {
                var fx = SyringeAugmentVfx.Play(names[i], new Vector3((i % 4) * 3 - 4.5f, 3 - (i / 4) * 3), sort);
                Check(fx != null, names[i] + " existing prefab leases successfully");
                var so = new SerializedObject(fx); so.FindProperty("worldSpace").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
                fx.transform.position = new Vector3((i % 4) * 3 - 4.5f, 3 - (i / 4) * 3);
                fx.SetWorldSize(Vector2.one * 2.3f, Vector2.one); effects.Add(fx);
                var ink = fx.GetComponent<PhoenixNobleVfx>();
                Check(ink != null && ink.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount > 0, names[i] + " geometry present");
                Check(fx.GetComponentsInChildren<Collider2D>().Length == 0, names[i] + " cosmetic only");
            }
            yield return null; yield return null;
            foreach (var fx in effects)
            {
                var sr = fx.GetComponent<SpriteRenderer>(); var draw = fx.GetComponent<PhoenixNobleVfx>().Drawing;
                Check(sr.forceRenderingOff && draw.enabled && draw.sortingLayerID == sr.sortingLayerID && draw.sortingOrder == sr.sortingOrder, fx.name + " old art hidden, sorting retained");
            }
            Time.timeScale = 0;
            // Unity applies a timeScale change from the following frame, after this frame's LateUpdate.
            yield return null;
            var paused = effects[6].GetComponent<PhoenixNobleVfx>().Drawing.transform.localRotation;
            yield return null; yield return null;
            Check(Quaternion.Angle(paused, effects[6].GetComponent<PhoenixNobleVfx>().Drawing.transform.localRotation) < .001f, "cosmetic animation respects pause");
            Time.timeScale = 1;
            Capture(effects);
            var loopEffect = effects[0]; var geometry = loopEffect.GetComponent<PhoenixNobleVfx>().Drawing;
            loopEffect.Release(); Check(!geometry.gameObject.activeInHierarchy, "release hides child geometry");
            var reused = SyringeAugmentVfx.Play("LifeBurn", Vector3.zero, sort);
            Check(reused == loopEffect && reused.GetComponentsInChildren<MeshRenderer>().Length == 1, "pool reuse keeps single geometry layer");
            reused.Release(); foreach (var fx in effects.Skip(1)) fx.Release(); Object.Destroy(sortObject);
            CrossSceneData.CharacterBlueprint = config.characters[0]; CrossSceneData.ClearStartingLobbyItems();
            Check(StageEntryLoading.Begin(config.characters[0]), "start real level");
            while (StageEntryLoading.IsLoading) yield return null;
            yield return null; yield return null;
            var needle = Object.FindObjectOfType<SyringeDartAbility>(); Check(needle != null, "real needle ability found");
            yield return TestPanels();
            float damage = needle.GetEffectiveDamage(); int count = needle.GetEffectiveProjectileCount();
            needle.EnableHedgehogNeedleLegendary(); yield return null;
            var orbit = Object.FindObjectOfType<HedgehogNeedleController>(); Check(orbit != null, "wing orbit controller created");
            var feather = orbit.GetComponentsInChildren<PhoenixNobleVfx>().First(f => f.Theme == "OrbitFeather");
            Vector3 before = feather.transform.localPosition; Vector3 worldBefore = feather.transform.position - orbit.transform.position;
            yield return new WaitForSeconds(.35f);
            Check(Quaternion.Angle(feather.transform.rotation, Quaternion.identity) < .01f, "orbit feather remains screen upright");
            Check(Vector3.Distance(worldBefore, feather.transform.position - orbit.transform.position) > .005f, "upright feather continues orbiting");
            Check(Vector3.Distance(before, feather.transform.localPosition) < .001f, "orbit radius/anchor unchanged");
            Check(Mathf.Abs(damage - needle.GetEffectiveDamage()) < .001f && count == needle.GetEffectiveProjectileCount(), "base damage and projectile count unchanged");
            SessionState.SetBool(Key + "Done", true);
        }
        static void Capture(System.Collections.Generic.List<SyringeAugmentVfx> effects)
        {
            foreach (var fx in effects) foreach (Transform t in fx.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
            var cam = new GameObject("Phoenix QA camera").AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 5.5f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.42f,.27f,.26f); cam.cullingMask = 1 << 31;
            cam.transform.position = new Vector3(0, 0, -20); var rt = RenderTexture.GetTemporary(1400, 1000, 24); cam.targetTexture = rt; cam.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var pixels = new Texture2D(1400,1000,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1400,1000),0,0); pixels.Apply();
            Directory.CreateDirectory("Library/PhoenixNobleQA"); File.WriteAllBytes("Library/PhoenixNobleQA/effects.png",pixels.EncodeToPNG());
            Object.Destroy(pixels); RenderTexture.active = old; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(cam.gameObject);
        }
    }
    public sealed class PhoenixNobleTestHost : MonoBehaviour {}
}
