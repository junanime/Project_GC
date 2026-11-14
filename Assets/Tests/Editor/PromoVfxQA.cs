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
    // Runs the actual level, checks cosmetic lifetimes and captures individual review stills.
    [InitializeOnLoad]
    public static class PromoVfxQA
    {
        const string Session = "PromoVfxQA";
        const string Output = "Library/PromoVfxQA";
        static double deadline;
        static bool started;
        static int checks;
        static PromoVfxQA() { if (SessionState.GetBool(Session, false)) Attach(); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/errors.txt", "");
            SessionState.SetBool(Session, true);
            SessionState.SetBool(Session + "Done", false);
            SessionState.SetBool(Session + "Fail", false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 600; started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string trace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", message + "\n" + trace + "\n");
            SessionState.SetBool(Session + "Fail", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Session, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) SessionState.SetBool(Session + "Fail", true);
            if (SessionState.GetBool(Session + "Done", false) || SessionState.GetBool(Session + "Fail", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool fail = SessionState.GetBool(Session + "Fail", false); SessionState.SetBool(Session, false);
                File.WriteAllText(Output + "/result.txt", "failed=" + fail + " checks=" + checks);
                Debug.Log("PROMO_VFX_FINISHED failed=" + fail + " checks=" + checks);
                EditorApplication.Exit(fail ? 1 : 0); return;
            }
            if (!started && EditorApplication.isPlaying && ApothecaryUI.Instance != null)
            {
                started = true; var host = new GameObject("Promo VFX QA").AddComponent<PhoenixNobleTestHost>();
                Object.DontDestroyOnLoad(host); host.StartCoroutine(Test());
            }
        }
        static void Check(bool ok, string message)
        { if (!ok) throw new Exception("PROMO_VFX_FAIL " + message); checks++; Debug.Log("PROMO_VFX_PASS " + message); }
        static FieldInfo Field(object o, string name)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new Exception("Missing field " + name);
        }
        static void Set(object o, string n, object value) => Field(o, n).SetValue(o, value);
        static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        static IEnumerator Test()
        {
            Application.runInBackground = true;
            var originalPreferences = GamePreferences.Current.Copy();
            var prefs = originalPreferences.Copy(); prefs.pauseOnFocusLoss = false; prefs.augmentVfxOpacity = 1; prefs.reducedMotion = false;
            GamePreferences.Apply(prefs, false);
            var shader = Resources.Load<Shader>("AugmentVfx/TimeSand");
            Check(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "sand shader compiles");
            var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            CrossSceneData.CharacterBlueprint = config.characters[0]; CrossSceneData.ClearStartingLobbyItems();
            Check(StageEntryLoading.Begin(config.characters[0]), "start actual stage");
            while (StageEntryLoading.IsLoading) yield return null;
            yield return Frames(3);
            var level = Object.FindObjectOfType<LevelManager>(); var player = level.PlayerCharacter;
            var needle = Object.FindObjectOfType<SyringeDartAbility>();
            level.SetRunFlowPaused(true);
            if (level.EntityManager.AbilitySelectionDialog.MenuOpen) level.EntityManager.AbilitySelectionDialog.Close();
            if (TutorialGuide.Instance != null) Set(TutorialGuide.Instance, "ready", false);
            Set(player, "isInvincible", true); needle.enabled = false; Time.timeScale = 1; Time.captureFramerate = 60;
            Check(needle != null && player != null, "live player and needle");
            Camera.main.orthographicSize = 5.5f;
            foreach (var m in Object.FindObjectsOfType<Monster>()) m.gameObject.SetActive(false);
            var blueprint = level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
            level.EntityManager.SpawnMonster(0, (Vector2)player.transform.position + Vector2.right * 1.7f, blueprint, 1000);
            var monster = Object.FindObjectsOfType<Monster>().First();
            var neural = needle.gameObject.AddComponent<NeuralBlockController>();
            neural.Configure(100, 1, .08f, 0, false);
            neural.StartCoroutine((IEnumerator)Call(neural, "SummonThenBlock"));
            yield return Frames(30);
            Check(!neural.IsBlocking, "prelude does not freeze early");
            Check(Object.FindObjectsOfType<PhoenixSummonVisual>().Length == 0, "old phoenix summon removed from time stop");
            yield return Frames(43);
            var visual = needle.GetComponent<NeuralTimeStopVisual>();
            Check(neural.IsBlocking && visual != null && visual.Visible, "hourglass begins with real freeze");
            Check(monster.GetComponent<NeuralBlockedMonsterStatus>().Active, "existing monster frozen");
            Check(!Object.FindObjectsOfType<PhoenixRadianceVisual>().Any(v => v.Key == "NeuralBlock"), "noble does not add old per-enemy feathers");
            Check(visual.Hud.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null, "indicator cannot intercept input");
            Check(visual.Hud.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g => !g.raycastTarget), "all indicator graphics pass clicks");
            Capture("01-time-stop", player, false, visual.Hud);
            Time.timeScale = 0; yield return Frames(2);
            float remaining = visual.Remaining01;
            yield return Frames(6);
            Check(Mathf.Abs(remaining - visual.Remaining01) < .001f, "pause also pauses displayed sand");
            Time.timeScale = 1;
            level.EntityManager.SpawnMonster(0, (Vector2)player.transform.position + Vector2.left * 1.7f, blueprint, 1000);
            yield return Frames(2);
            Check(Object.FindObjectsOfType<Monster>().All(m => m.GetComponent<NeuralBlockedMonsterStatus>() != null && m.GetComponent<NeuralBlockedMonsterStatus>().Active), "new spawn receives remaining freeze");
            Directory.CreateDirectory(Output+"/time-stop");
            for(int f=0;f<20;f++){Capture("time-stop/"+f.ToString("D3"),player,false,visual.Hud);yield return Frames(2);}
            Capture("02-time-stop-ending", player, false, visual.Hud);
            yield return Frames(30);
            Check(!neural.IsBlocking && !visual.Visible, "one-second gameplay freeze and visual tail finish");
            Check(!visual.Hud.gameObject.activeSelf, "hourglass removed after expiry");
            visual.Prepare(player); visual.Begin(1); prefs.augmentVfxOpacity = 0; GamePreferences.Apply(prefs, false);
            yield return Frames(2);
            Check(visual.Hud.GetComponent<CanvasGroup>().alpha == 0, "zero-opacity setting hides indicator");
            prefs.augmentVfxOpacity = 1; prefs.reducedMotion = true; GamePreferences.Apply(prefs, false);
            yield return Frames(2); Check(visual.Remaining01 < 1, "reduced motion preserves remaining-time information");
            neural.enabled = false; yield return null;
            Check(!visual.Visible && !visual.Hud.gameObject.activeSelf, "disable clears effect immediately");
            Object.Destroy(neural); yield return Frames(2);
            Check(needle.GetComponent<NeuralTimeStopVisual>() == null, "removing augment cleans visual component");
            var localStun = monster.GetComponent<NeuralBlockedMonsterStatus>() ?? monster.gameObject.AddComponent<NeuralBlockedMonsterStatus>();
            localStun.Apply(.3f); yield return Frames(2);
            Check(Object.FindObjectsOfType<PhoenixRadianceVisual>().Any(v => v.Key == "NeuralBlock"), "other item stun visuals remain unchanged");
            yield return Frames(22);
            prefs.reducedMotion = false; GamePreferences.Apply(prefs, false);
            foreach (var m in Object.FindObjectsOfType<Monster>()) m.gameObject.SetActive(false);
            var body = SyringeAugmentVfx.FindTarget(player);
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                var feathers = PhoenixHeavyReleaseVisual.Play(player.transform.position + (Vector3)direction * 1.2f, direction, 1, body);
                yield return Frames(7);
                Check(feathers.GetComponentsInChildren<MeshRenderer>().Length == 7, "seven distinct release feathers " + direction);
                Check(feathers.GetComponentsInChildren<Collider2D>().Length == 0, "release is cosmetic only " + direction);
                if (direction == Vector2.right) Capture("03-heavy-feather-release", player, true);
                yield return Frames(20); Check(feathers == null, "release expires without depending on projectile " + direction);
            }
            needle.EnableHeavySnipeLegendary();
            Call(needle, "FireHeavySnipe", 1f); yield return Frames(4);
            var flight = Object.FindObjectsOfType<PhoenixHeavyFlightVisual>().FirstOrDefault(f => f.enabled);
            Check(flight != null && Object.FindObjectOfType<PhoenixHeavyReleaseVisual>() != null, "real heavy shot connects flight and release");
            Capture("04-heavy-flight", player, true);
            Directory.CreateDirectory(Output+"/heavy");
            for(int f=0;f<20;f++){Capture("heavy/"+f.ToString("D3"),player,true);yield return Frames(2);}
            yield return Frames(5); Set(needle, "heavySnipeEnabled", false);

            foreach (string path in new[] { "AugmentVfx/HerbalGround", "AugmentVfx/PhoenixWind" })
            {
                var sh=Resources.Load<Shader>(path);
                Check(sh!=null && sh.isSupported && !ShaderUtil.ShaderHasError(sh), path+" compiles");
            }
            var origin=player.transform.position+Vector3.right*1.3f;
            var suction=new GameObject("QA actual compression field").AddComponent<OrganCompressionField>();
            suction.Init(origin,1.6f,3,.5f,2,1,~0,needle);
            level.EntityManager.SpawnMonster(0,(Vector2)origin+Vector2.right,blueprint,1000);
            var pulled=Object.FindObjectsOfType<Monster>().First();
            float beforeHP=pulled.HP;
            Directory.CreateDirectory(Output+"/suction");
            for(int f=0;f<60;f++){Capture("suction/"+f.ToString("D3"),player,true);yield return Frames(3);}
            yield return Frames(12);
            Check(pulled.HP<beforeHP,"compression retains periodic damage");
            Check(suction==null,"compression field and artwork expire together");
            Check(Object.FindObjectsOfType<HerbalGroundVisual>().Length==0,"compression leaves no orphan visual");
            foreach(var m in Object.FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
            Set(player,"currentHealth",player.MaxHealth*.5f); float health=player.CurrentHealth;
            Ver4GroundEffect.Create(player.transform.position,3,true,0,player,0);
            Directory.CreateDirectory(Output+"/healing");
            for(int f=0;f<60;f++){Capture("healing/"+f.ToString("D3"),player,true);yield return Frames(3);}
            Check(player.CurrentHealth>health,"wood field still heals actual player");
            yield return Frames(12);
            Check(Object.FindObjectsOfType<Ver4GroundEffect>().Length==0,"wood field expires");
            level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*2,blueprint,1000);
            var waveTarget=Object.FindObjectsOfType<Monster>().First();
            float waveHP=waveTarget.HP;
            var wave=player.gameObject.AddComponent<GastricPeristalsisWaveController>();
            wave.Configure(needle,player,~0,100,5.5f,.65f,.35f,Color.white,false);
            Set(wave,"timer",0f);
            wave.StartCoroutine((IEnumerator)Call(wave,"EmitWaveRoutine"));
            Directory.CreateDirectory(Output+"/wave");
            bool tail=false;
            for(int f=0;f<64;f++)
            {
                var wind=Object.FindObjectOfType<PhoenixWindWaveVisual>();
                if(wind!=null && wind.Radius>5.5f)tail=true;
                Capture("wave/"+f.ToString("D3"),player,false);yield return Frames(3);
            }
            Check(waveTarget.HP<waveHP,"actual wave still damages monsters");
            Check(tail,"wind continues beyond original combat radius");
            Check(Object.FindObjectOfType<PhoenixWindWaveVisual>()==null,"wind dissipates after reaching screen corners");
            Check(!Object.FindObjectsOfType<PhoenixSummonVisual>().Any(),"wave no longer summons old bird");
            var portrait=PhoenixWindWaveVisual.Play(player,5.5f,.65f);
            Camera.main.aspect=.5625f; yield return Frames(2);
            Check(portrait.ScreenReach>=Camera.main.orthographicSize,"portrait edge covered");
            Camera.main.aspect=2.4f;yield return Frames(2);
            Check(portrait.ScreenReach>=Camera.main.orthographicSize*Mathf.Sqrt(1+2.4f*2.4f),"ultrawide farthest corner covered");
            Check(portrait.GetComponentsInChildren<Collider2D>().Length==0,"screen-reaching tail creates no damage collider");
            Time.timeScale=0;float pausedRadius=portrait.Radius;yield return Frames(3);
            Check(Mathf.Abs(portrait.Radius-pausedRadius)<.001f,"wind respects pause");Time.timeScale=1;
            wave.enabled=false;yield return Frames(2);
            Check(Object.FindObjectOfType<PhoenixWindWaveVisual>()==null,"disabling wave clears wind tails");
            Object.Destroy(wave);
            Time.captureFramerate = 0; GamePreferences.Apply(originalPreferences, false);
            SessionState.SetBool(Session + "Done", true);
        }
        static void Capture(string name, Character player, bool close, Canvas overlay = null)
        {
            var source = Camera.main; var go = new GameObject("QA still camera"); var camera = go.AddComponent<Camera>(); camera.CopyFrom(source);
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = close ? 2.2f : source.orthographicSize;
            camera.transform.position = new Vector3(player.transform.position.x + (close ? .8f : 0), player.transform.position.y + (close ? .4f : 0), source.transform.position.z);
            camera.transform.rotation = source.transform.rotation;
            if (overlay != null) { overlay.renderMode = RenderMode.ScreenSpaceCamera; overlay.worldCamera = camera; overlay.planeDistance = 1; Canvas.ForceUpdateCanvases(); }
            var rt = RenderTexture.GetTemporary(1280, 720, 24); camera.targetTexture = rt; camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
            if (overlay != null) { overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.worldCamera = null; }
            Object.Destroy(pixels); RenderTexture.active = old; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(go);
        }
    }
}
