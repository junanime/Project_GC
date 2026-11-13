#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
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
    /// <summary>Real play-mode finale, dash and stage-transfer acceptance checks.</summary>
    [InitializeOnLoad]
    public static class OralGalleryDetailCapture
    {
        const string Key = "OralGalleryDetailCapture", Root = "Library/OralGalleryDetailProof";
        static readonly string[] Names = { "ashi", "ari", "hyuki", "shini" };
        static readonly string[] Blueprints = { "Main", "Ari", "Hyuki", "Shini" };
        static int Index => SessionState.GetInt(Key + "Index", 0);
        static string Output => Root + "/" + Index + "-" + Names[Index];
        static bool started; static double deadline;
        static Vector2? captureFocus; static float shotSize = 6;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }

        static OralGalleryDetailCapture() { if (SessionState.GetBool(Key, false)) Attach(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedSelection()
        {
            if (!SessionState.GetBool(Key, false)) return;
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/" + Blueprints[Index] + " Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root); RestorePrefs();
            File.WriteAllText(Root + "/result.txt", "RUNNING"); File.WriteAllText(Root + "/checks.txt", ""); File.WriteAllText(Root + "/errors.txt", "");
            var keys = new[] { "Coins", "LobbySilverCoins" };
            File.WriteAllText(Root + "/prefs.json", JsonUtility.ToJson(new Backup { prefs = keys.Select(k => new Pref { key = k, had = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            File.WriteAllText(Root + "/capture-method.txt", "Actual Unity Level 1 scene, selected production CharacterBlueprint, actual runtime and Camera.Render. Controlled placement, game-clock seek and explicit per-frame full health are QA fixtures only; percentage core damage bypasses ordinary invincibility. Actual Character.TryDash executes the recorded escapes. Full-flow review uses a wider camera; close-ups use the same production renderer. Fixed 15 simulation frames per second while recording; no generated or interpolated frames. PlayerPrefs restored on completion.\n");
            var arg = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("-swallowCharacter="));
            int index = arg == null ? 0 : int.Parse(arg.Substring("-swallowCharacter=".Length), CultureInfo.InvariantCulture);
            if (index < 0 || index >= Names.Length) throw new ArgumentOutOfRangeException("swallowCharacter");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Done", false);
            SessionState.SetInt(Key + "Index", index); SessionState.SetInt(Key + "Checks", 0); Directory.CreateDirectory(Output);
            CrossSceneData.ClearPendingRunSceneTransfer(); SeedSelection();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity"); Attach(); EditorApplication.EnterPlaymode();
        }
        static void RestorePrefs()
        {
            string file = Root + "/prefs.json"; if (!File.Exists(file)) return;
            foreach (var p in JsonUtility.FromJson<Backup>(File.ReadAllText(file)).prefs)
                if (p.had) PlayerPrefs.SetInt(p.key, p.value); else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save(); File.Delete(file);
        }
        static void Attach()
        {
            started = false; deadline = EditorApplication.timeSinceStartup + 1800;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string text, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText(Root + "/errors.txt", text + "\n" + stack + "\n"); SessionState.SetBool(Key + "Failed", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Root + "/errors.txt", "Deadline exceeded\n"); SessionState.SetBool(Key + "Failed", true); }
            if (SessionState.GetBool(Key + "Failed", false) || SessionState.GetBool(Key + "Done", false))
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                bool failed = SessionState.GetBool(Key + "Failed", false);
                string result = (failed ? "FAIL" : "PASS") + " checks=" + SessionState.GetInt(Key + "Checks", 0) + " character=" + Names[Index];
                File.WriteAllText(Root + "/result.txt", result); File.WriteAllText(Output + "/result.txt", result);
                File.Copy(Root + "/checks.txt", Output + "/checks.txt", true); File.Copy(Root + "/errors.txt", Output + "/errors.txt", true);
                RestorePrefs(); Time.timeScale = 1; Time.captureFramerate = 0; SessionState.SetBool(Key, false);
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true; level.StartCoroutine(Checks(level));
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("ORAL_SWALLOW_FAIL " + Names[Index] + ": " + message);
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            File.AppendAllText(Root + "/checks.txt", "PASS " + message + "\n"); Debug.Log("ORAL_SWALLOW_PASS " + message);
        }
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new MissingFieldException(owner.GetType().Name, name);
        }
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static void Place(Character player, Vector2 position)
        {
            OralStageRuntime.Instance.TeleportPlayer(position, OralZone.Tongue); player.Move(Vector2.zero); Physics2D.SyncTransforms();
        }
        static void Clear(LevelManager level)
        {
            foreach (var m in level.EntityManager.LivingMonsters.ToArray()) if (m != null) m.DespawnForStageTransfer();
            foreach (var p in Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();
            foreach (var c in Object.FindObjectsOfType<Collectable>()) c.DissolveInAcidLake();
        }
        static void CloseDialogs(LevelManager level)
        {
            if (level.EntityManager.AbilitySelectionDialog.MenuOpen) { level.EntityManager.AbilitySelectionDialog.Close(); Time.timeScale = 1; }
        }

        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Gallery detail retakes");
            var progression=StageProgression.Ensure(level);Check(progression.BeginDirectOral(),"production oral entry");while(OralStageRuntime.Instance==null||progression.Travelling)yield return null;
            var runtime=OralStageRuntime.Instance;var events=OralFieldEvents.Instance;var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;events.AutoSchedule=false;
            level.enabled=false;foreach(var x in Object.FindObjectsOfType<MonoBehaviour>())if(x is StageEventDirector||x is TimedSpecialMonsterSpawner||x is AcidToadSpawn||x is NPCSpawner||x is SnailBossDebugSpawn){x.enabled=false;x.StopAllCoroutines();}
            foreach(var x in Object.FindObjectsOfType<Ability>()){x.enabled=false;x.StopAllCoroutines();}Clear(level);Set(player,"isInvincible",true);
            var c=events.Cavities.First();foreach(var q in events.Cavities.Skip(1)){q.Healed=true;q.Active=false;}runtime.TeleportPlayer(c.Position,OralZone.Teeth);captureFocus=c.Position;shotSize=4.8f;
            float began=Time.time;bool triggered=false;yield return Record("cavity",()=>Time.time-began<12,()=>{CloseDialogs(level);Set(player,"currentHealth",player.MaxHealth);if(!triggered&&Time.time-began>2){Check(events.TryStartTreatment(0),"treatment starts with original bound visual");triggered=true;}},180);Check(c.Healed,"cavity visibly healed");
            foreach(var q in events.Cavities){q.Healed=true;q.Active=false;}Clear(level);var center=OralMapGeometry.TongueCenter(map);Place(player,center);
            var taste=OralTasteEvents.Instance;Check(taste.Begin(OralTasteKind.Sweet),"honey begins");var honey=taste.HoneyPositions.First();captureFocus=honey+Vector2.down*.5f;shotSize=3.3f;Place(player,honey+Vector2.down*2.5f);
            for(int i=0;i<5;i++)events.SpawnFinaleMonster(honey+new Vector2(Mathf.Cos(i*1.25f),Mathf.Sin(i*1.25f))*2.1f);began=Time.time;yield return Record("honey",()=>Time.time-began<10,()=>{CloseDialogs(level);Set(player,"currentHealth",player.MaxHealth);},140);Check(taste.HoneyFedCount>0,"monsters visibly feed");
            taste.CancelAll();Clear(level);captureFocus=center;shotSize=2.9f;player.CollectableCollider.enabled=false;
            var flat=level.CurrentLevelBlueprint.monsters.SelectMany(g=>g.monsterBlueprints).ToArray();
            foreach(bool elite in new[]{false,true})
            {
                Clear(level);Place(player,center+Vector2.down*1.2f);
                var types=elite?flat.Where(OralMonsterRoster.IsEliteBlueprint).GroupBy(b=>((EliteMonsterBlueprint)b).sourceNormalBlueprint).Select(g=>g.First()).Take(6).ToArray():flat.Where(OralMonsterRoster.IsBlueprint).Take(6).ToArray();
                Check(types.Length==6,elite?"six actual oral elite designs":"six actual oral normal designs");
                for(int i=0;i<types.Length;i++)
                {
                    var bp=types[i];int pool=Array.FindIndex(level.CurrentLevelBlueprint.monsters,g=>g.monsterBlueprints.Contains(bp));
                    var point=center+new Vector2((i-2.5f)*1.05f,.9f);var monster=level.EntityManager.SpawnMonster(pool,point,bp);
                    Check(monster!=null,"gallery roster spawn "+bp.name);
                }
                began=Time.time;yield return Record(elite?"oral-elites":"oral-normals",()=>Time.time-began<4.5f,()=>{CloseDialogs(level);Set(player,"currentHealth",player.MaxHealth);},100);
            }
            Clear(level);
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator DenseEscapeChecks(LevelManager level, OralSwallowFinale finale)
        {
            var player = level.PlayerCharacter; var center = OralMapGeometry.TongueCenter(FiniteStomachMap.Instance);
            var states = finale.Whirlpools.ToArray();
            var saved = states.Select(w => new { w.Position, w.Radius, w.Angle, Direction = (Vector2)Field(w, "Direction").GetValue(w), Turn = (float)Field(w, "TurnRemaining").GetValue(w) }).ToArray();
            float normalCap = Physics2D.maxTranslationSpeed; var rows = new string[2];
            try
            {
                for (int i = 0; i < states.Length; i++)
                {
                    states[i].Position = center + (i < 7 ? new Vector2(-12 + i * 4, -4) : new Vector2(-2 + (i - 7) * .3f, 7));
                    states[i].Radius = i < 7 ? 2.5f : .2f; Set(states[i], "Direction", Vector2.zero); Set(states[i], "TurnRemaining", 999f);
                }
                for (int mode = 0; mode < 2; mode++)
                {
                    Physics2D.maxTranslationSpeed = mode == 0 ? normalCap : 1f;
                    Place(player, center + new Vector2(-13, -4)); Set(player, "currentDashCharges", player.MaxDashCharges); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth);
                    Vector2 start = player.Position;
                    Check(finale.TryGetDashExit(player, Vector2.right, out var target), "overlapping-chain fixture supplies a real vortex escape target");
                    float planned = Vector2.Distance(start, target);
                    Check(planned < 10f, "overlapping-chain escape chooses a nearby union boundary instead of a field-length launch");
                    int charges = player.CurrentDashCharges; player.Move(Vector2.right); player.TryDash(); player.Move(Vector2.zero);
                    Check(player.IsDashing, "overlapping-chain actual dash starts with physics cap " + Physics2D.maxTranslationSpeed);
                    float until = Time.realtimeSinceStartup + 5;
                    while (player.IsDashing && Time.realtimeSinceStartup < until) { CloseDialogs(level); yield return null; }
                    Check(!player.IsDashing && player.CurrentDashCharges == charges - 1, "overlapping-chain escape completes using one charge");
                    Check(states.All(w => Vector2.Distance(player.Position, w.Position) > w.Radius), "overlapping-chain actual endpoint is outside every vortex despite physics speed cap " + Physics2D.maxTranslationSpeed);
                    rows[mode] = "{\"physicsCap\":" + F(Physics2D.maxTranslationSpeed) + ",\"plannedDistance\":" + F(planned) + ",\"actualDistance\":" + F(Vector2.Distance(start, player.Position)) + "}";
                }
            }
            finally
            {
                Physics2D.maxTranslationSpeed = normalCap; player.Move(Vector2.zero);
                for (int i = 0; i < states.Length; i++) { states[i].Position = saved[i].Position; states[i].Radius = saved[i].Radius; states[i].Angle = saved[i].Angle; Set(states[i], "Direction", saved[i].Direction); Set(states[i], "TurnRemaining", saved[i].Turn); }
            }
            Check(Physics2D.maxTranslationSpeed == normalCap, "dense-chain fixture restores the original global physics cap");
            File.WriteAllText(Output + "/dense-chain-metrics.json", "[" + string.Join(",", rows) + "]");
        }
        static IEnumerator DashCase(LevelManager level, OralSwallowFinale finale, bool large, string name)
        {
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            var vortex = large ? finale.Whirlpools.OrderByDescending(w => w.Radius).First() : finale.Whirlpools.OrderBy(w => w.Radius).First();
            Vector2 center = vortex.Position; Vector2 towardCenter = (OralMapGeometry.TongueCenter(map) - center).normalized;
            if (towardCenter.sqrMagnitude < .1f) towardCenter = Vector2.right;
            Place(player, center + towardCenter * vortex.Radius * .55f); Set(player, "currentDashCharges", player.MaxDashCharges);
            captureFocus = center; shotSize = Mathf.Max(5, vortex.Radius * 1.7f);
            float began = Time.time, beforePull = Vector2.Distance(player.Position, vortex.Position), pullDistance = 0;
            bool dashStarted = false, dashEnded = false; int chargesBefore = 0; Vector2 start = default, end = default; float endRadius = 0, endDistance = 0;
            yield return Record(name, () => Time.time - began < 3.5f, () =>
            {
                CloseDialogs(level); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth);
                if (!dashStarted && Time.time - began >= .75f)
                {
                    pullDistance = beforePull - Vector2.Distance(player.Position, vortex.Position);
                    // Directed to the tongue interior so the demonstration does not
                    // substitute an edge-clamped jump for the intended escape.
                    player.Move(towardCenter); chargesBefore = player.CurrentDashCharges; start = player.Position;
                    player.TryDash(); dashStarted = player.IsDashing; player.Move(Vector2.zero);
                }
                if (dashStarted && !player.IsDashing && !dashEnded)
                {
                    dashEnded = true; end = player.Position; endRadius = vortex.Radius; endDistance = Vector2.Distance(end, vortex.Position);
                    Check(player.CurrentDashCharges == chargesBefore - 1, name + " consumes exactly one dash charge");
                    Check(endDistance > endRadius, name + " ends outside the vortex in a single actual dash");
                    Check(finale.Whirlpools.All(w => Vector2.Distance(end, w.Position) > w.Radius), name + " endpoint also clears every overlapping neighboring vortex");
                }
            });
            player.Move(Vector2.zero); captureFocus = null;
            Check(pullDistance > .03f, name + " visibly pulls a stationary player inward before escape");
            Check(dashStarted && dashEnded, name + " actual dash starts and finishes");
            File.WriteAllText(Output + "/" + name + "-metrics.json", "{\"radius\":" + F(endRadius) + ",\"endpointRadius\":" + F(endDistance) + ",\"dashTravel\":" + F(Vector2.Distance(start, end)) + ",\"normalDashDistance\":" + F(player.DashDistance) + ",\"pullDistance\":" + F(pullDistance) + "}");
        }
        static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
        static IEnumerator Record(string name, Func<bool> active, Action step, float timeoutSeconds = 90)
        {
            string dir = Output + "/" + name; Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            File.WriteAllText(dir + "/times.csv", "frame,game_seconds\n");
            int oldRate = Time.captureFramerate, frame = 0; Time.captureFramerate = 15;
            float began = Time.time, timeout = Time.realtimeSinceStartup + timeoutSeconds;
            do
            {
                yield return null; step?.Invoke(); if (Index == 0) Frame(name + "/" + frame.ToString("D4"));
                File.AppendAllText(dir + "/times.csv", frame + "," + F(Time.time - began) + "\n"); frame++;
            } while (active() && Time.realtimeSinceStartup < timeout);
            Time.captureFramerate = oldRate; Check(!active(), name + " completes before the recording timeout");
        }
        static void Frame(string name)
        {
            var camera = Camera.main; if (camera == null) return;
            var overlays = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras = overlays.Select(c => c.worldCamera).ToArray(); var distances = overlays.Select(c => c.planeDistance).ToArray();
            var orders = overlays.Select(c => c.sortingOrder).ToArray(); var flags = overlays.Select(c => c.overrideSorting).ToArray();
            var target = camera.targetTexture; var active = RenderTexture.active; var position = camera.transform.position; float zoom = camera.orthographicSize;
            var rt = RenderTexture.GetTemporary(960, 640, 24); Texture2D image = null;
            try
            {
                if (captureFocus.HasValue) { var p = captureFocus.Value; camera.transform.position = new Vector3(p.x, p.y, position.z); camera.orthographicSize = shotSize; }
                foreach (var presentation in Object.FindObjectsOfType<MonoBehaviour>())
                {
                    if (!presentation.enabled || !(presentation is JawCrushVisual || presentation is PlayerCombatBars || presentation is OralSwallowVisual || presentation is OralMapVisual)) continue;
                    var late = presentation.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); late?.Invoke(presentation, null);
                }
                camera.targetTexture = rt;
                foreach (var c in overlays) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .1f; c.overrideSorting = true; c.sortingOrder = Mathf.Clamp(32100 + c.sortingOrder, 32100, 32760); }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(960, 640, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < overlays.Length; i++) if (overlays[i] != null) { overlays[i].renderMode = RenderMode.ScreenSpaceOverlay; overlays[i].worldCamera = cameras[i]; overlays[i].planeDistance = distances[i]; overlays[i].sortingOrder = orders[i]; overlays[i].overrideSorting = flags[i]; }
                camera.targetTexture = target; camera.transform.position = position; camera.orthographicSize = zoom; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); if (image != null) Object.Destroy(image); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
