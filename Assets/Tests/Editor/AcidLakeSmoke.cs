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
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class AcidLakeSmoke
    {
        const string Key="AcidLakeSmoke", Output="Library/AcidLakeProof";
        static bool started; static double deadline;
        [Serializable] class Entry{public string id;}
        [Serializable] class Catalog{public Entry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}
        [Serializable] class Backup{public Saved[] values;}
        static AcidLakeSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Vampire.Editor.AcidLakeArtImporter.Prepare();
            Directory.CreateDirectory(Output);RestorePrefs();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins",CapsuleDoctorDialogue.SeenKey}).Distinct();
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var view=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(view).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+300;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string text,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                RestorePrefs();SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");Debug.Log("ACID_LAKE_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Acid and initial loading QA").AddComponent<StageEntryLoadingTestHost>();
                Object.DontDestroyOnLoad(host);host.StartCoroutine(LoadThenCheck());
            }
        }
        static IEnumerator LoadThenCheck()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"real main-menu loading starts");
            while(StageEntryLoading.IsLoading)
            {
                var loadingLevel=Object.FindObjectOfType<LevelManager>();
                if(loadingLevel!=null)Check(loadingLevel.CurrentLevelTime==0,"no run time elapses behind loading overlay");
                yield return null;
            }
            var level=Object.FindObjectOfType<LevelManager>();var layout=level.GetComponent<FieldObjectLayout>();
            Check(layout!=null&&layout.Ready&&layout.SpawnedObjects.Count==86,"loading ends only after all 86 live objects exist");
            Check(Object.FindObjectsOfType<Chest>().Length==24&&Object.FindObjectsOfType<MerchantNPC>().Length==12&&Object.FindObjectsOfType<BloodClotMonster>().Length==14&&
                Object.FindObjectsOfType<EliteMonsterSummonInteractable>().Length==8&&Object.FindObjectsOfType<CurrencyJar>().Count(j=>!j.Silver)==20&&Object.FindObjectsOfType<CurrencyJar>().Count(j=>j.Silver)==8,"actual initial counts 24/12/14/8/20/8");
            Check(Object.FindObjectsOfType<Chest>().All(c=>c.CanInteract&&c.transform.localScale==Vector3.one),"all starting chests settled and usable before play");
            var markers=layout.SpawnedObjects.Select(o=>o.GetComponent<MapMarker>()).ToArray();
            Check(markers.All(m=>m!=null&&!m.IsDiscovered&&!m.AlwaysVisible&&m.HideUntilDiscovered&&m.RequireNearbyDiscovery),"all initial object markers hidden until found");
            var map=ExplorationMapSystem.Instance;var player=level.PlayerCharacter;var start=player.transform.position;
            Position(player,markers[0].transform.position+Vector3.right*2);Call(map,"UpdateMarkerDiscovery");
            Check(markers[0].IsDiscovered&&markers.Any(m=>!m.IsDiscovered),"nearby object discovered without revealing all objects");
            Position(player,start);Call(map,"UpdateMarkerDiscovery");Check(markers[0].IsDiscovered,"discovery persists after moving away");
            yield return Checks(level);
        }
        static void RestorePrefs()
        {
            string p=Output+"/prefs-backup.json";if(!File.Exists(p))return;
            foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(p)).values)
                if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);
            PlayerPrefs.Save();File.Move(p,Output+"/prefs-restored-"+DateTime.UtcNow.Ticks+".json");
        }
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception(n);}
        static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,args);
        static void Check(bool ok,string message){if(!ok)throw new Exception("ACID_LAKE_FAIL "+message);Debug.Log("ACID_LAKE_PASS "+message);}
        static void Position(Character p,Vector2 q){p.transform.position=q;p.GetComponent<Rigidbody2D>().position=q;p.GetComponent<Rigidbody2D>().velocity=Vector2.zero;}
        static void Shot(string name)
        {var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.Destroy(t);}
        static void Overview(AcidLakeEvent lake,string name)
        {
            var go=new GameObject("QA overview");var cam=go.AddComponent<Camera>();cam.orthographic=true;
            var r=lake.Map.WorldRect;cam.orthographicSize=r.height*.52f;cam.transform.position=new Vector3(r.center.x,r.center.y,-20);
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.04f,.08f);
            var rt=RenderTexture.GetTemporary(1200,800,24);var prev=RenderTexture.active;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var t=new Texture2D(1200,800,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,800),0,0);t.Apply();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());
            RenderTexture.active=prev;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(t);Object.Destroy(go);
        }
        static void OpeningShot(StomachOpening opening,string name)
        {
            var go=new GameObject("QA opening camera");var cam=go.AddComponent<Camera>();cam.orthographic=true;
            cam.orthographicSize=opening.Radii.y*1.4f;cam.transform.position=new Vector3(opening.Center.x,opening.Center.y,-20);
            var rt=RenderTexture.GetTemporary(900,900,24);var prev=RenderTexture.active;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var t=new Texture2D(900,900,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,900,900),0,0);t.Apply();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());
            RenderTexture.active=prev;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(t);Object.Destroy(go);
        }
        static Color MapSample(FiniteStomachMap map,Vector2 uv)
        {
            var texture=(RenderTexture)map.OverviewTexture;var prev=RenderTexture.active;RenderTexture.active=texture;
            var t=new Texture2D(1,1,TextureFormat.RGB24,false);t.ReadPixels(new Rect((int)(uv.x*texture.width),(int)(uv.y*texture.height),1,1),0,0);t.Apply();
            var color=t.GetPixel(0,0);Object.Destroy(t);RenderTexture.active=prev;return color;
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            Application.runInBackground=true;Time.timeScale=1;Time.captureDeltaTime=1f/60;
            TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;
            var player=level.PlayerCharacter;var lake=AcidLakeEvent.Instance;var map=FiniteStomachMap.Instance;
            Check(lake!=null,"installed on actual Level 1");
            for(int i=0;i<100&&!level.GetComponent<FieldObjectLayout>().Ready;i++)yield return null;
            foreach(var behaviour in Object.FindObjectsOfType<MonoBehaviour>())
                if(behaviour is StageEventDirector||behaviour is TimedSpecialMonsterSpawner||behaviour is AcidToadSpawn||behaviour is NPCSpawner){behaviour.enabled=false;behaviour.StopAllCoroutines();}
            level.enabled=false;
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            Set(player,"isInvincible",true);
            var openings=Object.FindObjectsOfType<StomachOpening>().OrderByDescending(o=>o.Upper).ToArray();
            Check(openings.Length==2&&openings.All(o=>!o.IsOpen),"two anatomical covers start closed");
            Check(Mathf.Abs(StomachOpening.Inlet.z/.045f-2f/3)<.0001f&&Mathf.Abs(StomachOpening.Outlet.w/.075f-2f/3)<.0001f,"both opening dimensions reduced to two thirds");
            foreach(var opening in openings)
            {
                string prefix=opening.Upper?"05-inlet":"06-outlet";
                var front=opening.FrontLip+opening.Inward*.8f;
                Position(player,front);player.Move(Vector2.zero);
                for(int i=0;i<12;i++)yield return null;
                Check(Vector2.Distance(player.transform.position,front)<.35f,"opening front lip is reachable: "+prefix);
                Check(InteractionFocus.IsFocused(opening),"actual interaction focus at front lip: "+prefix);
                OpeningShot(opening,prefix+"-closed");
                var blocked=FiniteStomachMap.ClampDash(front,opening.Center-opening.Inward*opening.Radii.y,.25f);
                Check(Vector2.Dot(blocked-opening.FrontLip,opening.Inward)>=.20f,"dash stops on the field side of the opening: "+prefix);
                for(int n=-16;n<=16;n++)
                {
                    var candidate=opening.Center+Vector2.right*(n/8f*opening.Radii.x);
                    var safe=map.ClampToField(candidate,map.ClimbLimit+.25f);
                    Check(map.NavigationDistance(safe,out _,true)>=map.ClimbLimit+.24f,"wall and opening joined constraint "+prefix+" "+n);
                }
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(UnityEngine.InputSystem.Key.E));yield return null;
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;
                Check(opening.IsOpen,"E triggers opening: "+prefix);
                for(int i=0;i<32;i++)yield return null;OpeningShot(opening,prefix+"-opening");
                for(int i=0;i<50;i++)yield return null;OpeningShot(opening,prefix+"-open");
                Check(opening.Openness==1,"opening finishes and remains open: "+prefix);
                Check(Vector2.Dot(FiniteStomachMap.ClampDash(front,opening.Center,.25f)-opening.FrontLip,opening.Inward)>=.20f,"open cover cannot be entered: "+prefix);
            }
            Position(player,map.UVToWorld(map.Definition.startUV));
            var liveTexture=map.OverviewTexture;
            Color beforeFlood=MapSample(map,new Vector2(.5f,.3f));
            Check(AcidLakeEvent.ProgressAt(929)==0&&AcidLakeEvent.ProgressAt(1035)==.5f&&AcidLakeEvent.ProgressAt(1140)==1&&AcidLakeEvent.ProgressAt(2000)==1,"15:30 to 19:00 linear progression and permanent cap");
            Set(level,"levelTime",909f);yield return null;Check(!lake.Raining&&!lake.Flooding,"no early warning");
            Set(level,"levelTime",910f);yield return null;Check(lake.Raining&&!lake.Flooding,"15:10 warning and cosmetic rain");
            for(int i=0;i<35;i++)yield return null;Shot("01-warning");
            Set(level,"levelTime",929f);yield return null;Check(!lake.Flooding,"no flood before 15:30");
            var keyboard=Keyboard.current;Check(keyboard!=null,"keyboard available");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Equals));yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Check(lake.Flooding&&level.CurrentLevelTime==929,"equals starts flood without advancing other schedules");
            Set(lake,"debugOffset",0f);Set(level,"levelTime",1035f);yield return null;
            var wet=map.UVToWorld(new Vector2(.5f,.3f));var dry=map.UVToWorld(new Vector2(.24f,.78f));
            Check(lake.IsSubmerged(wet)&&!lake.IsSubmerged(dry),"shared wet/dry world geometry");
            Position(player,dry);Overview(lake,"02-mid-flood");
            Set(map,"nextOverview",0f);
            Color floodedMap=MapSample(map,new Vector2(.5f,.3f));
            Check(map.OverviewTexture==liveTexture&&floodedMap.g>floodedMap.r*.8f&&floodedMap.b<beforeFlood.b*.7f,"persistent TAB texture updates to actual acid coverage");
            ApothecaryUI.Instance.OpenRunBook();yield return null;
            var bookMap=Object.FindObjectsOfType<UnityEngine.UI.RawImage>().First(i=>i.name=="Live exploration map");
            Check(bookMap.texture==liveTexture,"actual TAB panel is bound to the live water texture");
            Shot("07-tab-mid-flood");ApothecaryUI.Instance.CloseRunBook();yield return null;
            Position(player,new Vector2(dry.x,lake.SurfaceY+1.5f));
            for(int i=0;i<45;i++)yield return null;
            Shot("02b-shoreline-gameplay");Position(player,dry);
            var chest=level.EntityManager.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint,wet);
            var merchant=Object.FindObjectsOfType<MerchantNPC>().First();merchant.transform.position=wet;
            var clot=Object.FindObjectsOfType<BloodClotMonster>().First();clot.transform.position=wet;
            var jar=Object.FindObjectsOfType<CurrencyJar>().First();jar.transform.position=wet;
            var coin=level.EntityManager.SpawnCoin(wet,CoinType.Gold5,false);
            var gem=level.EntityManager.SpawnExpGem(wet,GemType.White1,false);
            var progression=level.GetComponent<StageProgression>();
            Check(progression.FinalBossDefeated(wet,10),"boss death creates exit while flood continues");
            var portal=progression.Portal;Check(portal!=null,"boss exit exists before water sweep");
            // Boss defeat now delivers the capsule before ordinary field spawning
            // resumes. Complete that real presentation before testing new spawns.
            float deliveryLimit=Time.realtimeSinceStartup+15;
            while(progression.Travelling&&Time.realtimeSinceStartup<deliveryLimit)
            {
                if(CapsuleDoctorDialogue.Instance!=null&&!CapsuleDoctorDialogue.Instance.Finished)CapsuleDoctorDialogue.Instance.Advance();
                yield return null;
            }
            Check(!progression.Travelling&&!player.IsPortalTravelling,"capsule delivery releases field spawning");
            var monster=level.EntityManager.SpawnMonsterRandomPosition(0,level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0]);
            Check(monster!=null,"field spawn succeeds after capsule landing");
            monster.transform.position=wet;float hp=player.CurrentHealth;
            Call(lake,"RemoveSubmergedObjects");yield return null;
            Check(!chest.gameObject.activeSelf&&merchant==null&&jar==null&&!clot.gameObject.activeSelf,"chest merchant jar and blood clot removed without rewards");
            Check(!coin.gameObject.activeSelf&&!gem.gameObject.activeSelf&&portal==null,"pickups and boss escape portal removed");
            Check(!monster.gameObject.activeSelf&&monster.HP==0&&!monster.WasKilledByPlayer,"environmental instant monster death without player credit");
            Check(Object.FindObjectsOfType<Chest>().All(c=>!lake.IsSubmerged(c.transform.position)),"boss reward chests submerged and removed");
            Check(player.CurrentHealth==hp,"cosmetic rain does no damage on dry land");
            // Even a boss already in water survives the environment sweep.
            var boss=level.EntityManager.SpawnFinalBoss(level.CurrentLevelBlueprint,wet);
            Check(boss!=null,"actual final boss created");boss.transform.position=wet;
            Call(lake,"RemoveSubmergedObjects");yield return null;Check(boss!=null&&boss.activeInHierarchy,"final boss immune to acid");
            for(int i=0;i<40;i++)
            {
                var m=level.EntityManager.SpawnMonster(0,wet,level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0]);
                Check(m!=null&&!lake.IsSubmerged(m.transform.position),"wet spawn relocated to dry ground "+i);m.DespawnForStageTransfer();
            }
            var roomMonster=level.EntityManager.SpawnMonster(0,new Vector2(500,500),level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0],0,true);
            Check(roomMonster!=null&&Vector2.Distance(roomMonster.transform.position,new Vector2(500,500))<1,"mini-stage spawn untouched");roomMonster.DespawnForStageTransfer();
            float time=lake.EventTime;level.SetRunFlowPaused(true);Set(level,"levelTime",1100f);yield return null;
            Check(lake.EventTime==time,"mini-stage/run pause freezes flood clock");level.SetRunFlowPaused(false);
            Set(level,"levelTime",1140f);yield return null;float full=lake.SurfaceY;
            Set(level,"levelTime",1300f);yield return null;Check(lake.SurfaceY==full&&!lake.IsSubmerged(dry),"final safe zone stays dry after 19:00");
            Overview(lake,"03-final-safe-zone");
            ApothecaryUI.Instance.OpenRunBook();yield return null;Set(map,"nextOverview",0f);var finalTexture=map.OverviewTexture;
            Shot("08-tab-final-flood");ApothecaryUI.Instance.CloseRunBook();yield return null;
            lake.enabled=false;
            // Capsule landing grants the same arrival grace as blood-clot travel.
            // Test ordinary acid damage only after that real protection expires.
            yield return new WaitForSeconds(BloodClotTravel.ExitInvincibilityDuration+.1f);
            Position(player,wet);Set(player,"isInvincible",false);Set(player,"currentHealth",player.MaxHealth);
            player.TakeAcidLakeDamage(1,false);Check(Mathf.Abs(player.CurrentHealth-player.MaxHealth*.75f)<.01f,"25 percent MAX HP per second, no armor dilution");
            Set(player,"isInvincible",true);hp=player.CurrentHealth;player.TakeAcidLakeDamage(1,false);Check(player.CurrentHealth==hp,"temporary protection blocks HP loss");
            player.AddReviveCount(1);player.TakeAcidLakeDamage(1,true);
            Check(player.IsAlive&&player.CurrentHealth>0&&!lake.IsSubmerged(player.transform.position),"erosion bypasses protection and revival moves to dry land");
            Check(player.transform.position.y>=lake.SurfaceY+1.4f,"revival has margin from rising edge");
            Position(player,new Vector2(dry.x,full-.4f));
            Set(player,"isInvincible",true);Set(lake,"<ErosionSeconds>k__BackingField",0f);lake.enabled=true;
            for(int i=0;i<30;i++)yield return null;
            Check(lake.ErosionSeconds>.3f,"erosion advances during invulnerability");
            Check(player.TryDash(),"dash remains usable inside acid");
            yield return null;player.RelocateAfterAcidRevival(dry);yield return null;
            Check(lake.ErosionSeconds==0,"dry ground clears erosion");
            // Start rising again for result-panel continuation proof.
            Set(level,"levelTime",1035f);Set(lake,"<EventTime>k__BackingField",1035f);
            Position(player,wet);Set(player,"reviveCount",0);Set(player,"isInvincible",true);
            lake.enabled=false;float deathHeight=lake.SurfaceY;player.TakeAcidLakeDamage(1,true);
            Check(!player.IsAlive&&level.IsLevelEnded,"acid death opens game-over immediately");
            Check(player.GetComponent<CharacterExitSequence>()==null||!player.IsExiting,"acid death bypasses character exit motion");
            lake.enabled=true;
            float elapsed=0;while(elapsed<1){elapsed+=Time.unscaledDeltaTime;yield return null;}
            Check(lake.SurfaceY>deathHeight&&Time.timeScale==0,"water keeps rising behind paused result panel");
            var resultBackdrop=(UnityEngine.UI.Image)Field(ApothecaryUI.Instance,"fullScreenBackdrop").GetValue(ApothecaryUI.Instance);
            Check(resultBackdrop.color.a<.5f,"result backdrop exposes the live flooded world");
            Shot("04-acid-game-over");
            var retry=Object.FindObjectsOfType<UnityEngine.UI.Button>().First(b=>b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="다시하기"));
            retry.onClick.Invoke();yield return null;
            Check(StageEntryLoading.IsLoading,"result retry uses the same initial-field loading gate");
            while(StageEntryLoading.IsLoading)yield return null;
            var retryLevel=Object.FindObjectOfType<LevelManager>();
            Check(retryLevel.InitialFieldObjectsReady&&retryLevel.GetComponent<FieldObjectLayout>().SpawnedObjects.Count==86,"retry also finishes all 86 objects before play");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
