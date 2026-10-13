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
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class October6CombatSmoke
    {
        const string Key = "October6CombatSmoke";
        const string Output = "Library/October6Proof";
        static double deadline;
        static bool started;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static October6CombatSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys = catalog.entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct().ToArray();
            File.WriteAllText(Output + "/prefs-backup.json", JsonUtility.ToJson(new Backup { values = keys.Select(k => new Saved { key = k, exists = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k) }).ToArray() }));
            foreach (var e in catalog.entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id), 1);
            SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key + "Failed", false);
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
                Debug.Log("[Oct6] FINISHED failed=" + failed); EditorApplication.Exit(failed ? 1 : 0); return;
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (!started && EditorApplication.isPlaying && level != null && level.PlayerCharacter != null && level.CurrentLevelTime > .1f)
            { started = true; level.StartCoroutine(Checks(level)); }
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("[Oct6] FAIL " + message); Debug.Log("[Oct6] PASS " + message); }
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
        static void Select(Ver4AugmentRuntime runtime, P p, int slot, int count)
        { runtime.Progress.RegisterOwnedParent(p.ToString()); for (int i = 0; i < count; i++) runtime.Progress.TrySelect(p.ToString(), slot); runtime.RefreshProgress(); }
        static SyringeProjectile[] Flying() => Object.FindObjectsOfType<SyringeProjectile>().Where(s => s.IsFlying).ToArray();
        static void Capture(string name, Vector3 center, float size = 2.6f)
        {
            var go = new GameObject("Oct6 proof camera"); var camera = go.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = size; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .16f, .18f); go.transform.position = new Vector3(center.x, center.y, -50);
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var old = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); Object.Destroy(pixels); Object.Destroy(go);
        }
        static IEnumerator Checks(LevelManager level)
        {
            yield return null;
            Application.runInBackground = true;
            var prefs = GamePreferences.Current.Copy(); prefs.pauseOnFocusLoss = false; prefs.muteOnFocusLoss = false; GamePreferences.Apply(prefs, false);
            Time.timeScale = 1;
            var player = level.PlayerCharacter;
            var manager = Object.FindObjectOfType<AbilityManager>(); var runtime = manager.Ver4;
            var syringe = SyringeAbilityResolver.FindOwnedOrFirst(manager); syringe.enabled = false; syringe.StopAllCoroutines();
            var entity = level.EntityManager;
            Check(OriginalAugmentProgress.MaxParentLevel == 9, "Three originals, three levels each retained");
            Check(Mathf.Approximately(OriginalCombatRules.FormationDamage(3), 1f) && OriginalCombatRules.FormationEndpointCount(3)==7, "Formation endpoint seven needles replaces damage bonus");
            Check(Mathf.Approximately(OriginalCombatRules.FormationRepeatChance(2), .31f), "Formation repeat stage 2 = 31%");
            Check(Mathf.Approximately(OriginalCombatRules.BipolarDamage(3, 3, true, true), 1.75f), "Cross + alternating bonuses additive, max 1.75x");
            Check(FormationNeedleCollision.Crossed(Vector2.left, Vector2.right, Vector2.down, Vector2.up, .14f), "Swept formation collision catches crossing");
            Check(!FormationNeedleCollision.Crossed(Vector2.left, Vector2.right, Vector2.up * 2, Vector2.up * 3, .14f), "Separated formation needles do not collide");
            var full = player.GetComponentsInChildren<BoxCollider2D>().First(c => c.name == "Full Hitbox");
            var size = full.size; var offset = full.offset;
            CharacterHurtboxFit.Fit(full, (SpriteRenderer)Get(player, "spriteRenderer"));
            Check((full.size - size).sqrMagnitude < .00001f && (full.offset - offset).sqrMagnitude < .00001f, "Hurtbox fits idle art, shrinks around same center without compounding");

            var data = AssetDatabase.LoadAssetAtPath<MonsterBlueprint>("Assets/Prefabs/Monsters/genral Monster BP/3_1.asset");
            Check(data != null, "Existing monster blueprint found");
            var point = (Vector2)player.transform.position + Vector2.right * 20;
            var target = entity.SpawnMonster(0, point, data, 500); target.enabled = false;
            var other = entity.SpawnMonster(0, point + Vector2.right * 2, data, 500); other.enabled = false;
            target.GetComponent<Rigidbody2D>().velocity = Vector2.zero; other.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            var mark = target.gameObject.AddComponent<BipolarMarkStatus>();
            float hp = target.HP; mark.Hit(target, 1, 1, 100, player);
            Check(mark.Polarity == 1 && target.HP == hp, "Yang places mark without extra damage");
            mark.Hit(target, 1, 1, 100, player);
            Check(mark.Polarity == 1 && target.HP == hp, "Same polarity refreshes, does not consume");
            mark.Hit(target, -1, 1, 100, player);
            Check(mark.Polarity == 0 && Mathf.Abs(hp - target.HP - 40) < .1f, "Opposite consumes mark, exactly 40% bonus");
            for (int stage = 2; stage <= 3; stage++)
            { hp = target.HP; mark.Hit(target, -1, stage, 100, player); mark.Hit(target, 1, stage, 100, player); Check(Mathf.Abs(hp - target.HP - (stage == 2 ? 60 : 80)) < .1f, "Polarity bonus stage " + stage); }
            mark.Hit(target, 1, 3, 100, player);
            var mark2 = other.gameObject.AddComponent<BipolarMarkStatus>(); mark2.Hit(other, -1, 3, 100, player);
            yield return null; Capture("polarity-halves", (Vector3)(point + Vector2.right) + Vector3.up * .3f, 1.3f);
            mark.enabled = false; Check(mark.Polarity == 0, "Pooled/disabled enemy clears mark and visual"); mark.enabled = true;

            var explosion = SyringeAugmentVfx.PlayRadius("Explosion", point, 1);
            var doubleExplosion = SyringeAugmentVfx.PlayRadius("Explosion", point, 2);
            Check(Mathf.Abs(doubleExplosion.transform.localScale.x / explosion.transform.localScale.x - 2) < .001f, "Explosion VFX scales with actual radius");
            explosion.Release(); doubleExplosion.Release();
            var wave = SyringeAugmentVfx.PlayRadius("VibrationShockwave", point, 1);
            var bigWave = SyringeAugmentVfx.PlayRadius("VibrationShockwave", point, 1.36f);
            Check(Mathf.Abs(bigWave.transform.localScale.x / wave.transform.localScale.x - 1.36f) < .001f, "Shockwave VFX scales 1.36x with radius upgrades"); wave.Release(); bigWave.Release();
            var puddle = new GameObject("QA acid 1").AddComponent<DigestiveAcidPuddle>(); puddle.transform.position = point;
            var puddle2 = new GameObject("QA acid 2").AddComponent<DigestiveAcidPuddle>(); puddle2.transform.position = point + Vector2.right * 4;
            puddle.Init(1, 1, 0, .2f, Color.green); puddle2.Init(1, 2, 0, .2f, Color.green);
            Check(Mathf.Abs(((SyringeAugmentVfx)Get(puddle2, "augmentVisual")).transform.localScale.x / ((SyringeAugmentVfx)Get(puddle, "augmentVisual")).transform.localScale.x - 2) < .001f, "Acid footprint doubles with radius");
            SyringeAugmentVfx.Play("WoodSprout", point, SyringeAugmentVfx.FindTarget(target));
            SyringeAugmentVfx.PlayBranch(point, point + Vector2.right * 2, SyringeAugmentVfx.FindTarget(other));
            yield return new WaitForSeconds(.18f); Capture("wood-and-acid", point + Vector2.right * 2, 3);

            Select(runtime, P.BipolarNeedle, 0, 3); Select(runtime, P.BipolarNeedle, 1, 3); Select(runtime, P.BipolarNeedle, 2, 3);
            syringe.EnableBipolarNeedleAugment(); Set(syringe, "bipolarEmpowerFront", false);
            foreach (var shot in Flying()) shot.RemoveForItem();
            ((IEnumerator)Call(syringe, "LaunchBipolarSyringes", Vector2.up, 2)).MoveNext();
            var shots = Flying();
            Check(shots.Length == 4, "Cross volley creates exactly four projectiles (actual=" + shots.Length + ")");
            Check(shots.Count(s => ((SyringeSpecialRuntime)Get(s, "specials")).bipolarPolarity == 1) == 2 && shots.Count(s => ((SyringeSpecialRuntime)Get(s, "specials")).bipolarPolarity == -1) == 2, "Front/left Yang and rear/right Yin travel on projectiles");
            float yangDamage = (float)Get(shots.First(s => ((SyringeSpecialRuntime)Get(s, "specials")).bipolarPolarity == 1), "damage");
            float yinDamage = (float)Get(shots.First(s => ((SyringeSpecialRuntime)Get(s, "specials")).bipolarPolarity == -1), "damage");
            Check(Mathf.Abs(yinDamage / yangDamage - 1.75f / 1.25f) < .001f, "First volley empowers rear/right only");
            foreach (var s in shots) s.RemoveForItem();
            ((IEnumerator)Call(syringe, "LaunchBipolarSyringes", Vector2.up, 2)).MoveNext();
            shots = Flying();
            Check(Mathf.Abs((float)Get(shots.First(s => ((SyringeSpecialRuntime)Get(s, "specials")).bipolarPolarity == 1), "damage") - yinDamage) < .001f, "Next volley switches empowered side");
            foreach (var s in shots) s.RemoveForItem();

            Select(runtime, P.Pierce, 1, 3); syringe.EnablePierceAugment();
            Call(syringe, "LaunchSyringeProjectile", Vector2.up, false, 1f, 1);
            var accelerating = Flying().Single();
            float speed = (float)Get(accelerating, "speed"), distance = accelerating.maxDistance;
            for (int i = 0; i < 8; i++) Call(accelerating, "AccelerateAfterPierce");
            Check(Mathf.Abs((float)Get(accelerating, "speed") - speed * 2) < .001f && accelerating.maxDistance == distance, "Pierce speed caps at +100%, range unchanged"); accelerating.RemoveForItem();
            Select(runtime, P.AcupunctureFormation, 0, 1); syringe.EnableAcupunctureFormationAugment();
            var formation = Object.FindObjectOfType<AcupunctureFormationController>();
            Set(formation, "previousIsDashing", true); Set(player, "isDashing", false);
            Call(formation, "Update");
            Check(Flying().Length == 3, "First endpoint formation emits exactly three needles after dash gate closes");
            foreach (var s in Flying()) s.RemoveForItem();
            for (int stage = 2; stage <= 3; stage++)
            {
                Select(runtime, P.AcupunctureFormation, 0, 1);
                Set(formation, "previousIsDashing", true); Call(formation, "Update");
                Check(Flying().Length == 1 + stage * 2, "Endpoint formation stage " + stage + " emits " + (1 + stage * 2));
                foreach (var s in Flying()) s.RemoveForItem();
            }

            var a = (SyringeProjectile)entity.SpawnProjectile((int)Get(syringe, "projectileIndex"), point + Vector2.left, 1, 0, 1, ~0);
            var b = (SyringeProjectile)entity.SpawnProjectile((int)Get(syringe, "projectileIndex"), point + Vector2.right, 1, 0, 1, ~0);
            a.Launch(Vector2.right); b.Launch(Vector2.left); a.StopAllCoroutines(); b.StopAllCoroutines();
            a.transform.position = point + Vector2.left; b.transform.position = point + Vector2.right;
            var ac = a.GetComponent<FormationNeedleCollision>() ?? a.gameObject.AddComponent<FormationNeedleCollision>();
            var bc = b.GetComponent<FormationNeedleCollision>() ?? b.gameObject.AddComponent<FormationNeedleCollision>();
            ac.Initialize(a, formation, 500, 100, player, ~0); bc.Initialize(b, formation, 501, 100, player, ~0);
            a.transform.position = point + Vector2.right; b.transform.position = point + Vector2.left;
            hp = target.HP; Physics2D.SyncTransforms(); Call(ac, "LateUpdate");
            Check(!a.IsFlying && !b.IsFlying && Mathf.Abs(hp - target.HP - 100) < .1f, "Cross-burst collision consumes both needles and deals one radius explosion");

            Select(runtime, P.AcupunctureFormation, 2, 3);
            var randomState = UnityEngine.Random.state;
            int seed = 0;
            for (; seed < 100; seed++) { UnityEngine.Random.InitState(seed); if (UnityEngine.Random.value < .4f) break; }
            UnityEngine.Random.InitState(seed); int burstBefore = (int)Get(formation, "burstId");
            Call(formation, "FireRadialNeedles", (Vector3)point, false);
            yield return new WaitForSeconds(.3f);
            Check((int)Get(formation, "burstId") == burstBefore + 2, "Repeat emits exactly one delayed burst");
            yield return new WaitForSeconds(.3f);
            Check((int)Get(formation, "burstId") == burstBefore + 2, "Repeated formation cannot recursively repeat");
            UnityEngine.Random.state = randomState;

            var npc = Object.FindObjectOfType<MerchantNPC>();
            if (npc == null) npc = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/hyeon/prepabs/Merchant.prefab")).GetComponent<MerchantNPC>();
            Vector3 origin = player.transform.position;
            npc.transform.position = origin + Vector3.left * .9f;
            var director = Object.FindObjectOfType<MiniStageDirector>(); director.OpenEntrancePortal(origin + Vector3.right * .6f);
            var portal = Object.FindObjectsOfType<BloodClotMiniStagePortal>().OrderBy(p => Vector3.Distance(p.transform.position, origin)).First();
            Physics2D.SyncTransforms(); yield return new WaitForSeconds(.15f);
            var loot = npc.GetComponentInChildren<LootInteraction>();
            Check(portal.CanInteract && InteractionFocus.Selected == portal, "Overlap: nearer blood clot beats merchant");
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(KeyCodeToInput())); InputSystem.Update();
            Check(!GameInput.TryConsumeInteraction(KeyCode.E, loot), "Merchant cannot consume clot's highlighted E");
            Check(GameInput.TryConsumeInteraction(KeyCode.E, portal), "Highlighted clot consumes actual E press");
            Check(!GameInput.TryConsumeInteraction(KeyCode.E, portal), "Same press cannot be reused");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update();
            yield return null;
            portal.transform.position = origin + Vector3.right * 1.1f; npc.transform.position = origin + Vector3.left * .6f;
            Physics2D.SyncTransforms(); yield return new WaitForSeconds(.15f);
            Check(InteractionFocus.Selected == loot, "Overlap focus switches to nearer merchant");
            yield return new WaitForSeconds(.05f);
            var border = npc.GetComponentsInChildren<LineRenderer>().First(l => l.name == "Active E golden border");
            Check(border.enabled && border.startWidth < .02f && border.bounds.size.x < .6f, "Golden E border is thin and stays within key dimensions");
            Capture("interaction-focus", origin + Vector3.up * .3f, 2.2f);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(KeyCodeToInput())); InputSystem.Update();
            loot.SendMessage("Update");
            Check(Time.timeScale == 0 && ApothecaryUI.Instance.Page == "merchant", "Highlighted merchant opens actual shop through E Update");
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState()); InputSystem.Update();
            MerchantUIManager.Instance.CloseShop();
            SessionState.SetBool(Key + "Done", true);
        }
        static Key KeyCodeToInput() => UnityEngine.InputSystem.Key.E;
    }
}
