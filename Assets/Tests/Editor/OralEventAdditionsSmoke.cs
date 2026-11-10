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
    /// <summary>One real character session per process; run -oralCharacter=0..3 for the full matrix.</summary>
    [InitializeOnLoad]
    public static class OralEventAdditionsSmoke
    {
        const string Key="OralEventAdditionsSmoke", Root="Library/OralEventAdditionsProof";
        static readonly string[] Names={"ashi","ari","hyuki","shini"};
        static readonly string[] Blueprints={"Main","Ari","Hyuki","Shini"};
        static int Index=>SessionState.GetInt(Key+"Index",0);
        static string Output=>Root+"/"+Index+"-"+Names[Index];
        static bool started; static double deadline; static float shotSize=5f;
        [Serializable] sealed class Pref { public string key; public bool had; public int value; }
        [Serializable] sealed class Backup { public Pref[] prefs; }
        static OralEventAdditionsSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedActualCharacterSelection()
        {
            if(!SessionState.GetBool(Key,false))return;
            // Editor domain reload clears CrossSceneData. Restore the same selection
            // before Character.Awake, exactly where a normal scene transfer uses it.
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Blueprints[Index]+" Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Root);Restore();
            File.WriteAllText(Root+"/result.txt","RUNNING");File.WriteAllText(Root+"/checks.txt","");File.WriteAllText(Root+"/errors.txt","");
            var keys=new[]{"Coins","LobbySilverCoins"};
            File.WriteAllText(Root+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            File.WriteAllText(Root+"/capture-method.txt","Actual Unity play-mode camera renders; four separate gameplay sessions using each actual CharacterBlueprint. Fixed 20 fps simulation during recorded sequences only. Events and actor positioning are controlled for reproducible QA. No sprite editing or interpolated frames.\n");
            string selected=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-oralCharacter="));
            int selectedIndex=selected!=null?int.Parse(selected.Substring("-oralCharacter=".Length),CultureInfo.InvariantCulture):0;
            if(selectedIndex<0||selectedIndex>=Names.Length)throw new ArgumentOutOfRangeException("oralCharacter");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetInt(Key+"Index",selectedIndex);SessionState.SetInt(Key+"Checks",0);BeginSession();
        }
        static void BeginSession()
        {
            SessionState.SetBool(Key+"Done",false);started=false;Directory.CreateDirectory(Output);
            CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+Blueprints[Index]+" Character Blueprint.asset");
            if(CrossSceneData.CharacterBlueprint==null)throw new Exception("Missing actual character blueprint "+Names[Index]);
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+900;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Root+"/errors.txt",Names[Index]+": "+text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void Restore()
        {
            var file=Root+"/prefs.json";if(!File.Exists(file))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(file)).prefs)if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(file);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Root+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.timeScale=1;Time.captureFramerate=0;SessionState.SetBool(Key,false);
                bool failed=SessionState.GetBool(Key+"Failed",false);
                string result=(failed?"FAIL":"PASS")+" checks="+SessionState.GetInt(Key+"Checks",0)+" character="+Names[Index]+" characterSessions=1";
                File.WriteAllText(Root+"/result.txt",result);File.WriteAllText(Output+"/result.txt",result);
                File.Copy(Root+"/checks.txt",Output+"/checks.txt",true);File.Copy(Root+"/errors.txt",Output+"/errors.txt",true);
                EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool condition,string message)
        {
            if(!condition)throw new Exception("ORAL_JAW_CRUSH_FAIL "+Names[Index]+": "+message);
            SessionState.SetInt(Key+"Checks",SessionState.GetInt(Key+"Checks",0)+1);
            File.AppendAllText(Root+"/checks.txt","PASS "+Names[Index]+": "+message+"\n");Debug.Log("ORAL_JAW_CRUSH_PASS "+Names[Index]+": "+message);
        }
        static FieldInfo Field(object owner,string name)
        {
            for(var type=owner.GetType();type!=null;type=type.BaseType){var f=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new MissingFieldException(owner.GetType().Name,name);
        }
        static void Set(object owner,string name,object value)=>Field(owner,name).SetValue(owner,value);
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
        }
        static void Clear(LevelManager level)
        {
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(m!=null)m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
        }
        static void Place(Character player,Vector2 p)
        {
            var runtime=OralStageRuntime.Instance;runtime.TeleportPlayer(p,OralMapGeometry.ZoneAt(runtime.Map,p)==OralZone.Teeth?OralZone.Teeth:OralZone.Tongue);player.Move(Vector2.zero);Physics2D.SyncTransforms();
        }
        static IEnumerator Checks(LevelManager level)
        {
            var preferences=GamePreferences.Current.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Oral event additions integration QA");Silence(level);
            Check(StageProgression.Ensure(level).BeginDirectOral(),"stage zero entry");
            while(OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)yield return null;
            Silence(level);Clear(level);var player=level.PlayerCharacter;var map=FiniteStomachMap.Instance;var events=OralFieldEvents.Instance;
            events.AutoSchedule=false;foreach(var c in events.Cavities){c.Healed=true;c.Active=false;}
            Place(player,OralMapGeometry.TongueCenter(map));Set(player,"isInvincible",true);
            var additions=events.Additions;Check(additions!=null,"new event controller installed");
            var group=level.CurrentLevelBlueprint.monsters.Select((g,i)=>new{g,i}).First(x=>x.g.monsterBlueprints.Any(b=>b!=null&&OralMonsterRoster.IsBlueprint(b)));
            var bp=group.g.monsterBlueprints.First(b=>b!=null&&OralMonsterRoster.IsBlueprint(b));
            var monsters=new List<Monster>();
            for(int i=0;i<8;i++){var m=level.EntityManager.SpawnMonster(group.i,player.Position+new Vector2((i%4-1.5f)*1.1f,1+i/4),bp,0,false);monsters.Add(m);}
            Check(events.ForceEvent(OralEventKind.Hiccup),"hiccup starts");Check(!events.ForceEvent(OralEventKind.Ceiling)&&!events.ForceEvent(OralEventKind.Brush),"disruptive events cannot overlap");
            float hold=0;bool saw=false;
            yield return Record("01-hiccup",()=>events.CurrentEvent!=OralEventKind.None,()=>{
                if(additions.HiccupHolding){hold+=Time.deltaTime;if(!saw){saw=true;Check(additions.Lifts.Count==8,"all eight field monsters lifted");foreach(var lift in additions.Lifts){Check(Mathf.Abs(lift.Height-additions.LiftHeight)<.01f,"three-character-height lift");Check(lift.Footprint.sortingLayerName=="GroundEffects","shadow remains ground layer");}Check(monsters.All(m=>m.IsFieldRuntimeSuspended),"airborne contact physics suspended");}}
            });
            Check(saw&&Mathf.Abs(hold-3)<.12f,"full three-second airborne hold");Check(monsters.All(m=>!m.IsFieldRuntimeSuspended&&!m.BodyRenderer.forceRenderingOff),"landing restores physics and body renderers");
            Clear(level);Place(player,OralMapGeometry.TongueCenter(map));
            for(int i=0;i<12;i++)level.EntityManager.SpawnMonster(group.i,player.Position+new Vector2(i%4-1.5f,1+i/4),bp,0,false);
            Check(Mathf.Abs(additions.BrushWidth-2.4f)<.01f,"brush width is 1.2 times Fanta's effective two-unit width");
            Check(events.ForceEvent(OralEventKind.Brush),"brush begins");var passes=new HashSet<int>();
            yield return Record("02-toothbrush",()=>events.CurrentEvent!=OralEventKind.None,()=>{passes.Add(additions.BrushPass);});
            Check(passes.SetEquals(new[]{0,1,2}),"exactly horizontal vertical horizontal passes");Check(additions.BrushMonsterHits>0,"real brush hits and pushes monsters");Check(!additions.BrushWarningVisible,"brush warning cleans up");
            Clear(level);Check(events.ForceEvent(OralEventKind.BalloonDelivery),"delivery begins");
            var delivery=additions.Delivery;Check(delivery!=null&&delivery.ChestSprite==RemakeBalance.Current.levelUpChest.Visual(RemakeBalance.Current.levelUpChest.closedChest),"exact mimic-room chest sprite");
            bool fired=false;float start=Time.time;int before=Object.FindObjectsOfType<Chest>().Length;
            yield return Record("03-bubble-delivery",()=>Time.time-start<5,()=>{
                if(!fired&&Time.time-start>2){fired=true;var dart=SyringeAbilityResolver.FindOwnedOrFirst(Object.FindObjectOfType<AbilityManager>());int pool=level.EntityManager.AddPoolForProjectile(dart.ProjectilePrefab);var p=(SyringeProjectile)level.EntityManager.SpawnProjectile(pool,delivery.BalloonPosition-Vector2.right*1.1f,10,0,3,~0);Set(p,"useLaunchForwardOffset",false);p.Launch(Vector2.right);}
            });
            Check(fired&&delivery==null,"real needle physics pops balloon and completes drop");Check(Object.FindObjectsOfType<Chest>().Length==before+1,"landing creates exactly one real chest");
            var reward=Object.FindObjectsOfType<Chest>().OrderBy(c=>Vector2.Distance(c.transform.position,player.Position+new Vector2(2,1))).First();
            Check(reward.CanInteract,"landed reward is interactable");reward.OpenChest();
            yield return new WaitForSecondsRealtime(.5f);Check(level.EntityManager.AbilitySelectionDialog.MenuOpen,"delivered chest opens real level-up selection");
            level.EntityManager.AbilitySelectionDialog.Close();Time.timeScale=1;yield return null;
            Check(events.ForceEvent(OralEventKind.BalloonDelivery),"second delivery appears after previous landing");var pending=additions.Delivery;
            yield return new WaitForSeconds(1.6f);Check(events.ForceEvent(OralEventKind.BalloonDelivery),"appearance notification can repeat safely");
            Check(additions.Delivery==pending&&Object.FindObjectsOfType<OralBalloonDelivery>().Length==1,"unopened balloon never duplicates");
            yield return new WaitForSeconds(1.6f);
            MiniStageRuntimeState.EnterMiniStage(null);yield return null;yield return null;
            Check(!pending.GetComponent<Collider2D>().enabled&&pending.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"mini-stage isolation hides delivery and disables its hitbox");
            Check(!events.ForceEvent(OralEventKind.Brush),"new event is blocked inside mini-stage");MiniStageRuntimeState.ExitMiniStage(null);yield return null;yield return null;
            Check(pending.GetComponent<Collider2D>().enabled&&pending.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"delivery returns after mini-stage exit");
            Place(player,OralMapGeometry.TongueCenter(map));Check(events.ForceEvent(OralEventKind.Ceiling),"existing ceiling event begins while delivery floats");
            while(events.CeilingPhase<2)yield return null;yield return null;
            Check(!pending.GetComponent<Collider2D>().enabled&&pending.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"real ceiling transition hides pending delivery");
            foreach(var monster in events.CeilingMonsters.ToArray())monster.TakeDamage(monster.HP+1000);
            while(events.CurrentEvent!=OralEventKind.None)yield return null;yield return null;
            Check(pending.GetComponent<Collider2D>().enabled,"ceiling completion restores delivery targeting");
            var poolActor=level.EntityManager.SpawnMonster(group.i,player.Position+Vector2.right*2,bp,0,false);
            Check(events.ForceEvent(OralEventKind.Hiccup),"pause test starts");yield return new WaitForSeconds(1.8f);level.SetRunFlowPaused(true);float age=events.Elapsed;
            yield return new WaitForSecondsRealtime(.3f);Check(Mathf.Approximately(age,events.Elapsed),"pause freezes event clock");level.SetRunFlowPaused(false);
            Check(poolActor.GetComponent<OralHiccupLift>().Active,"pool-return trial has airborne actor");poolActor.DespawnForStageTransfer();
            var reused=level.EntityManager.SpawnMonster(group.i,player.Position+Vector2.left*2,bp,0,false);yield return null;
            Check(!reused.IsFieldRuntimeSuspended&&!reused.BodyRenderer.forceRenderingOff,"pool reuse never retains airborne hidden state");
            events.CancelAll();yield return null;Check(Object.FindObjectsOfType<OralEventAdditions>().Length==0&&Object.FindObjectsOfType<OralBalloonDelivery>().Length==0,"teardown removes event assets and floating deliveries");
            Check(!Object.FindObjectsOfType<OralHiccupLift>().Any(l=>l.Active),"teardown restores every airborne actor");
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator Record(string name,Func<bool> active,Action step)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);foreach(var old in Directory.GetFiles(dir,"*.png"))File.Delete(old);
            File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");int oldRate=Time.captureFramerate;Time.captureFramerate=20;float start=Time.time,timeout=Time.realtimeSinceStartup+70;int frame=0;
            do{yield return null;step?.Invoke();Frame(name+"/"+frame.ToString("D4"));File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-start).ToString("F4",CultureInfo.InvariantCulture)+"\n");frame++;}while(active()&&Time.realtimeSinceStartup<timeout);
            Time.captureFramerate=oldRate;Check(!active(),"recorded event and squash finish within timeout");
        }
        static void Frame(string name)
        {
            var camera=Camera.main;var player=OralStageRuntime.Instance?.Level.PlayerCharacter;if(camera==null||player==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var flags=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float zoom=camera.orthographicSize;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D image=null;
            try{
                camera.transform.position=new Vector3(player.Position.x,player.Position.y+1.2f,position.z);camera.orthographicSize=shotSize;
                // Coroutine captures run before Unity LateUpdate. Refresh the same real
                // presentation ownership that the normal rendering phase executes.
                foreach(var crush in Object.FindObjectsOfType<JawCrushVisual>())if(crush.enabled)typeof(JawCrushVisual).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(crush,null);
                foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())typeof(PlayerCombatBars).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bars,null);
                camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());
            }finally{
                for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=flags[i];}
                camera.targetTexture=target;camera.transform.position=position;camera.orthographicSize=zoom;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
