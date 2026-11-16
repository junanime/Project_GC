#if UNITY_EDITOR
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
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad] public static class GameplayGuideProof
    {
        const string Key="GameplayGuideProof",Root="Library/GameplayGuideProof";
        static bool started;static double deadline;static int checks;
        static bool CaptureMode=>SessionState.GetBool(Key+"Capture",false);
        static GameplayGuideProof(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Capture(){Start(true);}
        public static void Run(){Start(false);}
        static void Start(bool capture)
        {
            Directory.CreateDirectory(Root);SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Capture",capture);
            SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            SessionState.SetString(Key+"Data",Path.GetFullPath(Root+"/data-"+Guid.NewGuid().ToString("N")));
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");Attach();EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate()
        {
            if(!SessionState.GetBool(Key,false))return;
            GStarDemo.TestOverride=true;GStarDemo.TestDataDirectory=SessionState.GetString(Key+"Data","");
            GStarSession.TestDirectory=GStarDemo.TestDataDirectory;GStarSession.ReloadForTests();
            GStarLeaderboard.TestDirectory=GStarDemo.TestDataDirectory;GStarLeaderboard.ReloadForTests();
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1500;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(Root+"/errors.txt",message+"\n"+stack+"\n");}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){Time.captureFramerate=0;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool fail=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("GAMEPLAY_GUIDES_FINISHED failed="+fail+" checks="+checks);EditorApplication.Exit(fail?1:0);return;
            }
            if(CaptureMode&&TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {started=true;var host=new GameObject("Gameplay guide QA").AddComponent<StageEntryLoadingTestHost>();Object.DontDestroyOnLoad(host);host.StartCoroutine(Sequence());}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("GUIDE_FAIL "+message);checks++;Debug.Log("GUIDE_PASS "+message);}
        static void Set(object o,string n,object v)=>ColonContentSmoke.Set(o,n,v);
        static object Call(object o,string n,params object[] args)
        {for(var t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args);}throw new Exception(n);}
        static void Silence(LevelManager l)
        {
            l.enabled=false;foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn||b is ColonSpecialDirector){b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in Object.FindObjectsOfType<Monster>())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            Set(l.PlayerCharacter,"isInvincible",true);l.PlayerCharacter.CollectableCollider.enabled=false;
        }
        static IEnumerator Sequence()
        {
            Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint,stage:1),"stage-one entry");
            while(StageEntryLoading.IsLoading)yield return null;
            var l=Object.FindObjectOfType<LevelManager>();Silence(l);var p=l.PlayerCharacter;Time.timeScale=1;
            if(CaptureMode)
            {
                Time.captureFramerate=15;var center=p.Position;
                var clot=Object.FindObjectsOfType<BloodClotMonster>().FirstOrDefault();
                Check(clot!=null,"real blood clot exists");clot.transform.position=center+Vector2.right*1.5f;
                var rb=clot.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=clot.transform.position;
                bool killed=false,begun=false;BloodClotOvercharge charge=null;
                yield return Clip("blood-clot",8,center,4.8f,t=>
                {
                    if(t>1&&!killed){killed=true;clot.TakeDamage(clot.HP+9999);}
                    charge=Object.FindObjectOfType<BloodClotOvercharge>();
                    if(t>2.5f&&charge!=null&&!begun){ColonContentSmoke.Position(p,charge.transform.position);begun=charge.Begin();}
                    if(t>3&&charge!=null&&charge.Started&&!charge.Enhanced)charge.Advance(charge.Target/(15f*2.5f));
                });Check(killed&&begun&&charge!=null&&charge.Enhanced,"clot destruction, portal and overcharge recorded");
                if(charge!=null)Object.Destroy(charge.gameObject);
                var vending=Object.FindObjectOfType<EliteMonsterSummonInteractable>();Check(vending!=null,"real elite vending exists");
                center=vending.transform.position;ColonContentSmoke.Position(p,center+Vector2.down);
                bool activated=false;yield return Clip("elite-vending",6,center,5,t=>{if(t>1&&!activated){activated=true;Call(vending,"ExecuteInteraction",p);}});
                Check(l.EntityManager.LivingMonsters.Any(m=>m.Blueprint is EliteMonsterBlueprint),"vending summons real elites");
                Time.captureFramerate=0;Check(StageProgression.Ensure(l).BeginDirectOral(true),"oral field entry");
                while(OralStageRuntime.Instance==null||StageProgression.Ensure(l).Travelling)yield return null;
                Silence(l);p=l.PlayerCharacter;var events=OralFieldEvents.Instance;events.AutoSchedule=false;
                foreach(var c in events.Cavities){c.Healed=true;c.Active=false;}
                center=OralMapGeometry.TongueCenter(FiniteStomachMap.Instance);OralStageRuntime.Instance.TeleportPlayer(center,OralZone.Tongue);
                Check(events.ForceEvent(OralEventKind.Spicy),"actual spicy event started");
                Check(events.Tastes.SpiceCenters.Count==10,"ten randomized fire circles");
                yield return new WaitForSeconds(3);Time.captureFramerate=15;
                yield return Clip("spicy",8,center,6.8f,t=>p.Move(t<2?Vector2.right:Vector2.zero));
                p.Move(Vector2.zero);Time.captureFramerate=0;
            }
            else
            {
                var catalogTests=new TutorialGuideTests();catalogTests.EveryFirstDiscoveryHasItsOwnPlayableClip();catalogTests.MonstersHaveDistinctStableDiscoveryIds();Check(true,"all atlas dimensions, clip IDs and metadata valid");
                var guide=TutorialGuide.Instance;guide.Close();guide.Initialize(l);guide.enabled=true;
                var entries=guide.Entries.ToArray();Check(entries.Length>=80,"expanded catalog loaded");
                Directory.CreateDirectory(Root+"/panels");
                foreach(var e in entries.Where(e=>e.gameplay))
                {
                    Check(guide.Review(e.id),"review opens "+e.id);Check(Time.timeScale==0,"safe pause "+e.id);
                    yield return new WaitForSecondsRealtime(.1f);int frame=guide.CurrentFrame;
                    yield return new WaitForSecondsRealtime(.19f);Check(guide.CurrentFrame!=frame,"clip animates while paused "+e.id);
                    yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Root+"/panels/"+e.effect+".png",shot.EncodeToPNG());Object.Destroy(shot);
                    var text=guide.GetComponentsInChildren<Text>().First(x=>x.text==e.title);Check(text.cachedTextGenerator.characterCountVisible>0,"title rendered "+e.id);
                    var preview=guide.GetComponentInChildren<RawImage>();Check(Mathf.Abs(preview.rectTransform.rect.width/preview.rectTransform.rect.height-16f/9f)<.01f,"gameplay aspect preserved "+e.id);
                    guide.Close();Check(Time.timeScale==1,"resume "+e.id);yield return null;
                }
                foreach(var id in new[]{"event/oral-spicy","monster/colon-S07","mechanic/blood-clot"})
                {
                    guide.Review(id);string dir=Root+"/ui-motion/"+id.Replace('/','-');Directory.CreateDirectory(dir);
                    for(int i=0;i<40;i++){yield return new WaitForSecondsRealtime(.1f);yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(dir+"/"+i.ToString("0000")+".png",shot.EncodeToPNG());Object.Destroy(shot);}
                    guide.Close();
                }
                guide.Review("mechanic/blood-clot");yield return new WaitForSecondsRealtime(.3f);
                var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Space));InputSystem.Update();
                Call(guide,"Update");Check(!TutorialGuide.IsOpen,"space closes guide");Check(!GameInput.Pressed(GameKeyAction.Active)&&!p.TryDash(),"space close cannot activate skill or dash");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
                guide.Review("mechanic/blood-clot");guide.GetComponentsInChildren<Button>().First(b=>b.name=="Button 확인 · SPACE").onClick.Invoke();Check(!TutorialGuide.IsOpen,"confirm click closes guide");
                Check(!GStarSession.HasSeenGuide("event/colon-expulsion"),"review does not consume discovery");
                Check(GStarSession.MarkGuideSeen("event/colon-expulsion"),"visitor discovery saved");
                GStarSession.ReloadForTests();Check(GStarSession.HasSeenGuide("event/colon-expulsion"),"discovery survives restart");
                GStarSession.CompleteAttempt(false);Check(GStarSession.ResetVisitor(),"next visitor reset");Check(!GStarSession.HasSeenGuide("event/colon-expulsion"),"next visitor receives guide again");
                guide.Initialize(l);guide.enabled=false;Set(guide,"nextFieldScan",Time.unscaledTime+999);Set(guide,"nextDiscovery",0f);
                var probe=new GameObject("First appearance probe");probe.transform.position=Vector3.one*10000;TutorialDiscoveryTrigger.Bind(probe,"monster/sniper");
                var trigger=probe.GetComponent<TutorialDiscoveryTrigger>();Call(trigger,"Update");Call(guide,"Update");Check(!TutorialGuide.IsOpen,"off-screen enemy does not interrupt play");
                probe.transform.position=p.transform.position;Set(trigger,"next",0f);Call(trigger,"Update");Call(guide,"Update");Check(TutorialGuide.IsOpen&&guide.CurrentId=="monster/sniper","visible enemy opens first-discovery guide");guide.Close();Object.Destroy(probe);
                // Queue routing is exercised in the real stage-two and stage-zero runtimes.
                guide.enabled=false;yield return ColonContentSmoke.Enter(l);Silence(l);
                var roster=ColonMonsterRoster.Active;
                foreach(var e in roster.Specials)Check(guide.Entries.Any(x=>x.id==TutorialGuide.ResolveMonsterId(e.blueprint)),"colon special routing "+e.id);
                var ce=ColonFieldEvents.Instance;ce.AutoSchedule=false;
                foreach(ColonEventKind kind in new[]{ColonEventKind.Expulsion,ColonEventKind.Absorption,ColonEventKind.Residue,ColonEventKind.Nutrients})
                {Check(ce.Begin(kind),"event hook "+kind);ce.Cancel();}
                guide.Initialize(l);guide.enabled=true;TutorialGuide.QueueStageEvent("colon-expulsion");
                Set(guide,"nextDiscovery",0f);yield return null;yield return null;Check(guide.CurrentId=="event/colon-expulsion"&&TutorialGuide.IsOpen,"first event opens actual guide");guide.Close();
                Check(GStarSession.HasSeenGuide("event/colon-expulsion"),"first appearance receipt");
                guide.Initialize(l);TutorialGuide.QueueStageEvent("colon-expulsion");guide.enabled=false;Check(GStarSession.HasSeenGuide("event/colon-expulsion"),"repeat kept suppressed");
            }
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator Clip(string name,float seconds,Vector2 center,float zoom,Action<float> step)
        {
            string dir=Root+"/captures/"+name;Directory.CreateDirectory(dir);
            for(int i=0;i<Mathf.CeilToInt(seconds*15);i++)
            {
                step?.Invoke(i/15f);yield return new WaitForEndOfFrame();
                var camera=Camera.main;var pos=camera.transform.position;float size=camera.orthographicSize;var target=camera.targetTexture;var active=RenderTexture.active;
                var rt=RenderTexture.GetTemporary(768,432,24);var image=new Texture2D(768,432,TextureFormat.RGB24,false);
                try{camera.transform.position=new Vector3(center.x,center.y,pos.z);camera.orthographicSize=zoom;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,768,432),0,0);image.Apply();File.WriteAllBytes(dir+"/"+i.ToString("0000")+".png",image.EncodeToPNG());}
                finally{camera.transform.position=pos;camera.orthographicSize=size;camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
            }
        }
    }
}
#endif
