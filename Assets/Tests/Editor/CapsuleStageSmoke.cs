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
    public static class CapsuleStageSmoke
    {
        const string Key="CapsuleStageSmoke",Output="D:/Project_GC/work/capsule-proof-v3";
        static bool started;static int checks;static double deadline;
        static string pendingGroup;static int pendingFrame;
        [Serializable]class Entry{public string id;}[Serializable]class Catalog{public Entry[] entries;}
        [Serializable]class Pref{public string key;public bool had;public int value;}[Serializable]class Backup{public Pref[] prefs;}
        static CapsuleStageSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run(){SessionState.SetBool(Key+"NoImages",false);Start();}
        public static void RunChecksOnly(){SessionState.SetBool(Key+"NoImages",true);Start();}
        static void Start()
        {
            Directory.CreateDirectory(Output);Restore();File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            Vampire.Editor.AmphibiousCapsuleInstaller.Install();
            var entries=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text).entries;
            var keys=entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{CapsuleDoctorDialogue.SeenKey}).ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var k in keys)PlayerPrefs.SetInt(k,1);PlayerPrefs.DeleteKey(CapsuleDoctorDialogue.SeenKey);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+900;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Restore(){if(!File.Exists(Output+"/prefs.json"))return;foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs.json")).prefs){if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);}PlayerPrefs.Save();File.Delete(Output+"/prefs.json");}
        static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(Output+"/errors.txt","Deadline\n");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.captureDeltaTime=0;SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();if(started||!EditorApplication.isPlaying||level==null||!level.InitialFieldObjectsReady||level.CurrentLevelTime<.1f)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(n);}
        static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static void Check(bool ok,string text){if(!ok)throw new Exception("[Colon] FAIL "+text);checks++;Debug.Log("[Colon] PASS "+text);File.AppendAllText(Output+"/checks.txt",text+"\n");}
        static void Position(Character p,Vector2 at){p.transform.position=at;var rb=p.GetComponent<Rigidbody2D>();rb.position=at;rb.velocity=Vector2.zero;Physics2D.SyncTransforms();}
        static void Capture(string group,int index){pendingGroup=group;pendingFrame=index;}
        public static void RenderPending(){if(pendingGroup==null)return;var g=pendingGroup;pendingGroup=null;CaptureNow(g,pendingFrame);}
        static void CaptureNow(string group,int index)
        {
            if(group=="stage2")
            {
                var p=Object.FindObjectOfType<LevelManager>().PlayerCharacter;var point=Camera.main.WorldToViewportPoint(p.transform.position);
                Check(point.x>.1f&&point.x<.9f&&point.y>.1f&&point.y<.9f,"camera returns to player after establishing shot");
                Check(!p.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>().forceRenderingOff,"normal character renderer restored");
            }
            if(SessionState.GetBool(Key+"NoImages",false))return;
            string folder=Output+"/"+group;Directory.CreateDirectory(folder);
            var camera=Camera.main;var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();
            var nodes=overlays.SelectMany(c=>c.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();var layers=nodes.Select(n=>n.gameObject.layer).ToArray();
            var oldTarget=camera.targetTexture;var mask=camera.cullingMask;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            var uiGo=new GameObject("Proof overlay camera");var ui=uiGo.AddComponent<Camera>();ui.enabled=false;ui.CopyFrom(camera);ui.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);ui.clearFlags=CameraClearFlags.Depth;ui.cullingMask=1<<31;ui.targetTexture=rt;
            try
            {
                foreach(var n in nodes)n.gameObject.layer=31;
                camera.targetTexture=rt;camera.cullingMask=mask&~(1<<31);
                foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=ui;c.planeDistance=ui.nearClipPlane+.1f;}
                Canvas.ForceUpdateCanvases();camera.Render();ui.Render();RenderTexture.active=rt;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+index.ToString("D4")+".png",pixels.EncodeToPNG());
            }
            finally{for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];}for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];camera.cullingMask=mask;camera.targetTexture=oldTarget;ui.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Object.Destroy(uiGo);Canvas.ForceUpdateCanvases();}

        }
        static IEnumerator Checks(LevelManager level)
        {
            var capture=level.gameObject.AddComponent<StageCaptureProbe>();Check(capture!=null,"late-frame capture probe installed");capture.AfterLateUpdate=RenderPending;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}Time.timeScale=1;RunScore.Exclude("대장 맵 전환 검증");
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;var player=level.PlayerCharacter;Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
            foreach(var ability in Object.FindObjectsOfType<Ability>()){ability.enabled=false;ability.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            var map=FiniteStomachMap.Instance;var originalRect=map.WorldRect;var originalBlueprint=level.CurrentLevelBlueprint;
            var progression=StageProgression.Ensure(level);var originalPlayer=player;var inventory=level.PlayerInventory;var skills=player.Skills;
            var bomb=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Chest/Bomb.prefab")).GetComponent<Collectable>();bomb.gameObject.SetActive(false);
            var bombSlot=inventory.Slots.First(s=>s.IsBomb);bombSlot.AddItem(bomb);int bombCount=bombSlot.Count;
            player.AddDamageMultiplier(.23f);float damage=player.DamageMultiplier;player.AddReviveCount(1);int revives=player.ReviveCount;float health=player.CurrentHealth;
            Vector2 origin=map.UVToWorld(new Vector2(.53f,.47f));Time.captureDeltaTime=1f/15;
            Set(level,"levelTime",1035f);var lake=level.GetComponent<AcidLakeEvent>();yield return new WaitForSeconds(.15f);
            Position(player,origin+Vector2.right*2.4f+Vector2.up*.9f);
            var boss=FoodBossRuntime.Spawn(FoodBossKind.Rolpangi,player,origin);boss.AutoPatterns=false;yield return null;
            boss.TakeDamage(boss.MaxHealth+1);Check(boss.Dead,"actual boss HP zero starts escape and capsule delivery");
            float deadline=Time.time+15;int drop=0;
            while(CapsuleDoctorDialogue.Instance==null&&Time.time<deadline){Capture("01-descent",drop++);yield return null;}
            var dialogue=CapsuleDoctorDialogue.Instance;Check(dialogue!=null&&progression.Capsule!=null&&progression.Capsule.Landed,"doctor opens only after parachute landing");
            Check(progression.Remnant==null&&Object.FindObjectOfType<BossRemnantPickup>()==null,"no wearable remnants spawn");
            Check(progression.Portal!=null&&!progression.TryEnter(player,progression.Portal),"reserved pursuit hole retained");
            Check(dialogue.Message.text==CapsuleDoctorDialogue.Greeting("Ashi"),"exact first-page greeting uses active character name");
            foreach(var pair in new[]{new[]{"Ari","아리"},new[]{"Hyuki","혁이"},new[]{"Shini","신이"}})Check(CapsuleDoctorDialogue.Greeting(pair[0]).StartsWith(pair[1]+"!"),"localized greeting "+pair[0]);
            Check(CapsuleDoctorDialogue.Seen&&PlayerPrefs.GetInt(CapsuleDoctorDialogue.SeenKey)==1,"first-boss tutorial remembered persistently");
            for(int i=0;i<20;i++){Capture("02-dialogue",i);yield return null;}
            dialogue.NextButton.onClick.Invoke();Check(dialogue.Page==1&&dialogue.Message.text=="계속 수고해라.","next button shows exact second line");
            for(int i=20;i<40;i++){Capture("02-dialogue",i);yield return null;}
            dialogue.NextButton.onClick.Invoke();yield return null;yield return null;
            Check(progression.Capsule.Ready&&!progression.Travelling,"dialogue end unlocks capsule interaction");
            Check(CapsuleDoctorDialogue.Show(player)==null,"repeat tutorial suppressed after first display");
            var capsule=progression.Capsule;Vector3 parked=capsule.transform.position;
            capsule.transform.position=map.UVToWorld(new Vector2(.5f,.2f));yield return new WaitForSeconds(.3f);
            Check(capsule!=null&&capsule.gameObject.activeInHierarchy,"acid cleanup preserves rescue capsule");capsule.transform.position=parked;
            Position(player,(Vector2)parked+Vector2.right*1.2f);yield return new WaitForSeconds(.3f);
            InteractionFocus.SelectTouched(capsule);capsule.TryInteract();yield return null;
            Check(progression.CapsuleBoarded&&player.IsPortalDamageProtected,"actual E/touch interaction starts protected boarding");
            int boarding=0,move=0,close=0,loading=0,intro=0;deadline=Time.time+45;bool sawClosed=false,sawPaused=false,sawWet=false,sawDry=false,sawOpenDoor=false;float rotor=capsule.RotorAngle;
            float closeAt=-1,openAt=-1,closeDuration=0,openDuration=0;string prior="";
            while(progression.Travelling&&Time.time<deadline)
            {
                string phase=progression.TransitionPhase;
                if(phase!=prior)
                {
                    if(prior=="iris-close")closeDuration=Time.time-closeAt;
                    if(prior=="iris-open")openDuration=Time.time-openAt;
                    if(phase=="iris-close")closeAt=Time.time;
                    if(phase=="iris-open")openAt=Time.time;
                    prior=phase;
                }
                if(phase=="hatch-open"||phase=="boarding"||phase=="hatch-close"){Capture("03-boarding",boarding++);sawOpenDoor|=capsule.HatchOpen>.95f;}
                else if(phase=="capsule-travel"){Capture("04-amphibious-travel",move++);sawDry|=capsule.Immersion<.05f;sawWet|=capsule.Immersion>.95f;}
                else if(phase=="outlet-open"||phase=="iris-close")Capture("05-exit-iris",close++);
                else if(phase=="loading")Capture("06-loading",loading++);
                else{Capture("07-stage2-opening",intro++);sawPaused|=level.IsRunFlowPaused&&level.CurrentLevelTime==0;sawClosed|=StomachOpening.Find(true)!=null&&StomachOpening.Find(true).Openness<.1f;}
                yield return null;
            }
            Check(sawOpenDoor,"center hatch opens before passenger boards");Check(sawDry&&sawWet,"same vehicle traverses dry floor and immerses tube halfway in acid");
            Check(closeDuration>=1.95f&&closeDuration<2.4f&&openDuration>=1.95f&&openDuration<2.4f,"spiral closes and opens in two seconds each");
            Check(player.IsAlive&&Mathf.Abs(player.CurrentHealth-health)<.01f,"capsule journey blocks acid damage and erosion");
            Check(progression.StageNumber==2&&!progression.Travelling,"colon stage opens and completes entry");
            Check(sawClosed&&sawPaused,"closed inlet and stopped stage clock during opening");
            Check(player==originalPlayer&&inventory==level.PlayerInventory&&skills==player.Skills,"same player inventory and skill objects retained");
            Check(player.DamageMultiplier==damage&&player.ReviveCount==revives,"combat upgrades and revival count retained");
            Check(bombSlot.Count==bombCount&&bomb!=null&&!bomb.gameObject.activeSelf,"stored consumable object and count retained");
            Check(map.IsColon&&map.WorldRect==originalRect&&map.Definition.pixelWorldSize==Resources.Load<StomachMapDefinition>("StomachMap/Stage1").pixelWorldSize,"same world bounds and pixel size");
            Check(level.CurrentLevelBlueprint.monsters.Length==originalBlueprint.monsters.Length+1&&level.CurrentLevelBlueprint.levelTime==originalBlueprint.levelTime,"colon adds a dedicated ordinary and elite pool while preserving duration");
            for(int i=0;i<originalBlueprint.monsters.Length;i++)Check(level.CurrentLevelBlueprint.monsters[i].monstersPrefab==originalBlueprint.monsters[i].monstersPrefab,"same monster prefab "+i);
            Check(level.InitialFieldObjectsReady&&level.GetComponent<FieldObjectLayout>().SpawnedObjects.Count==86,"stage two initial layout completes before play");
            Check(!lake.Active&&!AcidLakeEvent.Contains(player.transform.position),"stage-one acid stops in colon");
            Check(Object.FindObjectOfType<FinalBossSummonInteractable>()!=null,"stage-two boss altar survives or restores after stage-one flood");
            Check(!player.IsPortalTravelling&&!player.IsPortalPoseHeld&&player.GetComponent<Rigidbody2D>().simulated,"input and physics released after one-second settle");
            yield return new WaitForSeconds(.3f);
            Capture("stage2",0);
            // Capture the actual full terrain material without changing the gameplay camera.
            var full=(RenderTexture)map.OverviewTexture;var active=RenderTexture.active;RenderTexture.active=full;var tex=new Texture2D(full.width,full.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,full.width,full.height),0,0);tex.Apply();File.WriteAllBytes(Output+"/map.png",tex.EncodeToPNG());Object.Destroy(tex);RenderTexture.active=active;
            Check(map.AppliesAt(player.transform.position)&&map.NavigationDistance(player.transform.position,out _)>=map.WallWidth,"spawn lands on reachable flat floor");
            var ridge=map.UVToWorld(new Vector2(795f/1536,1-360f/1024));Check(!map.Contains(ridge),"approved central ridge is solid");
            var from=map.UVToWorld(new Vector2(.5f,.74f));var to=map.UVToWorld(new Vector2(.5f,.50f));
            var dash=FiniteStomachMap.ClampDash(from,to);Check((dash-to).sqrMagnitude>1,"dash cannot cross internal ridge");
            var direction=ColonNavigation.Toward(from,to);Check(Mathf.Abs(direction.x)>.1f,"horde steers around ridge rather than through it");
            Check(FiniteStomachMap.UphillMultiplier(.5f)==.5f&&Mathf.Approximately(FiniteStomachMap.UphillMultiplier(1),.1f),"50 to 90 percent uphill slowdown retained");
            level.enabled=true;float clock=level.CurrentLevelTime;yield return new WaitForSeconds(2);Check(level.CurrentLevelTime>clock+1.5f,"stage two clock runs after entry");
            Check(level.NormalMonstersSpawned>0,"stage two normal monsters actually spawn");
            level.enabled=false;Vector2 ramp=Vector2.zero;bool found=false;
            for(int y=5;y<95&&!found;y++)for(int x=5;x<95;x++)
            {var p=map.UVToWorld(new Vector2(x/100f,y/100f));float d=map.NavigationDistance(p,out _);if(d>3.8f&&d<4.2f){ramp=p;found=true;break;}}
            Check(found,"reachable slope sample exists");Position(player,ramp);map.NavigationDistance(ramp,out var inward);
            Check(player.GetComponent<StomachSlopeBody>().MovementMultiplier(-inward)<.5f,"actual player uphill multiplier reduced on colon wall");
            player.Move(-inward);Check(player.TryDash(),"actual dash remains usable on colon slope");yield return new WaitForSeconds(.7f);player.Move(Vector2.zero);Position(player,ramp);
            float beforeSlide=map.NavigationDistance(player.transform.position,out _);yield return new WaitForSeconds(1);
            Check(map.NavigationDistance(player.transform.position,out _)>beforeSlide+.02f,"actual player slides down colon wall after release");
            Position(player,map.ClampToField(map.UVToWorld(new Vector2(.75f,.3f)),10));
            foreach(var key in new[]{"Ashi","Ari","Hyuki","Shini"})Check(BloodClotTravel.Frames(key).Length==(key=="Hyuki"?5:16),"character-specific mini-stage exit art available "+key);
            var final=level.EntityManager.SpawnFinalBoss(level.CurrentLevelBlueprint,(Vector2)player.transform.position+Vector2.right*3,0,FoodBossKind.Maribi).GetComponent<FoodBossRuntime>();
            final.AutoPatterns=false;final.TakeDamage(final.MaxHealth+1);yield return new WaitForSeconds(3);
            Check(progression.Portal!=null&&progression.Capsule==null,"actual stage-two boss leaves final exit without another capsule");
            Check(progression.TryEnter(player,progression.Portal)&&level.IsLevelEnded,"stage two exit finishes the run");
            Time.captureDeltaTime=0;SessionState.SetBool(Key+"Done",true);
        }
    }
}
