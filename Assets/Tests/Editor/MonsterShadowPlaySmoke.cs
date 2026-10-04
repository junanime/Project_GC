using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class MonsterShadowPlaySmoke
    {
        const string Key = "MonsterShadowReview";
        static bool attached, complete, failed;
        static double deadline;
        static MonsterShadowPlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void RunBefore() => Run("before");
        public static void RunAfter() => Run("after");
        public static void InstallAndRun()
        {
            MonsterShadowFootprintInstaller.Install();
            RunAfter();
        }
        static void Run(string mode)
        {
            SessionState.SetString(Key + "Mode", mode);
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach();
            EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            attached = complete = failed = false;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        static void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true; }
        public static void Done() { complete = true; }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > deadline) failed = true;
            if (failed || complete)
            {
                if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                Debug.Log("[MonsterShadowReview] FINISHED failed=" + failed);
                EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            if (!attached && EditorApplication.isPlaying && ApothecaryUI.Instance != null)
            {
                attached = true;
                var runner = new GameObject("Monster shadow review");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<MonsterShadowReviewRunner>();
            }
        }
    }

    public sealed class MonsterShadowReviewRunner : MonoBehaviour
    {
        [Serializable] public class Record
        {
            public string blueprint, sprite, image;
            public Vector3 bodyMin, bodyMax, shadowCenter, shadowSize;
        }
        [Serializable] public class Report { public List<Record> entries = new List<Record>(); }
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        public static SpriteRenderer Shadow(Monster monster)
        {
            var target = (GameObject)typeof(Monster).GetField("shadow", Hidden).GetValue(monster);
            return target != null ? target.GetComponent<SpriteRenderer>() : null;
        }
        public static SpriteRenderer Body(Monster monster)
        {
            var body = (SpriteRenderer)typeof(Monster).GetField("monsterSpriteRenderer", Hidden).GetValue(monster);
            if (body != null && body.enabled) return body;
            return monster.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(x => x.enabled && x != Shadow(monster));
        }
        static void Check(bool result, string message)
        {
            if (!result) throw new Exception("[MonsterShadowReview] FAIL " + message);
        }
        IEnumerator Start()
        {
            var original = CrossSceneData.CharacterBlueprint;
            try
            {
                CrossSceneData.CharacterBlueprint = ApothecaryUI.Instance.Config.characters.First(x =>
                    x.skills != null && x.skills.kind == CharacterSkillDefinition.SkillKind.Hyuki);
                SceneManager.LoadScene(1);
                yield return new WaitForSecondsRealtime(2);
                var level = FindObjectOfType<LevelManager>();
                level.enabled = false;
                var tutorial = TutorialGuide.Instance;
                if (tutorial != null) { tutorial.Close(); tutorial.enabled = false; }
                Time.timeScale = 0;
                string mode = SessionState.GetString("MonsterShadowReviewMode", "before");
                string folder = "Library/MonsterShadowReview/" + mode;
                Directory.CreateDirectory(folder);
                var report = new Report();
                var containers = level.CurrentLevelBlueprint.monsters;
                for (int pool = 0; pool < containers.Length; pool++)
                {
                    if (containers[pool].monstersPrefab.GetComponent<BossMonster>() != null ||
                        containers[pool].monstersPrefab.GetComponent<MiniBossMonster>() != null) continue;
                    foreach (var blueprint in containers[pool].monsterBlueprints)
                    {
                        if (blueprint == null) continue;
                        var monster = level.EntityManager.SpawnMonster(pool, new Vector2(100, 100), blueprint, 0);
                        if (monster == null) throw new Exception("Unable to review " + blueprint.name);
                        var body = Body(monster); var shadow = Shadow(monster);
                        if (body == null || body.sprite == null) { monster.gameObject.SetActive(false); continue; }
                        body.flipX = false;
                        var placement = monster.GetComponent<MonsterGroundShadow>();
                        if (placement != null && placement.enabled) placement.RefreshFacing();
                        var record = new Record {
                            blueprint = AssetDatabase.GetAssetPath(blueprint),
                            sprite = AssetDatabase.GetAssetPath(body.sprite),
                            image = report.entries.Count.ToString("D2") + ".png",
                            bodyMin = body.bounds.min - monster.transform.position,
                            bodyMax = body.bounds.max - monster.transform.position,
                            shadowCenter = shadow != null ? shadow.bounds.center - monster.transform.position : Vector3.zero,
                            shadowSize = shadow != null ? shadow.bounds.size : Vector3.zero
                        };
                        Capture(monster, body, shadow, folder + "/" + record.image);
                        report.entries.Add(record);
                        File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(report, true));
                        if (mode == "after" && placement != null && placement.enabled)
                        {
                            ValidateAnimationAndFacing(monster, body, shadow, placement, blueprint);
                            Capture(monster, body, shadow, folder + "/" + record.image.Replace(".png", "-flipped.png"));
                        }
                        monster.gameObject.SetActive(false);
                    }
                }
                File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(report, true));
                if (mode == "after") ValidatePoolReuse(level);
                Debug.Log("[MonsterShadowReview] Captured " + report.entries.Count + " field monster variants");
                MonsterShadowPlaySmoke.Done();
            }
            finally { CrossSceneData.CharacterBlueprint = original; Time.timeScale = 1; }
        }

        static void ValidateAnimationAndFacing(Monster monster, SpriteRenderer body, SpriteRenderer shadow,
            MonsterGroundShadow placement, MonsterBlueprint blueprint)
        {
            Vector3 center = shadow.bounds.center, size = shadow.bounds.size;
            var original = body.sprite;
            var visual = blueprint is EliteMonsterBlueprint elite && elite.useSourceVisual && elite.sourceNormalBlueprint != null
                ? elite.sourceNormalBlueprint : blueprint;
            var frames = visual.walkSpriteSequence;
            foreach (var frame in frames ?? Array.Empty<Sprite>())
            {
                if (frame == null) continue;
                body.sprite = frame; placement.RefreshFacing();
                Check(Vector3.Distance(shadow.bounds.center, center) < .001f, blueprint.name + " ground does not jump with animation");
                Check(Vector3.Distance(shadow.bounds.size, size) < .001f, blueprint.name + " footprint width remains stable");
            }
            body.sprite = original;
            body.flipX = true; placement.RefreshFacing();
            Check(Mathf.Abs(shadow.bounds.center.y - center.y) < .001f, blueprint.name + " flip retains ground height");
            Check(Mathf.Abs(shadow.bounds.center.x + center.x - 2 * body.transform.position.x) < .003f,
                blueprint.name + " ground follows horizontal facing");
            Check(shadow.sortingOrder < body.sortingOrder, blueprint.name + " shadow remains behind body");
            var scale = monster.transform.localScale;
            monster.transform.localScale = scale * 1.5f;
            Check(Mathf.Abs(shadow.bounds.size.x - size.x * 1.5f) < .003f, blueprint.name + " scaling preserves footprint ratio");
            monster.transform.localScale = scale;
            Debug.Log("[MonsterShadowReview] PASS frames, facing and scale: " + ((UnityEngine.Object)blueprint).name);
        }

        static void ValidatePoolReuse(LevelManager level)
        {
            var shark = AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/3_1.asset");
            var monster = level.EntityManager.SpawnMonster(0, new Vector2(100,100), shark, 0);
            var body = Body(monster); var shadow = Shadow(monster);
            Check(shadow.bounds.center.y > body.bounds.min.y + body.bounds.size.y * .12f,
                "shark footprint raised between feet instead of below them");
            var sourceShadow = Shadow(level.CurrentLevelBlueprint.monsters[0].monstersPrefab.GetComponent<Monster>());
            var unprofiled = Instantiate(shark);
            try
            {
                unprofiled.useGroundShadowFootprint = false;
                level.EntityManager.LivingMonsters.Remove(monster);
                level.EntityManager.DespawnMonster(0, monster, false);
                var reused = level.EntityManager.SpawnMonster(0, new Vector2(101,100), unprofiled, 0);
                Check(reused == monster, "real monster pool reuses the reviewed instance");
                Check(Vector3.Distance(shadow.transform.localPosition, sourceShadow.transform.localPosition) < .001f,
                    "pooled unprofiled monster restores prefab shadow position");
                Check(Vector3.Distance(shadow.transform.localScale, sourceShadow.transform.localScale) < .001f,
                    "pooled unprofiled monster restores prefab shadow size");
                Check(!monster.GetComponent<MonsterGroundShadow>().enabled, "pooled legacy actor has no stale follow behavior");
                Debug.Log("[MonsterShadowReview] PASS shark contact and pooled restoration");
            }
            finally
            {
                level.EntityManager.LivingMonsters.Remove(monster);
                level.EntityManager.DespawnMonster(0, monster, false);
                Destroy(unprofiled);
            }
        }

        static void Capture(Monster monster, SpriteRenderer body, SpriteRenderer shadow, string path)
        {
            foreach (var t in monster.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
            var bounds = body.bounds;
            foreach (var sprite in monster.GetComponentsInChildren<SpriteRenderer>())
                if (sprite.enabled && sprite.sprite != null) bounds.Encapsulate(sprite.bounds);
            if (shadow != null && shadow.sprite != null) bounds.Encapsulate(shadow.bounds);
            var cameraObject = new GameObject("Shadow review camera");
            var camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(320, 280, 24);
            var image = new Texture2D(320, 280, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x * 280f / 320) * .65f;
                camera.transform.position = bounds.center + Vector3.back * 10;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.61f, .52f, .52f);
                camera.cullingMask = 1 << 30;
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 320, 280), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                DestroyImmediate(image); target.Release(); DestroyImmediate(target); DestroyImmediate(cameraObject);
            }
        }
    }
}
