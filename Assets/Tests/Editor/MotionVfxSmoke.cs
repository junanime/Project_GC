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
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    // Real Level1 verification of the approved production effects, including pool reuse.
    [InitializeOnLoad]
    public static class MotionVfxSmoke
    {
        const string Key="MotionVfxSmoke";
        const string Output="Library/MotionVfxQA";
        static double deadline; static bool started;
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }

        static MotionVfxSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output+"/frames"); Restore();
            foreach(var name in new[]{"result.txt","checks.txt","errors.txt","capture-log.txt"})
                if(File.Exists(Output+"/"+name))File.Delete(Output+"/"+name);
            Vampire.Editor.MotionVfxImporter.Import();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){deadline=EditorApplication.timeSinceStartup+1200;started=false;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string t,string s,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",t+"\n"+s+"\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null){started=true;var go=new GameObject("VFX concept capture host");Object.DontDestroyOnLoad(go);go.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Preview());}
        }
        static void Restore(){string path=Output+"/prefs-backup.json";if(!File.Exists(path))return;foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).values){if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}PlayerPrefs.Save();File.Move(path,Output+"/prefs-restored-"+DateTime.UtcNow.Ticks+".json");}
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new Exception(n);}
        static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static void Check(bool ok, string message)
        {
            if(!ok)throw new Exception("MOTION_VFX_FAIL "+message);
            File.AppendAllText(Output+"/checks.txt",message+"\n");
        }
        static SpriteRenderer[] Effects()=>Object.FindObjectOfType<MotionVfx>()?.GetComponentsInChildren<SpriteRenderer>()??new SpriteRenderer[0];
        static IEnumerator Preview()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");
            string[] names={"Main","Ari","Hyuki","Shini"};
            for(int c=0;c<4;c++)
            {
                CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/"+names[c]+" Character Blueprint.asset");
                Time.captureDeltaTime=0;Time.timeScale=1;
                if(!StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint))throw new Exception("Could not start preview scene");
                while(StageEntryLoading.IsLoading)yield return null;
                var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;
                RunScore.Exclude("VFX 디자인 미리보기");
                if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
                foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
                level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();
                foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
                foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
                Set(player,"isInvincible",true);player.CollectableCollider.enabled=false;
                var dust=(ParticleSystem)Field(player,"dustParticles").GetValue(player);
                Check(dust==null||!dust.gameObject.activeInHierarchy,"Legacy dust disabled: "+names[c]);
                Check(player.GetComponent<CharacterMotionTrail>()!=null,"Character trail attached: "+names[c]);
                Check(MotionVfx.CharacterSprite(player.Blueprint)?.name==OctoberArt.CharacterKey(player.Blueprint),"Correct character motif: "+names[c]);
                var actor=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
                var needle=Object.FindObjectOfType<SyringeDartAbility>();Set(needle,"homingEnabled",false);
                var origin=player.transform.position;var rb=player.GetComponent<Rigidbody2D>();
                Time.captureDeltaTime=1f/60;Time.timeScale=1;
                foreach(var shot in Object.FindObjectsOfType<SyringeProjectile>())shot.gameObject.SetActive(false);
                player.Move(Vector2.zero);player.transform.position=origin;rb.position=origin;rb.velocity=Vector2.zero;
                for(int f=0;f<20;f++)yield return null;
                float start=Time.time,nextShot=.12f;int seq=0,endCount=0;bool still=false,trailSeen=false;
                var tracked=new Dictionary<SyringeProjectile,bool>();
                for(int f=0;f<270;f++)
                {
                    float elapsed=Time.time-start;player.Move(elapsed<2.85f?Vector2.right:Vector2.zero);
                    if(elapsed>=nextShot&&elapsed<2.8f)
                    {
                        nextShot+=.8f;Call(needle,"LaunchAimedSyringe",Vector2.right,false,1f,0);
                        foreach(var shot in Object.FindObjectsOfType<SyringeProjectile>().Where(x=>x.IsFlying))tracked[shot]=true;
                    }
                    foreach(var shot in tracked.Keys.ToArray())if(tracked[shot]&&(shot==null||!shot.IsFlying))
                    {
                        tracked[shot]=false;
                        if(shot!=null)
                        {
                            endCount++;
                            Check(Effects().Any(r=>r.enabled&&r.sprite.name=="NeedleEnd"),"Real projectile end emitted approved glint: "+names[c]+" #"+endCount);
                            var ps=(ParticleSystem)Field(shot,"destructionParticleSystem").GetValue(shot);
                            Check(ps==null||!ps.isPlaying,"Legacy needle particle not played");
                        }
                    }
                    trailSeen|=Effects().Any(r=>r.enabled&&r.sprite.name==OctoberArt.CharacterKey(player.Blueprint));
                    yield return null;
                    if(f%5==0)Capture("frames/"+c+"-"+(seq++).ToString("D3"));
                    if(!still&&endCount>=2&&Effects().Any(r=>r.enabled&&r.sprite.name=="NeedleEnd"&&r.bounds.size.x>.30f))
                    {Capture("approved-"+names[c]);still=true;}
                }
                Check(trailSeen,"Moving emits themed trail: "+names[c]);
                Check(endCount>=3,"Repeated projectile pooling/range end: "+names[c]);
                Check(!Effects().Any(r=>r.enabled),"All effects expire after movement and shooting stop: "+names[c]);
                Check(Mathf.Abs(MotionVfx.NeedleSize-.32f)<.0001f,"Glint size exactly two thirds of .48 preview");
                // Exercise reverse and vertical travel, pause, and pool pressure.
                foreach(var heading in new[]{Vector2.left,Vector2.up,Vector2.down})
                {
                    player.Move(heading);for(int f=0;f<20;f++)yield return null;
                    Check(Effects().Any(r=>r.enabled&&r.sprite.name==OctoberArt.CharacterKey(player.Blueprint)),"Directional trail "+heading+" "+names[c]);
                }
                player.Move(Vector2.zero);
                var live=Effects().First(r=>r.enabled);var before=live.color;var at=live.transform.position;
                Time.captureDeltaTime=0;Time.timeScale=0;
                yield return new WaitForSecondsRealtime(.15f);
                Check(live.color==before&&live.transform.position==at,"Pause preserves VFX timeline");
                Time.timeScale=1;Time.captureDeltaTime=1f/60;
                for(int i=0;i<200;i++)MotionVfx.PlayNeedleEnd(origin,actor);
                Check(Effects().Length<=128,"Visual pool stays bounded under burst");
                for(int f=0;f<65;f++)yield return null;
                Check(!Effects().Any(r=>r.enabled),"Burst effects expire");
                File.AppendAllText(Output+"/capture-log.txt",names[c]+" range-end events "+endCount+" screenshot "+still+"\n");
            }
            SessionState.SetBool(Key+"Done",true);
        }
        static void Capture(string name)
        {
            var camera=Camera.main;var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var over=overlays.Select(c=>c.overrideSorting).ToArray();
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D pixels=null;
            try{camera.targetTexture=rt;foreach(var canvas in overlays){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;canvas.overrideSorting=true;canvas.sortingOrder=Mathf.Clamp(32100+canvas.sortingOrder,32100,32760);}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
            finally{for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=over[i];}camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Canvas.ForceUpdateCanvases();}
        }
    }
}
