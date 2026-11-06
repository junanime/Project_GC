#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ColonBossSmoke
    {
        const string Key="ColonBossSmoke",Output="Library/ColonBossProof";
        static bool started;static double deadline;static int checks;
        [Serializable]class Entry{public string id;}
        [Serializable]class Catalog{public Entry[] entries;}
        [Serializable]class Pref{public string key;public bool had;public int value;}
        [Serializable]class Backup{public Pref[] prefs;}
        static ColonBossSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);Restore();checks=0;
            File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/patterns.csv","kind,index,seconds,completed,health,width,height\n");
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{CapsuleDoctorDialogue.SeenKey,"Coins","LobbySilverCoins"}).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();CrossSceneData.ClearPendingRunSceneTransfer();
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+1800;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.timeScale=1;SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Colon boss integration QA");Object.DontDestroyOnLoad(host);
                host.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Checks());
            }
        }
        static void Restore()
        {
            string path=Output+"/prefs.json";if(!File.Exists(path))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)
                if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(path);
        }
        static void Check(bool value,string message)
        {
            if(!value)throw new Exception("COLON_BOSS_FAIL "+message);
            checks++;File.AppendAllText(Output+"/checks.txt",message+"\n");Debug.Log("COLON_BOSS_PASS "+message);
        }
        static FieldInfo Field(object o,string name)
        {
            for(var t=o.GetType();t!=null;t=t.BaseType)
            {var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new MissingFieldException(o.GetType().Name,name);
        }
        static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static void Position(Character p,Vector2 at)=>ColonContentSmoke.Position(p,at);
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn)
                {b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())
                if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
        }
        static void ClearProjectiles()
        {
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
        }
        static Bounds VisibleBounds(ColonBossRuntime boss)
        {
            var renderers=boss.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite!=null&&r.color.a>.01f).ToArray();
            if(renderers.Length==0)return new Bounds(boss.transform.position,Vector3.zero);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        static IEnumerator Checks()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");
            yield return null;Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"main menu enters actual stage-one loading");
            float until=Time.realtimeSinceStartup+120;while(StageEntryLoading.IsLoading&&Time.realtimeSinceStartup<until)yield return null;
            Check(!StageEntryLoading.IsLoading,"stage-one loading completed");
            var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;
            if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
            RunScore.Exclude("대장 선택 보스 검증");
            Silence(level);Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;Time.timeScale=1;
            player.AddDamageMultiplier(.17f);float carryDamage=player.DamageMultiplier;
            var inventory=level.PlayerInventory;var skills=player.Skills;
            yield return ColonContentSmoke.Enter(level);Silence(level);ClearProjectiles();
            var map=FiniteStomachMap.Instance;var director=level.GetComponent<ColonBossDirector>();var progression=StageProgression.Ensure(level);
            Check(map.IsColon&&progression.StageNumber==2,"stage-two map and progression active");
            Check(player==level.PlayerCharacter&&inventory==level.PlayerInventory&&skills==player.Skills&&Mathf.Approximately(carryDamage,player.DamageMultiplier),"stage-one player, inventory, skills and upgrades survive transition");
            Check(level.InitialFieldObjectsReady&&director!=null&&director.Ready&&!director.Failed,"loading waits for all six habitats");
            Check(director.Habitats.Count==6&&director.Habitats.Select(h=>h.Kind).Distinct().Count()==6,"one live habitat for every accepted boss");
            var field=level.GetComponent<FieldObjectLayout>();Check(field.SpawnedObjects.Count==86,"all 86 ordinary field objects retained");
            Check(level.EntityManager.LivingMonsters.OfType<BloodClotMonster>().Count()==14,"all fourteen clot entrances coexist with boss habitats");
            Check(level.GetComponent<BuriedBossDirector>().Part==null&&!level.GetComponent<BuriedBossDirector>().CanSummon,"stage-one buried parts retired in colon");
            var legacy=Object.FindObjectOfType<FinalBossSummonInteractable>();
            Check(legacy==null||(!legacy.TryAutomaticSummon()&&!legacy.OpenBossChoice(player)),"legacy altar cannot create a stage-one final boss in colon");
            var ids=director.Habitats.Select(h=>h.GetInstanceID()).ToArray();director.PrepareStage(player.transform.position);
            Check(ids.SequenceEqual(director.Habitats.Select(h=>h.GetInstanceID())),"repeated prepare preserves homes and consumed state");
            var layout=ColonBossLayoutValidation.Run(level,256,Output+"/layout-report.json");
            Check(layout.success,"256 real-geometry seeds have complete safe spaced layouts");
            foreach(var h in director.Habitats)
            {
                var position=(Vector2)h.transform.position;
                Position(player,map.ClampToField(position+Vector2.down*3,map.WallWidth+.4f));yield return new WaitForSeconds(.3f);
                Capture("home-"+h.Kind);
            }
            ColonBossRuntime held=null;
            foreach(ColonBossKind kind in Enum.GetValues(typeof(ColonBossKind)))
            {
                var home=director.Habitats.First(h=>h.Kind==kind);var at=(Vector2)home.transform.position;
                Position(player,map.ClampToField(at+Vector2.down*2.5f,map.WallWidth+.4f));yield return new WaitForSeconds(.2f);
                InteractionFocus.SelectTouched(home);home.TryInteract();yield return null;
                var boss=Object.FindObjectsOfType<ColonBossRuntime>().FirstOrDefault(b=>b.Kind==kind&&!b.Dead);
                Check(boss!=null,"real interaction creates "+kind);boss.AutoPatterns=false;
                Check(!director.TrySummon(home,player),"repeat interaction cannot duplicate "+kind);
                float health=boss.Health;boss.TakeDamage(boss.MaxHealth*2);Check(boss.Arriving&&boss.Health==health,"arrival is invulnerable "+kind);
                float begin=Time.time;int frame=0;until=Time.time+20;
                while(boss.Arriving&&Time.time<until)
                {
                    if(Time.time-begin>=frame*.55f&&frame<12){Capture("arrival-"+kind+"-"+frame.ToString("D2"));frame++;}
                    yield return null;
                }
                Check(!boss.Arriving&&!boss.Dead&&boss.Health==boss.MaxHealth,"arrival finishes ready at full health "+kind);
                Check(boss.GetComponentsInChildren<Collider2D>().Any(c=>c.enabled),"combat collider enabled after arrival "+kind);
                var initialScale=boss.transform.localScale;
                if(kind==ColonBossKind.Shark&&held!=null)
                {
                    held.transform.position=boss.transform.position+Vector3.left*3;
                    var rb=held.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=held.transform.position;
                    yield return null;Capture("two-active-boss-health-bars");
                    Check(Object.FindObjectsOfType<ColonBossHealthHud>().Length>=2,"two simultaneous bosses have independent health HUDs");
                    float secondHealth=boss.Health;held.TakeDamage(1);Check(boss.Health==secondHealth,"simultaneous health pools are independent");
                    held.TakeDamage(held.MaxHealth*2);yield return new WaitForSeconds(1.5f);held=null;
                }
                var definition=ColonBossDefinition.Get(kind);Check(!boss.PlayPattern(-1)&&!boss.PlayPattern(definition.PatternCount),"invalid pattern indices rejected "+kind);
                for(int pattern=0;pattern<definition.PatternCount;pattern++)
                {
                    Position(player,map.ClampToField((Vector2)boss.transform.position+Vector2.right*4+Vector2.down,map.WallWidth+.5f));
                    int previous=boss.CompletedPatterns;Check(boss.PlayPattern(pattern),"starts "+kind+" pattern "+pattern);
                    begin=Time.time;until=Time.time+35;frame=0;
                    while(boss.Busy&&Time.time<until)
                    {
                        if(Time.time-begin>=.25f+frame*.7f&&frame<4){Capture("pattern-"+kind+"-"+pattern+"-"+frame);frame++;}
                        CheckFinite(boss.transform.position,kind);
                        yield return null;
                    }
                    Check(!boss.Busy&&boss.CompletedPatterns==previous+1,"finishes "+kind+" pattern "+pattern);
                    Check((boss.transform.localScale-initialScale).sqrMagnitude<.00001f,"root body size preserved "+kind+" pattern "+pattern);
                    Check(Object.FindObjectsOfType<FoodBossBullet>().Length==0,"no inherited basic/radial barrage "+kind+" pattern "+pattern);
                    var bounds=VisibleBounds(boss);Check(bounds.size.x>0&&bounds.size.y>0,"visible intact body after "+kind+" pattern "+pattern);
                    File.AppendAllText(Output+"/patterns.csv",$"{kind},{pattern},{Time.time-begin:F3},{boss.CompletedPatterns},{boss.Health:F2},{bounds.size.x:F3},{bounds.size.y:F3}\n");
                }
                var specials=new SyringeSpecialRuntime{poisonEnabled=true,poisonDuration=1,poisonTickInterval=.1f,poisonTickDamage=7,corrosionEnabled=true,corrosionDuration=3,corrosionMaxStacks=3,corrosionBossDamageTakenBonusPerStack=.05f,markEnabled=true,markDuration=3};
                Ver4HitEffects.AfterHit(boss,specials,player,~0,false);health=boss.Health;yield return new WaitForSeconds(.25f);
                Check(boss.Health<health,"poison damage applies "+kind);
                Check(boss.GetComponent<CorrosionStatus>()?.CurrentStacks==1,"corrosion stack applies "+kind);
                Check(boss.GetComponent<NeedleMarkStatus>()?.TryConsume()==true,"needle mark applies "+kind);
                IceSkillRules.Freeze(boss);Check(boss.GetComponent<FoodBossCombatStatus>()!=null,"boss freeze/status adapter exists "+kind);
                Capture("status-"+kind);
                if(kind==ColonBossKind.Flamingo){held=boss;continue;}
                boss.TakeDamage(boss.MaxHealth*2);yield return new WaitForSeconds(1.5f);
                Check(director.IsDefeated(home)&&!director.CanSummon(home),"defeated home cannot be farmed twice "+kind);
                Check(!level.IsLevelEnded&&progression.Portal==null&&!progression.FirstBossDefeated,"optional defeat never clears stage or opens exit "+kind);
            }
            Check(director.DefeatedCount==6,"all six distinct optional boss defeats recorded");
            Check(!ColonBossDefinition.RequiredFinalBossImplemented&&ColonBossDefinition.RequiredFinalBossName=="변비왕","required final boss remains explicitly reserved");
            yield return SupplementalIntegration(level,player);
            yield return NeedleBenchmark(level,player);
            SessionState.SetBool(Key+"Done",true);
        }
        static void CheckFinite(Vector3 value,ColonBossKind kind)
        {if(float.IsNaN(value.x)||float.IsInfinity(value.x)||float.IsNaN(value.y)||float.IsInfinity(value.y))throw new Exception("Invalid boss position "+kind);}

        static IEnumerator SupplementalIntegration(LevelManager level,Character player)
        {
            Silence(level);ClearProjectiles();
            var map=FiniteStomachMap.Instance;var at=map.ClampToField(map.UVToWorld(new Vector2(.76f,.72f)),map.WallWidth+8);
            Position(player,at);
            var trio=new[]{ColonBossRuntime.Spawn(ColonBossKind.Flamingo,player,at+Vector2.left*3,null),ColonBossRuntime.Spawn(ColonBossKind.Pudding,player,at+Vector2.up*4,null),ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,at+Vector2.right*4,null)};
            foreach(var b in trio)b.AutoPatterns=false;
            float until=Time.time+20;while(trio.Any(b=>b.Arriving)&&Time.time<until)yield return null;
            Check(trio.All(b=>!b.Arriving),"supplemental three bosses finish arrival");yield return null;
            var hud=trio.Select(b=>b.GetComponent<ColonBossHealthHud>()).ToArray();
            Check(hud.All(h=>h!=null&&((GameObject)Field(h,"root").GetValue(h)).activeInHierarchy),"three nearby bosses expose three simultaneous health bars");
            var ys=hud.Select(h=>((RectTransform)Field(h,"panel").GetValue(h)).anchoredPosition.y).OrderBy(y=>y).ToArray();
            Check(ys[1]-ys[0]>=40&&ys[2]-ys[1]>=40,"three boss health bars occupy separate rows");Capture("three-active-boss-health-bars");
            var turtle=trio[2];Position(player,(Vector2)turtle.transform.position+Vector2.left*5);
            Check(turtle.PlayPattern(0),"status test starts turtle dash");yield return new WaitForSeconds(1.6f);
            var block=turtle.gameObject.AddComponent<NeuralBlockedMonsterStatus>();block.Apply(.85f,false);
            Vector3 stopped=turtle.transform.position;yield return new WaitForSeconds(.30f);
            Check(block.Active&&(turtle.transform.position-stopped).sqrMagnitude<.0001f,"neural block freezes an in-flight boss dash");
            yield return new WaitForSeconds(1.0f);
            Check((turtle.transform.position-stopped).sqrMagnitude>.001f,"boss dash resumes after neural block expires");
            until=Time.time+10;while(turtle.Busy&&Time.time<until)yield return null;Check(!turtle.Busy,"interrupted dash completes without deadlock");
            FoodBossCombatStatus.Apply(turtle,new SyringeSpecialRuntime{honeyEnabled=true,honeyDuration=3,honeySlowMultiplier=.6f},player,false);
            var honey=turtle.GetComponentInChildren<HoneyTrailEmitter>();Check(honey!=null&&turtle.GetComponent<FoodBossCombatStatus>().SpeedMultiplier<1,"honey slow and trail attach to optional boss");
            var initial=turtle.transform.position;for(int i=0;i<4;i++){turtle.MoveTo((Vector2)initial+Vector2.right*(i+1)*.25f);yield return new WaitForSeconds(.3f);}
            Check(honey.Emitted>=1,"moving optional boss leaves a short honey stain");Capture("boss-honey-stain");
            var bullet=ColonBossEffects.Projectile(turtle,(Vector2)player.transform.position+Vector2.up*3,Vector2.right,1,1,.15f,"seal QA");
            var seal=bullet.GetComponent<OctoberEnemyProjectile>();Check(seal!=null&&seal.IsHostile,"optional boss projectile registers for item interception");
            seal.Seal(.6f);stopped=bullet.transform.position;yield return new WaitForSeconds(.25f);
            Check(bullet!=null&&!bullet.enabled&&(bullet.transform.position-stopped).sqrMagnitude<.0001f,"item seal immediately freezes boss projectile");
            yield return new WaitForSeconds(.5f);Check(bullet==null,"sealed boss projectile is removed after seal delay");
            var pudding=trio[1];Check(pudding.PlayPattern(1),"death test starts pudding lob children");yield return new WaitForSeconds(.4f);
            var effects=pudding.Effects;pudding.TakeDamage(pudding.MaxHealth*2);yield return new WaitForSeconds(2);
            Check(pudding==null&&effects==null,"mid-pattern death cancels child animation and removes effect hierarchy");
            foreach(var b in trio)if(b!=null)Object.Destroy(b.gameObject);yield return null;
        }

        static IEnumerator NeedleBenchmark(LevelManager level,Character player)
        {
            Silence(level);ClearProjectiles();
            foreach(var pickup in Object.FindObjectsOfType<Collectable>())pickup.DissolveInAcidLake();
            const string snapshot="Documentation/QA/Stage2CarryoverFixtures/Main-stage1-build.json";
            Check(File.Exists(snapshot),"recorded stage-one carryover build is available");
            var build=JsonUtility.FromJson<RunSceneTransferSnapshot>(File.ReadAllText(snapshot));
            build.CharacterState.SkillActiveRemaining=0;build.CharacterState.SkillCooldownRemaining=0;
            var abilities=Object.FindObjectOfType<AbilityManager>();int restored=abilities.RestoreRunSceneAbilities(build.AbilityStates);
            Check(restored==build.AbilityStates.Count,"recorded entry ability build restored in full");
            player.RestoreRunSceneState(build.CharacterState);Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            OctoberItemRuntime.Get(player).Restore(build.OctoberItems);
            var map=FiniteStomachMap.Instance;var at=map.ClampToField(map.UVToWorld(new Vector2(.76f,.72f)),map.WallWidth+8);
            Position(player,at);player.Move(Vector2.right);player.Move(Vector2.zero);
            foreach(var a in Object.FindObjectsOfType<Ability>())a.enabled=true;
            var boss=ColonBossRuntime.Spawn(ColonBossKind.Turtle,player,at+Vector2.right*2.5f,null);boss.AutoPatterns=false;
            float until=Time.time+20;while(boss.Arriving&&Time.time<until)yield return null;Check(!boss.Arriving,"benchmark target finishes arrival");
            boss.enabled=false;var body=boss.GetComponent<Rigidbody2D>();if(body!=null){body.constraints=RigidbodyConstraints2D.FreezeAll;body.velocity=Vector2.zero;}
            float before=boss.Health,start=Time.time;until=Time.time+20;
            while(boss!=null&&!boss.Dead&&Time.time<until)
            {
                var selection=level.EntityManager.AbilitySelectionDialog;if(selection.MenuOpen)selection.Close();
                yield return null;
            }
            float seconds=Time.time-start,remaining=boss!=null?Mathf.Max(0,boss.Health):0,damage=before-remaining;
            Check(damage>0,"real carryover needle combat damages the optional boss");
            float dps=damage/Mathf.Max(.01f,seconds);
            File.WriteAllText(Output+"/carryover-combat.csv","character,fixture,target,hp_start,hp_end,seconds,damage,dps,killed,note\n"+$"Ashi,2026-10-23 stage1 snapshot,Turtle,{before:F3},{remaining:F3},{seconds:F3},{damage:F3},{dps:F3},{remaining<=0},stationary 2.5-unit target; active skill excluded; not a human win-rate test\n");
            Capture("real-needle-carryover-combat");if(boss!=null)Object.Destroy(boss.gameObject);
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}ClearProjectiles();
        }
        static void Capture(string name)
        {
            var camera=Camera.main;if(camera==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();
            var orders=overlays.Select(c=>c.sortingOrder).ToArray();var overrideSort=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=overrideSort[i];}
                camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
