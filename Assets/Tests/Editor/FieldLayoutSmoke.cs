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
    public static class FieldLayoutSmoke
    {
        const string Key = "FieldLayoutSmoke";
        const string Output = "Library/FieldLayoutProof";
        static double deadline;
        static bool started;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        static FieldLayoutSmoke() { if (SessionState.GetBool(Key, false)) Attach(); }
        public static void Run()
        {
            Vampire.Editor.CurrencyJarArtImporter.Prepare();
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
                Debug.Log("[FieldLayout] FINISHED failed=" + failed); EditorApplication.Exit(failed ? 1 : 0); return;
            }
            var level = Object.FindObjectOfType<LevelManager>();
            if (!started && EditorApplication.isPlaying && level != null && level.PlayerCharacter != null && level.CurrentLevelTime > .1f)
            { started = true; level.StartCoroutine(Checks(level)); }
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("[FieldLayout] FAIL " + message); Debug.Log("[FieldLayout] PASS " + message); }
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
        static void Shot(string name)
        {
            var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);
        }
        static void CloseShot(Camera camera,string name)
        {
            var target=new RenderTexture(1000,700,24);camera.targetTexture=target;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1000,700,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1000,700),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(texture);Object.Destroy(target);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            Application.runInBackground=true;Time.timeScale=1;Time.captureDeltaTime=1f/60;
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var layout=level.GetComponent<FieldObjectLayout>();
            for(int i=0;i<180 && (layout==null||!layout.Ready);i++){yield return null;layout=level.GetComponent<FieldObjectLayout>();}
            Check(layout!=null&&layout.Ready,"stage-one initial layout installs");
            Check(layout.Slots.Count==86,"86 initial slots");
            Check(Object.FindObjectsOfType<Chest>().Length==24,"24 actual chests, no legacy startup extra");
            Check(Object.FindObjectsOfType<MerchantNPC>().Length==12,"12 actual merchants");
            Check(Object.FindObjectsOfType<BloodClotMonster>().Length==14,"14 actual blood clots");
            Check(Object.FindObjectsOfType<EliteMonsterSummonInteractable>().Length==8,"8 actual elite altars");
            Check(Object.FindObjectsOfType<CurrencyJar>().Count(j=>!j.Silver)==20&&Object.FindObjectsOfType<CurrencyJar>().Count(j=>j.Silver)==8,"20 gold and 8 silver jars");
            var player=level.PlayerCharacter;Set(player,"isInvincible",true);
            int pairs=0;
            for(int seed=0;seed<250;seed++)
            {
                var points=FieldObjectLayout.Generate(FiniteStomachMap.Instance,player.transform.position,seed);
                Check(points.Count==86&&Enumerable.Range(0,6).All(k=>points.Count(s=>(int)s.kind==k)==FieldObjectLayout.Counts[k]),"seed "+seed+" counts");
                Check(points.All(s=>FiniteStomachMap.Instance.ExactDistance(s.position,out _)>=9.7f),"seed "+seed+" safe flat field");
                bool safe=true;
                for(int a=0;a<points.Count;a++)for(int b=a+1;b<points.Count;b++)if(Vector2.Distance(points[a].position,points[b].position)<3.99f)safe=false;
                Check(safe,"seed "+seed+" no overlap");pairs+=points.Count(s=>s.partner>=0);
            }
            Check(pairs>250&&pairs<5000,"occasional clustered pairs across seeds: "+pairs);
            File.WriteAllText(Output+"/layout.csv","kind,x,y,partner\n"+string.Join("\n",layout.Slots.Select(s=>$"{s.kind},{s.position.x},{s.position.y},{s.partner}")));
            // Advance scheduler inputs without waiting fifteen real minutes or triggering the final boss.
            Set(level,"timeSinceLastChestSpawned",1000f);Call(level,"HandleChestSpawn");
            foreach(var spawner in Object.FindObjectsOfType<NPCSpawner>())Call(spawner,"TrySpawnMerchantByChance");
            var host=Object.FindObjectOfType<TimedSpecialMonsterSpawner>();
            foreach(var field in new[]{"bloodClotSpawns","summonObjectSpawns"})
                foreach(var module in (System.Collections.IEnumerable)Get(host,field))Call(module,"OnTick");
            Check(Object.FindObjectsOfType<Chest>().Length==24&&Object.FindObjectsOfType<MerchantNPC>().Length==12&&Object.FindObjectsOfType<BloodClotMonster>().Length==14,"legacy timed object spawning suppressed");
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;
            var needle=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());needle.enabled=false;needle.StopAllCoroutines();ClearShots();
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            player.CollectableCollider.enabled=false;
            var origin=(Vector2)player.CenterTransform.position;
            var parent=new GameObject("Jar visual test").transform;
            var gold=CurrencyJar.Create(level,origin+Vector2.right*.85f-Vector2.up*CurrencyJar.Height*.45f,false,parent);
            var silver=CurrencyJar.Create(level,origin+Vector2.right*.85f+Vector2.down*.6f,true,parent);
            Check(Mathf.Abs(gold.GetComponentsInChildren<SpriteRenderer>().First(s=>s.name=="Glass jar").bounds.size.y-CurrencyJar.Height)<.001f,"jar visible height = 55% of Ashi body");
            Check(CurrencyJar.RollAmount(false,.499f)==10&&CurrencyJar.RollAmount(false,.50f)==30&&CurrencyJar.RollAmount(false,.85f)==50,"gold drop thresholds");
            Check(CurrencyJar.RollAmount(true,.499f)==5&&CurrencyJar.RollAmount(true,.50f)==10&&CurrencyJar.RollAmount(true,.88f)==20,"silver drop thresholds");
            int[] g=new int[3],s=new int[3];
            for(int i=0;i<10000;i++){int a=CurrencyJar.RollAmount(false,i/10000f),b=CurrencyJar.RollAmount(true,i/10000f);g[a==10?0:a==30?1:2]++;s[b==5?0:b==10?1:2]++;}
            Check(g.SequenceEqual(new[]{5000,3500,1500})&&s.SequenceEqual(new[]{5000,3800,1200}),"exact configured probability partitions");
            level.SetRunFlowPaused(true);gold.TakeDamage(1);Check(!gold.Broken,"jar immune during transfer pause");level.SetRunFlowPaused(false);
            MiniStageRuntimeState.EnterMiniStage(Object.FindObjectOfType<MiniStageDirector>());gold.TakeDamage(1);Check(!gold.Broken,"field jars inactive in mini stage");MiniStageRuntimeState.ForceClear();
            gold.TakeDamage(0);Check(!gold.Broken,"zero damage does not break jar");
            var camera=Object.Instantiate(Camera.main);camera.name="Jar proof camera";camera.enabled=false;camera.orthographicSize=1.05f;
            camera.transform.position=new Vector3(origin.x+.3f,origin.y-.05f,Camera.main.transform.position.z);
            foreach(var behaviour in camera.GetComponents<MonoBehaviour>())behaviour.enabled=false;
            foreach(var listener in camera.GetComponents<AudioListener>())listener.enabled=false;
            yield return new WaitForEndOfFrame();Shot("jars-in-game");CloseShot(camera,"jars-close");
            Physics2D.SyncTransforms();Set(needle,"homingEnabled",false);Fire(needle,Vector2.right);
            for(int i=0;i<40&&!gold.Broken;i++)yield return new WaitForFixedUpdate();
            Check(gold.Broken,"actual needle collider breaks gold jar");
            silver.TakeDamage(1);int goldValue=gold.DropAmount,silverValue=silver.DropAmount;
            gold.TakeDamage(999);silver.TakeDamage(999);
            Check(parent.GetComponentsInChildren<CurrencyJarDrop>().Length==2,"repeated hits create exactly one payout per jar");
            for(int i=0;i<22;i++){yield return new WaitForEndOfFrame();CloseShot(camera,"break-"+i.ToString("D2"));yield return new WaitForSeconds(.035f);}
            Check(parent.GetComponentsInChildren<CurrencyJarDrop>().All(d=>d.Landed&&!d.Collected),"coins land and wait outside pickup area");
            var stats=Object.FindObjectOfType<StatsManager>();int beforeGold=stats.CoinsGained,beforeSilver=SilverWallet.Silver,beforeRun=SilverRunRewarder.Instance?.RunEarnedSilver??0;
            player.CollectableCollider.enabled=true;player.transform.position=origin+Vector2.right*.85f;Physics2D.SyncTransforms();
            yield return new WaitForSeconds(1.2f);
            int expected=StageEventRuntimeModifiers.ApplyCoinValueMultiplier(goldValue);var gen=player.GetComponent<PlayerGeneralStatRuntime>();if(gen!=null)expected=gen.ApplyGoldGainMultiplier(expected);
            Check(stats.CoinsGained-beforeGold==expected,"gold credited once with existing gain modifiers");
            Check(SilverWallet.Silver-beforeSilver==silverValue&&SilverRunRewarder.Instance.RunEarnedSilver-beforeRun==silverValue,"silver credited once to wallet and run record");
            Check(parent.GetComponentsInChildren<CurrencyJarDrop>().Length==0,"collected coin objects removed");
            int savedSilver=SilverWallet.Silver;SilverRunRewarder.RewardMonsterKill(null);
            Check(SilverWallet.Silver==savedSilver,"jar tracking does not enable disabled monster silver rewards");
            // Actual portal entry and return: verify the same field objects survive without respawning.
            var director=Object.FindObjectOfType<MiniStageDirector>();var clot=Object.FindObjectsOfType<BloodClotMonster>().First();
            player.transform.position=clot.transform.position+Vector3.left;
            clot.TakeDamage(100000);yield return new WaitForSeconds(.5f);
            var portal=Object.FindObjectsOfType<BloodClotMiniStagePortal>().OrderBy(p=>Vector2.Distance(p.transform.position,player.transform.position)).First();
            director.EnterMiniStageFromPortal(portal);
            for(int i=0;i<420&&(!director.IsInsideMiniStage||director.IsTransitioning);i++)yield return null;
            Check(director.IsInsideMiniStage&&!director.IsTransitioning,"actual mini-stage entry completes");
            var room=director.CurrentRoom;Set(room,"optionalReturnUnlocked",true);Set(room,"awaitingSkillReward",false);
            director.ReturnToFieldFromInteractable(room.ReturnInteractable);
            for(int i=0;i<420&&(director.IsInsideMiniStage||director.IsTransitioning);i++)yield return null;
            Check(!director.IsInsideMiniStage&&!director.IsTransitioning,"actual mini-stage return completes");
            Check(layout.Ready&&layout.Slots.Count==86&&Object.FindObjectsOfType<CurrencyJar>().Length==28,"field layout persists across portal roundtrip");
            Check(Object.FindObjectsOfType<MerchantNPC>().Length==12,"no duplicate merchants after return");
            yield return new WaitForEndOfFrame();Shot("after-portal");
            player.CollectableCollider.enabled=false;
            var aoe=CurrencyJar.Create(level,(Vector2)player.transform.position+Vector2.right,false,parent);
            var bombJar=CurrencyJar.Create(level,(Vector2)player.transform.position+Vector2.right*4,true,parent);
            Physics2D.SyncTransforms();
            int kills=stats.MonstersKilled;float damageBefore=stats.DamageDealt;
            Ver4HitEffects.AreaDamage(aoe.transform.position,.4f,1,0,player,LayerMask.GetMask("Monster Full"),"jar-test");
            Check(aoe.Broken&&!bombJar.Broken,"shared player area attacks break only in-range jars");
            ThrownConsumableBomb.Launch(player,level.EntityManager,bombJar.transform.position,300,CurrencyJarArt.Coin(false));
            yield return new WaitForSeconds(.8f);
            Check(bombJar==null||bombJar.Broken,"consumable bomb explosion breaks jar");
            Check(stats.MonstersKilled==kills&&Mathf.Approximately(stats.DamageDealt,damageBefore),"jars grant no monster kills or combat damage score");
            Object.Destroy(camera.gameObject);Object.Destroy(parent.gameObject);
            File.WriteAllText(Output+"/result.txt","PASS: 250 seeds, initial object counts, real projectile/area/bomb break, drop odds, one-time gold/silver collection, actual portal roundtrip.\n");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
