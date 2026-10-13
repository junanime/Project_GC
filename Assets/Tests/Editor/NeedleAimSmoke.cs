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
    public static class NeedleAimSmoke
    {
        const string Key = "NeedleAimSmoke";
        const string Output = "Library/NeedleAimProof";
        static double deadline;
        static bool started;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static NeedleAimSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys = catalog.entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct().ToArray();
            File.WriteAllText(Output + "/prefs-backup.json", JsonUtility.ToJson(new Backup { values = keys.Select(k => new Saved { key = k, exists = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            foreach (var e in catalog.entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id), 1);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            var view = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(view).position = new Rect(0, 0, 1280, 741);
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
                Debug.Log("[NeedleAim] FINISHED failed=" + failed); EditorApplication.Exit(failed ? 1 : 0); return;
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (!started && EditorApplication.isPlaying && level != null && level.PlayerCharacter != null && level.CurrentLevelTime > .1f)
            { started = true; level.StartCoroutine(Checks(level)); }
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("[NeedleAim] FAIL " + message); Debug.Log("[NeedleAim] PASS " + message); }
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
        static SyringeProjectile[] Flying() => Object.FindObjectsOfType<SyringeProjectile>().Where(p => p.IsFlying).ToArray();
        static SyringeProjectile Fire(SyringeDartAbility needle, Vector2 direction)
        {
            var before = Flying();
            Check((bool)Call(needle, "LaunchAimedSyringe", direction, false, 1f, 0), "launch succeeds");
            return Flying().Except(before).Single();
        }
        static void ClearShots()
        {
            foreach (var p in Flying()) typeof(Projectile).GetMethod("HitNothing", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(p, null);
        }
        static IEnumerator Checks(LevelManager level)
        {
            yield return null;
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            Application.runInBackground = true; Time.timeScale = 1; Time.captureDeltaTime = 1f / 60f;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            foreach (var b in Object.FindObjectsOfType<MonoBehaviour>())
                if (b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner) { b.enabled = false; b.StopAllCoroutines(); }
            level.enabled = false;
            var entity = level.EntityManager; entity.enabled = false; entity.StopAllCoroutines();
            var player = level.PlayerCharacter; Set(player, "isInvincible", true); player.CollectableCollider.enabled = false;
            var manager = Object.FindObjectOfType<AbilityManager>();
            var needle = SyringeAbilityResolver.FindOwnedOrFirst(manager); needle.enabled = false; needle.StopAllCoroutines();
            foreach (var m in entity.LivingMonsters.ToArray()) m.gameObject.SetActive(false);
            Set(needle, "homingEnabled", false); Set(needle, "lifeBurnEnabled", false); Set(needle, "bipolarNeedleEnabled", false);
            Set(player, "rangeMultiplier", 1f);
            float width = player.Blueprint.idleSpriteSequence[0].bounds.size.x * player.GetComponentInChildren<SpriteAnimator>().transform.lossyScale.x;
            Check(Mathf.Abs(needle.GetEffectiveSyringeMaxDistance() / width - (10f/3f)) < .3f, "base range approximately three-and-one-third live Ashi widths: " + width);
            Check(Mathf.Abs(needle.GetCurrentSpecialRuntime().homingRange / width - 4f) < .3f, "detection approximately four Ashi widths");
            Set(player, "rangeMultiplier", 1.5f);
            Check(Mathf.Abs(needle.GetEffectiveSyringeMaxDistance() - 4.1f) < .01f, "range upgrade extends flight");
            Check(Mathf.Abs(needle.GetCurrentSpecialRuntime().homingRange - 3.28f) < .01f, "flight upgrade does not multiply detection");
            Set(player, "rangeMultiplier", 1f);
            Vector2 origin = needle.GetReturnCatchPosition(Vector2.right);
            int idx = Array.FindIndex(level.CurrentLevelBlueprint.monsters, c => c.monstersPrefab.GetComponent<AcidToadMonster>() != null);
            var bp = level.CurrentLevelBlueprint.monsters[idx].monsterBlueprints[0];
            var target = (AcidToadMonster)entity.SpawnMonster(idx, origin + Vector2.up * 2, bp, 0, false);
            target.AutoPatterns = false; target.enabled = false; target.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            void Place(Vector2 center) { target.transform.position += (Vector3)(center - NeedleTargeting.Position(target)); Physics2D.SyncTransforms(); }
            Place(origin + Vector2.up * 2);
            var auto = (Vector2)Call(needle, "GetAutomaticNeedleDirection");
            Check(Vector2.Dot(auto, Vector2.up) > .999f, "automatic aim finds valid in-range enemy independent of movement");
            Place(origin + Vector2.left * 8);
            Check(Vector2.Dot((Vector2)Call(needle, "GetAutomaticNeedleDirection"), auto) > .999f, "out-of-range enemies ignored; last direction retained");
            Place(origin + Vector2.right * 2);
            Set(target, "currentHealth", 0f);
            Check(!NeedleTargeting.IsValid(target, needle.MonsterLayer), "dead target excluded");
            Set(target, "currentHealth", 2500f);
            target.BodyHitbox.enabled = false;
            Check(!NeedleTargeting.IsValid(target, needle.MonsterLayer), "hidden/untargetable body excluded");
            target.BodyHitbox.enabled = true;
            Place(origin + Vector2.up * 2);
            ClearShots();
            Set(needle, "pendingNeedleDirection", Vector2.left); Set(needle, "hasPendingNeedleDirection", true);
            needle.FireItemEcho(Vector2.right, .5f);
            Check((bool)Get(needle, "hasPendingNeedleDirection"), "item echo cannot consume next basic needle override");
            ClearShots();
            var volley = (IEnumerator)Call(needle, "LaunchSyringes");
            var beforeVolley = Flying(); Check(volley.MoveNext(), "actual volley emits first needle");
            var firstVolley = Flying().Except(beforeVolley).Single();
            Check(Vector2.Dot(firstVolley.VisualFlightDirection, Vector2.left) > .999f, "actual multishot volley uses manual direction once");
            beforeVolley = Flying(); Check(volley.MoveNext(), "actual volley emits second needle");
            var secondVolley = Flying().Except(beforeVolley).Single();
            Check(Vector2.Dot(secondVolley.VisualFlightDirection, Vector2.up) > .95f, "remaining volley resumes auto aim with spread");
            var homingAbility = manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).First(a => a.Type == SyringeSpecialAugmentAbility.SpecialAugmentType.Homing);
            Check(homingAbility.Description.Contains("발사 직후") && homingAbility.Description.Contains("4개"), "live selection/panel description reflects immediate homing and four-width detection");
            ClearShots(); Place(origin + Vector2.right * 2);
            // Real Input System press edges, including a field tap while another finger holds UI.
            var mode = InputSystem.settings.updateMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            var mouse = InputSystem.AddDevice<Mouse>();
            var screen = InputSystem.AddDevice<Touchscreen>();
            Vector2 click = Camera.main.WorldToScreenPoint(origin + Vector2.up * 2);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = click }.WithButton(MouseButton.Left)); InputSystem.Update();
            Call(needle, "ReadNeedleAimInput");
            Check((bool)Get(needle, "hasPendingNeedleDirection"), "field mouse press queues one shot");
            ClearShots(); var manual = Fire(needle, Vector2.right);
            Check(Vector2.Dot(manual.VisualFlightDirection, Vector2.up) > .99f && !(bool)Get(needle, "hasPendingNeedleDirection"), "manual direction consumed by exactly one needle");
            var next = Fire(needle, Vector2.right);
            Check(Vector2.Dot(next.VisualFlightDirection, Vector2.right) > .999f, "next needle resumes automatic direction");
            InputSystem.Update(); Call(needle, "ReadNeedleAimInput");
            Check(!(bool)Get(needle, "hasPendingNeedleDirection"), "held mouse does not repeat manual override");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = click }); InputSystem.Update();
            var canvasGo = new GameObject("Aim UI exclusion", typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var imageGo = new GameObject("UI touch area", typeof(RectTransform), typeof(UnityEngine.UI.Image)); imageGo.transform.SetParent(canvasGo.transform, false);
            var rect = imageGo.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Check(UnityEngine.EventSystems.EventSystem.current != null, "scene has active UI event system");
            Debug.Log("[NeedleAim] UI probe " + imageGo.GetComponent<UnityEngine.UI.Image>().depth + " " + rect.rect + " click=" + click);
            Call(needle, "QueueNeedleAim", click);
            Check(!(bool)Get(needle, "hasPendingNeedleDirection"), "UI raycast blocks manual field aiming");
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = 11, phase = UnityEngine.InputSystem.TouchPhase.Began, position = click }); InputSystem.Update();
            Call(needle, "ReadNeedleAimInput");
            Check(!(bool)Get(needle, "hasPendingNeedleDirection"), "touch on UI cannot queue needle");
            Object.DestroyImmediate(canvasGo);
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = 12, phase = UnityEngine.InputSystem.TouchPhase.Began, position = click }); InputSystem.Update();
            Call(needle, "ReadNeedleAimInput");
            Check((bool)Get(needle, "hasPendingNeedleDirection"), "second finger field tap queues independently of held UI finger");
            Time.timeScale = 0; Call(needle, "ReadNeedleAimInput");
            Check(!(bool)Get(needle, "hasPendingNeedleDirection"), "paused overlay cannot leave stale manual aim"); Time.timeScale = 1;
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = 11, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = click });
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = 12, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = click });
            InputSystem.Update(); InputSystem.settings.updateMode = mode;
            // Keep test devices alive until this isolated Editor process exits: the UI
            // module still owns its pointer records until it has processed their releases.
            ClearShots(); Place(origin + Vector2.up * 2);
            var plain = Fire(needle, Vector2.right); ((Collider2D)Get(plain, "col")).enabled = false;
            Set(plain, "speed", 1f);
            yield return new WaitForSeconds(.2f);
            Check(Vector2.Dot(plain.VisualFlightDirection, Vector2.right) > .9999f, "basic needle never retargets after firing");
            ClearShots(); Set(needle, "homingEnabled", true);
            var homing = Fire(needle, Vector2.right); ((Collider2D)Get(homing, "col")).enabled = false; Set(homing, "speed", 1f);
            Check(homing.VisualFlightDirection.y > .01f, "homing curves immediately from launch");
            yield return new WaitForSeconds(.2f);
            Check(homing.VisualFlightDirection.y > .5f, "homing follows target in early flight");
            Vector2 previous = homing.VisualFlightDirection;
            Place((Vector2)homing.transform.position + Vector2.left * 2);
            yield return new WaitForSeconds(.2f);
            Check(homing.VisualFlightDirection.x < -.5f && Vector2.Dot(previous, homing.VisualFlightDirection) < .95f, "homing follows moving target with a curved turn");
            Check(Mathf.Abs(NeedleTargeting.Curve(Vector2.right, Vector2.left, 8, .016f).magnitude - 1f) < .001f, "opposite target cannot stall arrow");
            Place((Vector2)homing.transform.position + Vector2.up * 9);
            yield return null;
            Check(Get(homing, "ver4HomingTarget") == null, "homing releases target beyond detection range");
            ClearShots(); Set(needle, "homingEnabled", false); target.gameObject.SetActive(false);
            var expiring = Fire(needle, Vector2.down);
            Vector2 start = needle.GetReturnCatchPosition(Vector2.down);
            float deadline = Time.time + 5;
            while (expiring.IsFlying && Time.time < deadline) yield return null;
            Check(!expiring.IsFlying && Mathf.Abs(Vector2.Distance(start, expiring.transform.position) - 2.733333f) < .03f, "needle expires at exact normal range");
            var particles = (ParticleSystem)Get(expiring, "destructionParticleSystem");
            Check(particles != null && particles.isPlaying, "original range-end disappearance VFX plays");
            Set(needle, "homingEnabled", true);
            var noTarget = Fire(needle, Vector2.down);
            deadline = Time.time + 5;
            while (noTarget.IsFlying && Time.time < deadline) yield return null;
            Check(!noTarget.IsFlying && Vector2.Distance(start, noTarget.transform.position) < 2.77f, "homing does not gain unintended extra flight range without a target");
            File.WriteAllText(Output + "/result.txt", "PASS: live Ashi scale, targeting, mouse/touch UI exclusion, one-shot override, straight flight, curved homing, range expiration and VFX.");
            SessionState.SetBool(Key + "Done", true);
        }
    }
}
