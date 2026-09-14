using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    // Launch with -executeMethod Vampire.Tests.Editor.MonsterRemakePlaySmoke.Run (without -quit).
    // Uses real Level 1 services and real pools; all fixture changes exist only in Play Mode.
    [InitializeOnLoad]
    public static class MonsterRemakePlaySmoke
    {
        private const string Key = "MonsterRemakeSmoke";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double next, deadline;
        private static Character character;
        private static SyringeDartAbility needle;
        private static TrapMonster trap;
        private static SniperMonster sniper;
        private static Projectile existing;
        private static Vector3 lockedPosition, oldProjectilePosition, sniperPosition;
        private static int[] projectilesAtBind;
        private static int trapPool;
        private static TrapMonsterBlueprint trapData;
        static MonsterRemakePlaySmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            MonsterRemakeTestRunner.RunAll();
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + "Failed", false);
            SessionState.SetInt(Key + "Phase", 0);
            Attach();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void Attach()
        {
            next = EditorApplication.timeSinceStartup + 3;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                SessionState.SetBool(Key + "Failed", true);
        }
        private static void Check(bool ok, string description)
        {
            if (!ok) throw new Exception(description);
            Debug.Log("[MonsterRemakeSmoke] PASS " + description);
        }
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Hidden).Invoke(target, args);
        private static int AddPool(EntityManager entity, string path)
        {
            var field = typeof(EntityManager).GetField("monsterPools", Hidden);
            var pools = (MonsterPool[])field.GetValue(entity);
            var go = new GameObject("Smoke Monster Pool");
            var pool = go.AddComponent<MonsterPool>();
            pool.Init(entity, character, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            field.SetValue(entity, pools.Concat(new[] { pool }).ToArray());
            return pools.Length;
        }
        private static SpriteRenderer PlayerSprite => character.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
        private static void CaptureView(string name, Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var go = new GameObject("Smoke Capture Camera");
            var camera = go.AddComponent<Camera>();
            var texture = new RenderTexture(640, 640, 24);
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.transform.position = new Vector3(center.x, center.y, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.16f, 0.19f);
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                image.Apply();
                string folder = System.IO.Path.GetFullPath("../outputs/monster-game-validation");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(image);
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
        private static void Tick()
        {
            int phase = SessionState.GetInt(Key + "Phase", 0);
            if (!EditorApplication.isPlaying)
            {
                if (phase == 9 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool failed = SessionState.GetBool(Key + "Failed", false);
                    SessionState.EraseBool(Key);
                    Debug.Log("[MonsterRemakeSmoke] FINISHED Failed=" + failed);
                    EditorApplication.Exit(failed ? 1 : 0);
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Smoke timeout");
                Time.timeScale = 1;
                var entity = UnityEngine.Object.FindObjectOfType<EntityManager>();
                switch (phase)
                {
                    case 0:
                        character = UnityEngine.Object.FindObjectOfType<Character>();
                        needle = UnityEngine.Object.FindObjectOfType<SyringeDartAbility>();
                        Check(character != null && entity != null && needle != null, "Level 1 player and weapon initialized");
                        UnityEngine.Object.FindObjectOfType<LevelManager>().enabled = false;
                        character.Move(Vector2.zero);
                        trapPool = AddPool(entity, "Assets/Prefabs/Monsters/Trap Monster.prefab");
                        trapData = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<TrapMonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/Trap Monster.asset"));
                        trapData.spawnNearPlayerForTest = false;
                        trapData.tickDamage = 0;
                        trapData.deathSprites = Array.Empty<Sprite>();
                        trapData.deathDespawnDelay = 0;
                        trap = (TrapMonster)entity.SpawnMonster(trapPool, (Vector2)character.transform.position + Vector2.up * 2, trapData);
                        int sniperPool = AddPool(entity, "Assets/Prefabs/Monsters/SniperMonster.prefab");
                        var sniperData = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SniperMonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/Sniper Monster.asset"));
                        sniperData.enforceSpawnDistance = false;
                        sniperData.firstAttackDelay = 100;
                        sniperData.aimDuration = 0.12f;
                        sniperData.lockDuration = 0.12f;
                        sniperData.atk = 0;
                        sniperData.hp = 100000;
                        sniper = (SniperMonster)entity.SpawnMonster(sniperPool, (Vector2)character.transform.position + new Vector2(-2, 1), sniperData);
                        sniperPosition = sniper.transform.position;
                        next = EditorApplication.timeSinceStartup + 0.1;
                        break;
                    case 1:
                        var before = UnityEngine.Object.FindObjectsOfType<Projectile>();
                        Check((bool)Call(needle, "LaunchSyringeProjectile", Vector2.right), "Unbound needle launches normally");
                        existing = UnityEngine.Object.FindObjectsOfType<Projectile>().Except(before).First();
                        oldProjectilePosition = existing.transform.position;
                        character.LookDirection = Vector2.right;
                        PlayerSprite.flipX = false;
                        Call(trap, "ActivateTrap", character);
                        lockedPosition = character.transform.position;
                        projectilesAtBind = UnityEngine.Object.FindObjectsOfType<Projectile>().Select(p => p.GetInstanceID()).ToArray();
                        character.Move(Vector2.left);
                        character.LookDirection = Vector2.left;
                        Check(needle.AttacksBlocked, "Capture blocks weapon attacks immediately");
                        Check(!(bool)Call(needle, "LaunchSyringeProjectile", Vector2.right), "New direct needle launch rejected while captured");
                        Call(needle, "Attack"); // A pending volley must also pass the emission gate.
                        next = EditorApplication.timeSinceStartup + 0.18;
                        break;
                    case 2:
                        Check(character.IsTrapBound && !PlayerSprite.flipX && character.LookDirection == Vector2.right, "Captured facing remains fixed despite opposite input");
                        Check(Vector2.Distance(character.transform.position, lockedPosition) < 0.001f, "Captured actor position stays fixed");
                        Check(existing != null && existing.gameObject.activeInHierarchy && Vector2.Distance(existing.transform.position, oldProjectilePosition) > 0.01f, "Already-fired projectile continues moving");
                        Check(!UnityEngine.Object.FindObjectsOfType<Projectile>().Any(p => !projectilesAtBind.Contains(p.GetInstanceID())), "No new player projectile during capture or delayed volley");
                        next = EditorApplication.timeSinceStartup + 0.8;
                        break;
                    case 3:
                        Check(PlayerSprite.sprite.name == "Capture_02", "Open yellow-gap beak holds final capture expression");
                        Check(trap.GetComponent<SeaweedTrapVisual>().FrameIndex == 7, "Seaweed holds final wrap instead of restarting");
                        CaptureView("Captured_Ashi_InGame", PlayerSprite.bounds.center);
                        var arrows = (Array)typeof(TrapMonster).GetField("arrowSequence", Hidden).GetValue(trap);
                        foreach (object arrow in arrows) Call(trap, "HandleArrowInput", arrow);
                        Check(!character.IsTrapBound && !needle.AttacksBlocked, "Correct arrow sequence releases bind and attack gate");
                        character.Move(Vector2.zero);
                        next = EditorApplication.timeSinceStartup + 0.6;
                        break;
                    case 4:
                        Check(PlayerSprite.sprite.name.Contains("Ashi_Idle"), "Normal idle returns after escape");
                        Check((bool)Call(needle, "LaunchSyringeProjectile", Vector2.right), "New firing resumes after escape");
                        trap = (TrapMonster)entity.SpawnMonster(trapPool, (Vector2)character.transform.position + Vector2.up * 2, trapData);
                        Check(trap.IsDormant && trap.GetComponent<SeaweedTrapVisual>().FrameIndex == 0, "Pooled trap returns to dormant art");
                        character.LookDirection = Vector2.left;
                        PlayerSprite.flipX = true;
                        Call(trap, "ActivateTrap", character);
                        next = EditorApplication.timeSinceStartup + 0.2;
                        break;
                    case 5:
                        Check(PlayerSprite.flipX && character.LookDirection == Vector2.left, "Left-facing capture mirrors expression and wrap");
                        trap.SetFieldRuntimeSuspended(true);
                        Check(!character.IsTrapBound && !needle.AttacksBlocked, "Stage suspension releases player and restores attacks");
                        trap.SetFieldRuntimeSuspended(false);
                        sniper.StartCoroutine((System.Collections.IEnumerator)Call(sniper, "AimLockAndShoot"));
                        next = EditorApplication.timeSinceStartup + 0.29;
                        break;
                    case 6:
                        Check(sniper.GetComponent<SniperRecoilVisual>().IsPlaying, "Real aim-lock-shot starts bonito recoil");
                        Check(Vector2.Distance(sniper.transform.position, sniperPosition) < 0.001f, "Recoil never moves sniper root XY (sorting Z excluded)");
                        CaptureView("Takoyaki_Recoil_InGame", sniper.transform.position + Vector3.up * 0.22f);
                        Check(UnityEngine.Object.FindObjectsOfType<Projectile>().Any(p => p.GetComponent<SpriteRenderer>() != null && p.GetComponent<SpriteRenderer>().sprite.name == "Takoyaki"), "Real sniper shot uses takoyaki projectile");
                        next = EditorApplication.timeSinceStartup + 0.7;
                        break;
                    case 7:
                        Check(!sniper.GetComponent<SniperRecoilVisual>().IsPlaying, "Bonito settles and recoil completes");
                        Call(trap, "ActivateTrap", character);
                        trap.gameObject.SetActive(false);
                        Check(!character.IsTrapBound, "Disabling trap cleans up capture and attack gate");
                        next = EditorApplication.timeSinceStartup + 0.2;
                        break;
                    case 8:
                        EditorApplication.ExitPlaymode();
                        break;
                }
                SessionState.SetInt(Key + "Phase", phase + 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SessionState.SetBool(Key + "Failed", true);
                SessionState.SetInt(Key + "Phase", 9);
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
