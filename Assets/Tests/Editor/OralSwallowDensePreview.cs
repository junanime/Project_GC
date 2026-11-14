#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>Actual runtime full-map review of dense, colliding swallow vortices.</summary>
    [InitializeOnLoad]
    public static class OralSwallowDensePreview
    {
        const string Key = "OralSwallowDensePreview", Root = "Library/OralSwallowDenseProof";
        static bool started;
        static double deadline;
        static Vector2 captureFocus;
        static float shotSize;
        static int dashFailures;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }
        [Serializable] sealed class VortexDump { public Vector2 Position; public float Radius; }
        [Serializable] sealed class ObstacleDump { public string Name; public Vector2 Center, Size, Closest; public bool ContainsOrigin; }
        [Serializable] sealed class DashDump
        {
            public int Index;
            public Vector2 Origin, Direction, Planned, End, ClampedOrigin;
            public float PlayerRadius, TongueDistance;
            public bool OriginSafe, PlannedSafe, PlanFound;
            public VortexDump[] Before, After;
            public ObstacleDump[] BloodClots;
        }
        [Serializable] sealed class Coverage
        {
            public int Vortices, Samples, RimSamples, NeverTouched, RimNeverTouched;
            public float MinCoverage, MaxCoverage, MeanCoverage, MeanRimCoverage, WorstCorePenetration;
            public float LongestStationarySafeTime;
            public int PairBounces, BoundaryBounces;
        }
        sealed class Probe
        {
            public Vector2 Point;
            public bool Rim, Touched;
            public float SafeTime, MaxSafeTime;
        }

        static OralSwallowDensePreview() { if (SessionState.GetBool(Key, false)) Attach(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedSelection()
        {
            if (!SessionState.GetBool(Key, false)) return;
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root); Directory.CreateDirectory(Root + "/01-full-map"); RestorePrefs();
            File.WriteAllText(Root + "/result.txt", "RUNNING"); File.WriteAllText(Root + "/checks.txt", ""); File.WriteAllText(Root + "/errors.txt", "");
            var keys = new[] { "Coins", "LobbySilverCoins" };
            File.WriteAllText(Root + "/prefs.json", JsonUtility.ToJson(new Backup { prefs = keys.Select(k => new Pref { key = k, had = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            File.WriteAllText(Root + "/capture-method.txt", "Actual production Unity scene, Ashi, OralSwallowFinale and OralSwallowVisual. Age seek to 17 seconds and per-frame full health are review fixtures; percentage core damage bypasses ordinary invincibility. Full-map camera and hidden overlay HUD expose the entire tongue, including rim. No generated or interpolated frames. Capture at 20 simulation frames per second.\n");
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false); SessionState.SetBool(Key + "Done", false); SessionState.SetInt(Key + "Checks", 0);
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
            started = false; deadline = EditorApplication.timeSinceStartup + 1200;
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
                File.WriteAllText(Root + "/result.txt", (failed ? "FAIL" : "PASS") + " checks=" + SessionState.GetInt(Key + "Checks", 0));
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
            if (!value) throw new Exception("ORAL_SWALLOW_DENSE_FAIL: " + message);
            SessionState.SetInt(Key + "Checks", SessionState.GetInt(Key + "Checks", 0) + 1);
            File.AppendAllText(Root + "/checks.txt", "PASS " + message + "\n"); Debug.Log("ORAL_SWALLOW_DENSE_PASS " + message);
        }
        static FieldInfo Field(object owner, string name)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            { var f = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f; }
            throw new MissingFieldException(owner.GetType().Name, name);
        }
        static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        static int Counter(object owner, string name)
        {
            var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property == null ? -1 : Convert.ToInt32(property.GetValue(owner));
        }
        static void CloseDialogs(LevelManager level)
        {
            if (level.EntityManager.AbilitySelectionDialog.MenuOpen) { level.EntityManager.AbilitySelectionDialog.Close(); Time.timeScale = 1; }
        }
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
        static IEnumerator Checks(LevelManager level)
        {
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; preferences.muteOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Application.runInBackground = true; Time.timeScale = 1; RunScore.Exclude("Dense swallow collision review");
            var progression = StageProgression.Ensure(level);
            Check(progression.BeginDirectOral(true), "real stage-zero entry starts");
            float until = Time.realtimeSinceStartup + 90;
            while ((OralStageRuntime.Instance == null || progression.Travelling) && Time.realtimeSinceStartup < until) yield return null;
            Check(OralStageRuntime.Instance != null && !progression.Travelling, "real stage-zero loading completes");
            var stage = OralStageRuntime.Instance; var events = OralFieldEvents.Instance; var finale = OralSwallowFinale.Instance;
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            events.AutoSchedule = false; foreach (var c in events.Cavities) { c.Healed = true; c.Active = false; }
            Set(player, "isInvincible", true); Place(player, OralMapGeometry.TongueCenter(map));
            Check(finale.StartForTesting(), "actual finale starts"); Set(finale, "age", 20.1f); yield return null;
            Check(finale.Whirlpools.Count >= 40, "vortex count is increased beyond the earlier 21 to fill the whole tongue");
            var visual = Object.FindObjectOfType<OralSwallowVisual>();
            Check(visual != null && visual.Ready && visual.WhirlpoolVisualCount == finale.Whirlpools.Count, "every actual vortex has its corresponding rendered disc");
            var renderers = visual.VisualRoot.GetComponentsInChildren<MeshRenderer>().Where(r => r.gameObject.name.StartsWith("흡인 반경")).ToArray();
            Check(renderers.Select(r => r.sortingOrder).Distinct().Count() == renderers.Length, "each overlapping vortex has a stable unique render order");
            Check(renderers.All(r => r.sortingOrder > -850 && r.sortingOrder < -800), "all vortex discs remain between the water and closed upper-jaw layers");
            var orders = renderers.Select(r => r.sortingOrder).ToArray();
            yield return PairAndWallChecks(level, finale);
            yield return DashCheck(level, finale);

            finale.CancelForTransition(); finale.Configure(stage); Set(level, "levelTime", 572f); Clear(level);
            Place(player, OralMapGeometry.TongueCenter(map));
            Check(finale.StartForTesting(), "full-map review starts from the real generated field"); Set(finale, "age", 17f); yield return null;
            captureFocus = OralMapGeometry.PixelToWorld(map, new Vector2(701.5f, 540)); shotSize = map.WorldRect.height * .425f;
            var probes = BuildProbes(map);
            var coverage = new Coverage { Vortices = finale.Whirlpools.Count, Samples = probes.Count, RimSamples = probes.Count(p => p.Rim), MinCoverage = 1f };
            File.WriteAllText(Root + "/timeline.csv", "age,count,coverage,rimCoverage,worstCorePenetration,pairBounces,boundaryBounces\n");
            int frame = 0, samples = 0, oldRate = Time.captureFramerate; Time.captureFramerate = 20;
            float realDeadline = Time.realtimeSinceStartup + 240;
            var dir = Root + "/01-full-map"; foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            while (finale.Age < 39.95f && Time.realtimeSinceStartup < realDeadline)
            {
                yield return null; CloseDialogs(level); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth); player.Move(Vector2.zero);
                float overlap = MaxCorePenetration(finale);
                coverage.WorstCorePenetration = Mathf.Max(coverage.WorstCorePenetration, overlap);
                if (finale.Age >= 20 && frame % 5 == 0)
                {
                    int hit = 0, rimHit = 0;
                    foreach (var p in probes)
                    {
                        bool touched = finale.Whirlpools.Any(w => (p.Point - w.Position).sqrMagnitude <= w.Radius * w.Radius);
                        if (touched) { hit++; if (p.Rim) rimHit++; p.Touched = true; p.SafeTime = 0; }
                        else { p.SafeTime += .25f; p.MaxSafeTime = Mathf.Max(p.MaxSafeTime, p.SafeTime); }
                    }
                    float covered = (float)hit / probes.Count, rim = (float)rimHit / coverage.RimSamples;
                    coverage.MinCoverage = Mathf.Min(coverage.MinCoverage, covered); coverage.MaxCoverage = Mathf.Max(coverage.MaxCoverage, covered);
                    coverage.MeanCoverage += covered; coverage.MeanRimCoverage += rim; samples++;
                    File.AppendAllText(Root + "/timeline.csv", F(finale.Age) + "," + finale.Whirlpools.Count + "," + F(covered) + "," + F(rim) + "," + F(overlap) + "," + Counter(finale, "PairBounces") + "," + Counter(finale, "BoundaryBounces") + "\n");
                }
                Frame(frame++);
            }
            Time.captureFramerate = oldRate;
            coverage.MeanCoverage /= Mathf.Max(1, samples); coverage.MeanRimCoverage /= Mathf.Max(1, samples);
            coverage.NeverTouched = probes.Count(p => !p.Touched); coverage.RimNeverTouched = probes.Count(p => p.Rim && !p.Touched);
            coverage.LongestStationarySafeTime = probes.Max(p => p.MaxSafeTime);
            coverage.PairBounces = Counter(finale, "PairBounces"); coverage.BoundaryBounces = Counter(finale, "BoundaryBounces");
            File.WriteAllText(Root + "/coverage.json", JsonUtility.ToJson(coverage, true));
            File.WriteAllText(Root + "/media-source.json", "{\"frames\":" + frame + ",\"fps\":20,\"width\":1080,\"height\":720}");
            Check(finale.Age >= 39.9f && !finale.IsSwallowing, "full-map recording spans emergence and all twenty seconds before the final sweep");
            Check(coverage.PairBounces > 0, "natural dense field includes vortex-to-vortex bounces");
            Check(coverage.BoundaryBounces > 0, "natural dense field includes tongue-boundary rebounds");
            Check(renderers.All(r => r != null) && renderers.Select(r => r.sortingOrder).SequenceEqual(orders), "render order remains stable through movement and collisions");
            Check(coverage.WorstCorePenetration < .12f, "half-radius cores do not remain deeply intersecting during the actual recording");
            Check(coverage.MeanCoverage > .65f && coverage.MeanRimCoverage > .55f, "whole tongue and outer rim both have greater coverage than the sparse original field");
            Check(dashFailures == 0, "every generated vortex allows one actual dash outside all overlapping discs; failures=" + dashFailures + "/" + finale.Whirlpools.Count);
            SessionState.SetBool(Key + "Done", true);
        }
        static List<Probe> BuildProbes(FiniteStomachMap map)
        {
            var probes = new List<Probe>(); var rect = map.WorldRect;
            for (float x = rect.xMin; x < rect.xMax; x += .55f)
                for (float y = rect.yMin; y < rect.yMax; y += .55f)
                {
                    var p = new Vector2(x, y); float d = OralMapGeometry.TongueDistance(map, p, out _);
                    if (d >= .3f) probes.Add(new Probe { Point = p, Rim = d < 2f });
                }
            return probes;
        }
        static float MaxCorePenetration(OralSwallowFinale finale)
        {
            float max = 0; var states = finale.Whirlpools;
            for (int i = 0; i < states.Count; i++)
                for (int j = i + 1; j < states.Count; j++)
                    max = Mathf.Max(max, (states[i].Radius + states[j].Radius) * .5f - Vector2.Distance(states[i].Position, states[j].Position));
            return max;
        }
        static IEnumerator PairAndWallChecks(LevelManager level, OralSwallowFinale finale)
        {
            var states = finale.Whirlpools.ToArray(); var center = OralMapGeometry.TongueCenter(FiniteStomachMap.Instance);
            var snapshots = states.Select(w => new { w.Position, w.Radius, w.Angle, Direction = (Vector2)Field(w, "Direction").GetValue(w), Turn = (float)Field(w, "TurnRemaining").GetValue(w) }).ToArray();
            var advance = typeof(OralSwallowFinale).GetMethod("MoveWhirlpools", BindingFlags.Instance | BindingFlags.NonPublic);
            bool paused = level.IsRunFlowPaused; level.SetRunFlowPaused(true);
            try
            {
                for (int i = 0; i < states.Length; i++) { states[i].Radius = .05f; states[i].Position = center + new Vector2(-8 + i % 10 * 1.5f, 4 + i / 10f); Set(states[i], "Direction", Vector2.zero); Set(states[i], "TurnRemaining", 999f); }
                var a = states[0]; var b = states[1];
                a.Radius = b.Radius = 2f; a.Position = center + new Vector2(-1.01f, -3); b.Position = center + new Vector2(1.01f, -3);
                Set(a, "Direction", Vector2.right); Set(b, "Direction", Vector2.left);
                int before = Counter(finale, "PairBounces");
                for (int k = 0; k < 10; k++) advance.Invoke(finale, new object[] { .02f });
                Check(Vector2.Distance(a.Position, b.Position) >= 1.98f, "pair collision keeps half-radius cores separated");
                Check(Vector2.Dot((Vector2)Field(a, "Direction").GetValue(a), Vector2.left) > .2f && Vector2.Dot((Vector2)Field(b, "Direction").GetValue(b), Vector2.right) > .2f, "head-on half-radius collision sends both vortices away from one another");
                Check(Counter(finale, "PairBounces") > before, "pair collision is counted by the actual runtime");
                for (int i = 1; i < states.Length; i++) states[i].Position = center + new Vector2(-8 + i % 10 * 1.5f, 4 + i / 10f);
                a.Position = center + new Vector2(OralMapGeometry.TongueRadii(FiniteStomachMap.Instance).x - .2f, -3f);
                Set(a, "Direction", Vector2.right); before = Counter(finale, "BoundaryBounces");
                for (int k = 0; k < 5; k++) advance.Invoke(finale, new object[] { .02f });
                Check(Counter(finale, "BoundaryBounces") > before && ((Vector2)Field(a, "Direction").GetValue(a)).x < 0, "vortex reaches the outer tongue edge and rebounds inward");
                yield return null;
            }
            finally
            {
                for (int i = 0; i < states.Length; i++) { states[i].Position = snapshots[i].Position; states[i].Radius = snapshots[i].Radius; states[i].Angle = snapshots[i].Angle; Set(states[i], "Direction", snapshots[i].Direction); Set(states[i], "TurnRemaining", snapshots[i].Turn); }
                level.SetRunFlowPaused(paused);
            }
        }
        static IEnumerator DashCheck(LevelManager level, OralSwallowFinale finale)
        {
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            var states = finale.Whirlpools.ToArray(); int failures = 0;
            var safe = typeof(OralSwallowFinale).GetMethod("SafeDashPoint", BindingFlags.Instance | BindingFlags.NonPublic);
            File.WriteAllText(Root + "/dense-dash.csv", "index,radius,plannedDistance,actualDistance,minClearance,started,chargesUsed,finished,startSafe\n");
            for (int i = 0; i < states.Length; i++)
            {
                Set(finale, "age", 20.2f); Set(player, "isInvincible", true); Set(player, "currentHealth", player.MaxHealth);
                var vortex = states[i]; float angle = (i % 8) * Mathf.PI / 4;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Place(player, vortex.Position); Set(player, "currentDashCharges", player.MaxDashCharges); int charges = player.CurrentDashCharges;
                var start = player.Position; bool found = finale.TryGetDashExit(player, direction, out var target);
                bool startSafe = (bool)safe.Invoke(finale, new object[] { start });
                var snapshot = new DashDump
                {
                    Index = i, Origin = start, Direction = direction, Planned = target,
                    PlayerRadius = player.GetComponent<StomachSlopeBody>()?.Radius ?? .25f,
                    TongueDistance = OralMapGeometry.TongueDistance(map, start, out _),
                    OriginSafe = startSafe, PlannedSafe = (bool)safe.Invoke(finale, new object[] { target }), PlanFound = found,
                    ClampedOrigin = BloodClotObstacle.ClampDash(player.GetComponent<Rigidbody2D>(), start),
                    Before = states.Select(w => new VortexDump { Position = w.Position, Radius = w.Radius }).ToArray(),
                    BloodClots = Object.FindObjectsOfType<BloodClotObstacle>().SelectMany(o => o.GetComponentsInChildren<Collider2D>())
                        .Where(c => c.enabled && !c.isTrigger && Vector2.Distance(c.ClosestPoint(start), start) < 8)
                        .Select(c => new ObstacleDump { Name = c.name, Center = c.bounds.center, Size = c.bounds.size, Closest = c.ClosestPoint(start), ContainsOrigin = c.OverlapPoint(start) }).ToArray()
                };
                float planned = Vector2.Distance(start, target);
                player.Move(direction); player.TryDash(); player.Move(Vector2.zero); bool began = player.IsDashing;
                int used = charges - player.CurrentDashCharges;
                float until = Time.realtimeSinceStartup + 5;
                while (player.IsDashing && Time.realtimeSinceStartup < until) { CloseDialogs(level); yield return null; }
                float clearance = states.Min(w => Vector2.Distance(player.Position, w.Position) - w.Radius);
                bool passed = began && !player.IsDashing && used == 1 && clearance > 0;
                if (!passed)
                {
                    failures++; snapshot.End = player.Position;
                    snapshot.After = states.Select(w => new VortexDump { Position = w.Position, Radius = w.Radius }).ToArray();
                    File.WriteAllText(Root + "/dash-failure-" + i + ".json", JsonUtility.ToJson(snapshot, true));
                }
                File.AppendAllText(Root + "/dense-dash.csv", i + "," + F(vortex.Radius) + "," + F(planned) + "," + F(Vector2.Distance(start, player.Position)) + "," + F(clearance) + "," + began + "," + used + "," + !player.IsDashing + "," + startSafe + "\n");
            }
            dashFailures = failures;
            File.AppendAllText(Root + "/checks.txt", "DASH_DIAGNOSTIC failures=" + failures + "/" + states.Length + "\n");
        }
        static string F(float value) => value.ToString("F4", CultureInfo.InvariantCulture);
        static void Frame(int frame)
        {
            var camera = Camera.main; if (camera == null) return;
            var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
            var enabled = canvases.Select(c => c.enabled).ToArray();
            var target = camera.targetTexture; var active = RenderTexture.active; var position = camera.transform.position; float zoom = camera.orthographicSize;
            var rt = RenderTexture.GetTemporary(1080, 720, 24); Texture2D image = null;
            try
            {
                camera.transform.position = new Vector3(captureFocus.x, captureFocus.y, position.z); camera.orthographicSize = shotSize;
                foreach (var presentation in Object.FindObjectsOfType<MonoBehaviour>())
                {
                    if (!presentation.enabled || !(presentation is JawCrushVisual || presentation is PlayerCombatBars || presentation is OralSwallowVisual || presentation is OralMapVisual)) continue;
                    presentation.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(presentation, null);
                }
                foreach (var c in canvases) c.enabled = false;
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(1080, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1080, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Root + "/01-full-map/" + frame.ToString("D4") + ".png", image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) if (canvases[i] != null) canvases[i].enabled = enabled[i];
                camera.targetTexture = target; camera.transform.position = position; camera.orthographicSize = zoom; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(rt); if (image != null) Object.Destroy(image);
            }
        }
    }
}
#endif
