#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Real runtime presentation, isolated from normal saves. No replacement animation art.
    [InitializeOnLoad]
    public static class ColonReleasePresentation
    {
        const string Session = "ColonReleasePresentation";
        static string Output => Environment.GetEnvironmentVariable("COLON_RELEASE_PROOF") ?? "Library/ColonReleasePresentation";
        static bool started, pending;
        static double deadline;
        static StageCaptureProbe probe;
        static Action frameAction;
        static Func<Vector2> focus;
        static string framePath;
        static float framing;
        static int width = 960, height = 540;
        static int checks;
        static ColonReleasePresentation() { if (SessionState.GetBool(Session, false)) Attach(); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/errors.txt", "");
            File.WriteAllText(Output + "/checks.txt", "");
            File.WriteAllText(Output + "/result.txt", "RUNNING");
            File.WriteAllText(Output + "/frame-times.csv", "frame,gameTime,unityFrame\n");
            foreach (string key in new[] { "Coins", "LobbySilverCoins" })
            {
                SessionState.SetBool(Session + key + "Had", PlayerPrefs.HasKey(key));
                SessionState.SetInt(Session + key, PlayerPrefs.GetInt(key));
            }
            SessionState.SetString(Session + "Character", CrossSceneData.CharacterBlueprint != null ? AssetDatabase.GetAssetPath(CrossSceneData.CharacterBlueprint) : "");
            SessionState.SetBool(Session, true); SessionState.SetBool(Session + "Done", false); SessionState.SetBool(Session + "Failed", false);
            CrossSceneData.CharacterBlueprint = AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems = Array.Empty<MerchantItemBlueprint>(); CrossSceneData.ClearPendingRunSceneTransfer();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started = false; checks = 0; deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            File.AppendAllText(Output + "/errors.txt", message + "\n" + stack + "\n"); SessionState.SetBool(Session + "Failed", true);
        }
        static void Check(bool pass, string message)
        {
            if (!pass) throw new Exception("COLON_RELEASE_PROOF: " + message);
            checks++; File.AppendAllText(Output + "/checks.txt", message + "\n");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Session, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) { File.AppendAllText(Output + "/errors.txt", "Capture deadline\n"); SessionState.SetBool(Session + "Failed", true); }
            if (SessionState.GetBool(Session + "Done", false) || SessionState.GetBool(Session + "Failed", false))
            {
                if (EditorApplication.isPlaying) { Time.captureFramerate = 0; Time.timeScale = 1; EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                foreach (string key in new[] { "Coins", "LobbySilverCoins" })
                { if (SessionState.GetBool(Session + key + "Had", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Session + key, 0)); else PlayerPrefs.DeleteKey(key); }
                PlayerPrefs.Save();
                var path = SessionState.GetString(Session + "Character", "");
                CrossSceneData.CharacterBlueprint = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(path);
                bool failed = SessionState.GetBool(Session + "Failed", false); SessionState.SetBool(Session, false);
                File.WriteAllText(Output + "/result.txt", (failed ? "FAIL" : "PASS") + " checks=" + File.ReadAllLines(Output + "/checks.txt").Length);
                EditorApplication.Exit(failed ? 1 : 0); return;
            }
            if (!EditorApplication.isPlaying) return;
            var guide = Object.FindObjectOfType<TutorialGuide>(); if (guide != null) { guide.Close(); guide.enabled = false; }
            var level = Object.FindObjectOfType<LevelManager>();
            if (started || level == null || !level.InitialFieldObjectsReady || level.PlayerCharacter == null) return;
            started = true; level.StartCoroutine(Record(level));
        }
        static IEnumerator Until(Func<bool> ready, float seconds, string message)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Check(ready(), message);
        }
        static void Quiet(LevelManager level)
        {
            level.enabled = false;
            foreach (var a in Object.FindObjectsOfType<Ability>()) { a.enabled = false; a.StopAllCoroutines(); }
            foreach (var b in Object.FindObjectsOfType<MonoBehaviour>())
                if (b is StageEventDirector || b is TimedSpecialMonsterSpawner || b is AcidToadSpawn || b is NPCSpawner || b is SnailBossDebugSpawn || b is ColonSpecialDirector)
                { b.enabled = false; b.StopAllCoroutines(); }
            if (ColonFieldEvents.Instance != null) { ColonFieldEvents.Instance.AutoSchedule = false; ColonFieldEvents.Instance.Cancel(); }
            foreach (var m in Object.FindObjectsOfType<Monster>()) m.DespawnForStageTransfer();
            foreach (var p in Object.FindObjectsOfType<Projectile>()) p.RemoveForItem();
        }
        static IEnumerator PrepareInteraction(Character player, InteractableEventObject target)
        {
            Check(target != null, "Interaction target exists");
            ColonContentSmoke.Position(player, target.transform.position); player.Move(Vector2.zero);
            Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
        }
        static void TriggerInteraction(InteractableEventObject target)
        {
            Check(InteractionFocus.SelectTouched(target), "Actual interaction focus: " + target.name);
            target.TryInteract();
        }
        static IEnumerator Record(LevelManager level)
        {
            var preferences = GamePreferences.Current.Copy(); preferences.pauseOnFocusLoss = false; GamePreferences.Apply(preferences, false);
            Application.runInBackground = true; Time.timeScale = 1; RunScore.Exclude("Colon release presentation capture");
            Quiet(level); yield return ColonContentSmoke.Enter(level); Quiet(level);
            var player = level.PlayerCharacter; var map = FiniteStomachMap.Instance;
            ColonContentSmoke.Set(player, "isInvincible", true); player.CollectableCollider.enabled = false;
            var director = ConstipationKingDirector.Ensure(level); yield return null; yield return null;
            var exit = ColonExitSequence.Ensure(level);
            Check(director.Throne != null && !director.Spawned, "Empty throne installed before manual summon");
            Time.captureFramerate = 12;
            probe = level.gameObject.AddComponent<StageCaptureProbe>(); probe.AfterLateUpdate = CapturePending;
            width = 1536; height = 1024;
            yield return Still("full-map", () => map.WorldRect.center, Mathf.Max(map.WorldRect.height * .5f, map.WorldRect.width / 1.5f * .5f));
            width = 960; height = 540;
            foreach (var obj in level.GetComponent<FieldObjectLayout>().SpawnedObjects) if (obj != null) obj.SetActive(false);
            foreach (var home in Object.FindObjectsOfType<ColonBossHabitat>()) home.gameObject.SetActive(false);
            Quiet(level);
            // Walk beside a real central ridge. Physics and the player's movement controller remain active.
            var ridge = ColonMapGeometry.CentralRidge(map);
            var wallSample = ridge[ridge.Length / 2];
            var nearWall = map.ClampToFloor(wallSample + Vector2.down * (map.WallWidth + 1.2f), .55f);
            ColonContentSmoke.Position(player, nearWall); player.Move(Vector2.zero);
            var walkOrigin = player.Position;
            float wallWalkDistance = 0; var previousWalk = player.Position;
            yield return Clip("wall-walk", 60, () => player.Position + Vector2.up * 1.6f, 4.1f, i =>
            {
                wallWalkDistance += Vector2.Distance(previousWalk, player.Position); previousWalk = player.Position;
                player.Move(i < 30 ? Vector2.right : Vector2.left);
                Check(map.FloorClearance(player.Position) >= .15f, "Wall walk safe frame " + i);
            });
            player.Move(Vector2.zero);
            Check(wallWalkDistance > 1, "Player visibly moves beside the wall through the actual controller");
            Check(Vector2.Distance(walkOrigin, player.Position) < 8, "Wall walk stayed in intended local area");

            var inlet = StomachOpening.Find(true);
            inlet.SetOpened(0); ColonContentSmoke.Position(player, inlet.transform.position);
            yield return Still("inlet-closed", () => inlet.Center + inlet.Inward * 1.0f, 6.5f);
            yield return PrepareInteraction(player, inlet);
            yield return Clip("inlet-opening", 30, () => inlet.Center + inlet.Inward * 1.0f, 6.5f, i => { if (i == 0) TriggerInteraction(inlet); });
            Check(inlet.IsOpen, "Upper inlet opens through actual interaction");
            yield return Still("inlet-open", () => inlet.Center + inlet.Inward * 1.0f, 6.5f);

            ColonContentSmoke.Position(player, director.Throne.transform.position); player.Move(Vector2.zero);
            var summonCenter = director.SummonAnchor + Vector2.up * 1.8f;
            yield return Still("empty-throne", () => summonCenter, 5.5f);
            yield return Clip("summoning", 10, () => summonCenter, 5.5f);
            yield return PrepareInteraction(player, director.Throne);
            yield return Clip("summoning", 65, () => summonCenter, 5.5f, i =>
            {
                if (i != 0) return;
                TriggerInteraction(director.Throne);
                Check(director.Boss != null, "Manual interaction creates the real boss");
                director.Boss.AutoPatterns = false;
            }, 10);
            var boss = director.Boss;
            yield return Until(() => !boss.Arriving && !director.Summoning, 15, "Bowl emergence finishes and releases combat");
            Quiet(level);
            boss.TakeDamage(999999);
            yield return Until(() => boss.PhaseTwo && !boss.Transitioning, 15, "Final phase ready for real defeat capture");
            var deathCenter = (Vector2)boss.transform.position + Vector2.up * 1.7f;
            ColonContentSmoke.Position(player, map.ClampToFloor((Vector2)boss.transform.position + Vector2.right * 5, .5f)); player.Move(Vector2.zero);
            yield return Clip("boss-key-drop", 8, () => deathCenter, 5.8f);
            yield return Clip("boss-key-drop", 64, () => deathCenter, 5.8f, i => { if (i == 0) boss.TakeDamage(999999); }, 8);
            yield return Until(() => exit.State == ColonExitSequence.ExitState.Dropped, 10, "Released key plants on safe floor");
            Check(map.FloorClearance(exit.Pickup.transform.position) >= .5f, "Planted key can be reached on floor");
            var planted = (Vector2)exit.Pickup.transform.position;
            yield return Still("planted-key", () => planted + Vector2.up * .9f, 3.4f);
            yield return Clip("planted-key", 30, () => planted + Vector2.up * .9f, 3.4f);
            ColonContentSmoke.Position(player, exit.Pickup.transform.position);
            yield return Clip("key-pickup", 8, () => player.Position + Vector2.up * .8f, 3.5f);
            yield return PrepareInteraction(player, exit.Pickup);
            yield return Clip("key-pickup", 30, () => player.Position + Vector2.up * .8f, 3.5f, i => { if (i == 0) TriggerInteraction(exit.Pickup); }, 8);
            yield return Until(() => exit.State == ColonExitSequence.ExitState.Carrying, 6, "Key extracts into floating carry");
            Check(!exit.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("Raised arm") || t.name.Contains("두 손")), "No carry arms or connectors remain");
            Quiet(level);
            var carryAt = map.ClampToFloor(map.UVToWorld(new Vector2(.5f, .45f)), 2);
            ColonContentSmoke.Position(player, carryAt);
            float carryDistance = 0; var previousCarry = player.Position;
            yield return Clip("floating-key", 48, () => player.Position + Vector2.up * .7f, 3.2f,
                i => { carryDistance += Vector2.Distance(previousCarry, player.Position); previousCarry = player.Position; player.Move(i < 24 ? Vector2.right : Vector2.left); });
            player.Move(Vector2.zero);
            Check(carryDistance > 1, "Floating key follows actual walking movement");
            ColonContentSmoke.Position(player, exit.Opening.transform.position);
            var gateCenter = exit.Opening.Center + exit.Opening.Inward * 1.5f;
            yield return Still("sealed-exit", () => gateCenter, 7.0f);
            yield return Clip("unlocking", 10, () => gateCenter, 7.0f);
            yield return PrepareInteraction(player, exit.Opening);
            yield return Clip("unlocking", 78, () => gateCenter, 7.0f, i => { if (i == 0) TriggerInteraction(exit.Opening); }, 10);
            yield return Until(() => exit.State == ColonExitSequence.ExitState.Open, 12, "Lock shatters and outlet opens");
            yield return Still("open-exit", () => gateCenter, 7.0f);
            Check(!level.IsLevelEnded, "Showcase does not finish or save a run");
            probe.AfterLateUpdate = null; Time.captureFramerate = 0;
            File.WriteAllText(Output + "/capture-notes.txt", "Actual game runtime at 12 fps, normal time. Review camera and no screen HUD. Field objects retained in full map, hidden during detail clips; automatic attacks/events disabled and enemies cleared for visibility. Manual boss summon, actual lethal damage, physical interaction pickup and unlock. Save submission excluded. No generated animation frames.");
            SessionState.SetBool(Session + "Done", true);
        }
        static IEnumerator Still(string name, Func<Vector2> center, float size)
        { yield return Frame(Output + "/" + name + ".png", center, size, null); }
        static IEnumerator Clip(string name, int count, Func<Vector2> center, float size, Action<int> before = null, int offset = 0)
        {
            Directory.CreateDirectory(Output + "/" + name);
            for (int i = 0; i < count; i++)
            { int index = i; yield return Frame(Output + "/" + name + "/" + (i + offset).ToString("D4") + ".png", center, size, () => before?.Invoke(index)); }
        }
        static IEnumerator Frame(string path, Func<Vector2> center, float size, Action before)
        {
            framePath = path; focus = center; framing = size; frameAction = before; pending = true;
            yield return null; while (pending) yield return null;
        }
        static void CapturePending()
        {
            if (!pending) return;
            frameAction?.Invoke();
            var camera = Camera.main; Vector3 oldPosition = camera.transform.position; float oldSize = camera.orthographicSize, oldAspect = camera.aspect;
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24); Texture2D pixels = null;
            try
            {
                var point = focus(); camera.transform.position = new Vector3(point.x, point.y, oldPosition.z);
                camera.orthographicSize = framing; camera.aspect = width / (float)height; camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                File.WriteAllBytes(framePath, pixels.EncodeToPNG());
                File.AppendAllText(Output + "/frame-times.csv", framePath.Replace('\\', '/') + "," + Time.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + Time.frameCount + "\n");
            }
            finally
            {
                camera.transform.position = oldPosition; camera.orthographicSize = oldSize; camera.aspect = oldAspect;
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target); if (pixels != null) Object.Destroy(pixels); pending = false;
            }
        }
    }
}
#endif
