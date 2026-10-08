using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Editor
{
    [InitializeOnLoad]
    public static partial class ChameleonSmoke
    {
        const string Key="ChameleonSmoke";static bool running;static double deadline;static int checks;
        [Serializable]class Catalog{public TutorialGuide.Entry[] entries;}
        static ChameleonSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            string[] keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).ToArray();
            SessionState.SetString(Key+"Keys",string.Join("|",keys));
            foreach(var k in keys){SessionState.SetBool(Key+k+"Exists",PlayerPrefs.HasKey(k));SessionState.SetInt(Key+k,PlayerPrefs.GetInt(k));}
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){running=false;checks=0;deadline=EditorApplication.timeSinceStartup+480;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("CHAMELEON_TIMEOUT");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                foreach(var k in SessionState.GetString(Key+"Keys","").Split('|')){if(SessionState.GetBool(Key+k+"Exists",false))PlayerPrefs.SetInt(k,SessionState.GetInt(Key+k,0));else PlayerPrefs.DeleteKey(k);}
                PlayerPrefs.Save();bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("CHAMELEON_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();if(!running&&EditorApplication.isPlaying&&level?.PlayerCharacter!=null){running=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool ok,string text){if(!ok)throw new Exception("CHAMELEON_FAIL "+text);checks++;Debug.Log("CHAMELEON_PASS "+text);}
        static void Capture(string name)
        {
            Directory.CreateDirectory("Library/ChameleonProof");
            typeof(Vampire.Tests.Editor.Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"chameleon-"+name+".png"});
            File.Copy(Path.GetFullPath("../work/chameleon-"+name+".png"),"Library/ChameleonProof/"+name+".png",true);
        }
        static object Get(object obj,string field){for(Type t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f.GetValue(obj);}throw new Exception(field);}
        static void Set(object obj,string field,object value){for(Type t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null){f.SetValue(obj,value);return;}}throw new Exception(field);}
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var tutorial=Object.FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}Time.timeScale=1;Time.captureDeltaTime=1f/60f;
            var director=Object.FindObjectOfType<StageEventDirector>();director.enabled=false;
            var timed=Object.FindObjectOfType<TimedSpecialMonsterSpawner>();if(timed!=null)timed.enabled=false;
            var schedule=Object.FindObjectOfType<AcidToadSpawn>();schedule.enabled=false;
            var player=level.PlayerCharacter;Set(player,"currentHealth",100000f);
            foreach(var ability in player.GetComponentsInChildren<Ability>())ability.enabled=false;
            ((Collider2D)Get(player,"collectableCollider")).enabled=false;
            Set(player,"nextLevelExp",1000000f);Set(player,"expToNextLevel",1000000f);
            for(int k=0;k<4;k++)
            {
                var kind=(ChameleonKind)k;var frames=ChameleonArt.Frames(kind.ToString());
                Check(frames.Length==16,kind+" 16 distinct pose frames");
                Check(frames.All(s=>Mathf.Abs(s.bounds.size.x-ChameleonArt.Width)<.00001f&&s.pivot.y==0),kind+" exact common width and foot pivot");
                var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Chameleons/"+kind+".png");
                Check(importer.textureCompression==TextureImporterCompression.Uncompressed&&!importer.mipmapEnabled,kind+" no compression");
                Check(ChameleonArt.Projectile(kind)!=null&&ChameleonArt.Portrait(kind,11)!=null,kind+" projectile and upper-body frame");
            }
            var lottery=new ChameleonLottery();Check(Enumerable.Range(0,4).All(i=>lottery.Weight((ChameleonKind)i)==25),"equal initial 25 percent");
            lottery.Record(ChameleonKind.Drift);Check(Mathf.Abs(lottery.Weight(ChameleonKind.Drift)-45)<.001f,"one event adds 20 percentage points");
            for(int n=0;n<20;n++)lottery.Record(ChameleonKind.Drift);
            Check(Mathf.Abs(lottery.Weight(ChameleonKind.Drift)-100)<.001f,"donor depletion caps at 100");
            for(int seed=0;seed<50;seed++)
            {
                var l=new ChameleonLottery();var random=new System.Random(seed);
                for(int n=0;n<50;n++){l.Record((ChameleonKind)random.Next(4));if(Mathf.Abs(Enumerable.Range(0,4).Sum(i=>l.Weight((ChameleonKind)i))-100)>.001f||Enumerable.Range(0,4).Any(i=>l.Weight((ChameleonKind)i)<-.00001f))throw new Exception("Invalid probability");}
            }
            Check(true,"2500 event updates conserve 100 and stay nonnegative");lottery.Pick(.5f);lottery.Record(ChameleonKind.Fanta);Check(lottery.Weight(ChameleonKind.Fanta)==0,"lottery frozen at spawn");
            var containers=level.CurrentLevelBlueprint.monsters;int index=Array.FindIndex(containers,c=>c.monstersPrefab.GetComponent<AcidToadMonster>()!=null);
            Check(index>=0&&containers[index].monsterBlueprints.Length==4,"four blueprints registered in level pool");
            Check(schedule.SpawnAt>=420&&schedule.SpawnAt<=450,"existing 7:00-7:30 spawn window");
            for(int k=0;k<4;k++)
            {
                var bp=containers[index].monsterBlueprints[k];var boss=(AcidToadMonster)level.EntityManager.SpawnMonster(index,(Vector2)player.transform.position+Vector2.right*3,bp,100000,false);boss.AutoPatterns=false;
                yield return new WaitForSeconds(.15f);Vector3 scale=boss.transform.localScale;
                Check(boss.Kind==(ChameleonKind)k&&boss.Motion.Art.sprite!=null,"live "+boss.Kind+" identity");
                Check(boss.Motion.Art.sprite.vertices.Length>4,boss.Kind+" isolated body geometry applied in player loop");Capture(boss.Kind+"-idle");
                Check(boss.UsePattern(AcidToadMonster.Pattern.Basic),boss.Kind+" basic starts");yield return new WaitForSeconds(.68f);
                Check(Object.FindObjectsOfType<ChameleonProjectile>().Count(p=>p.name.StartsWith(boss.Kind.ToString()))==3,boss.Kind+" exactly three themed projectiles");
                while(boss.Busy)yield return null;
                Check(boss.UsePattern(AcidToadMonster.Pattern.Leap),boss.Kind+" teleport starts");yield return new WaitForSeconds(.5f);
                Check(boss.Invisible&&boss.Motion.Art.color.a<.01f&&!boss.BodyHitbox.enabled,boss.Kind+" invisible and noncolliding");
                float hp=boss.HP;boss.TakeDamage(100);Check(boss.HP==hp,boss.Kind+" invisible cannot be hit");
                yield return new WaitForSeconds(2.98f);var warning=boss.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(s=>s.name=="Chameleon silhouette warning");
                Check(warning!=null&&boss.Invisible&&boss.Motion.Art.color.a<.01f,boss.Kind+" silhouette appears after 3 seconds with body hidden");Capture(boss.Kind+"-outline");
                Vector3 target=boss.transform.position;player.transform.position+=Vector3.up*4;yield return new WaitForSeconds(.3f);
                Check(((Vector2)(boss.transform.position-target)).sqrMagnitude<.001f,boss.Kind+" telegraph target locks in gameplay XY");
                while(boss.Busy)yield return null;Check(!boss.Invisible&&boss.BodyHitbox.enabled,boss.Kind+" reappears with collision restored");
                boss.transform.position=player.transform.position+Vector3.right*2.5f;boss.GetComponent<Rigidbody2D>().position=boss.transform.position;
                Check(boss.UsePattern(AcidToadMonster.Pattern.Jet),boss.Kind+" special starts");float age=0;bool captured=false;
                while(boss.Busy&&age<10)
                {
                    age+=Time.deltaTime;if((boss.transform.localScale-scale).sqrMagnitude>.000001f)throw new Exception("Scale changed");
                    if(!captured&&age>1.35f){Capture(boss.Kind+"-skill");captured=true;}
                    CheckNoPortrait();yield return null;
                }
                Check(!boss.Busy,boss.Kind+" special recovers");if(k==0)Check(boss.DriftPulses==2,"spawned drift skill emits exactly twice");
                boss.UsePattern(AcidToadMonster.Pattern.Leap);yield return new WaitForSeconds(.5f);boss.SetFieldRuntimeSuspended(true);yield return null;
                Check(!boss.Busy&&!boss.Invisible&&boss.Motion.Art.color.a>.99f,boss.Kind+" suspension restores hidden actor");boss.SetFieldRuntimeSuspended(false);
                Check(boss.GetComponent<Rigidbody2D>().simulated,boss.Kind+" physics resumes after interrupted teleport");
                boss.StartCoroutine(boss.Killed(false));yield return new WaitForSeconds(.5f);Capture(boss.Kind+"-death");yield return new WaitForSeconds(2);
                Check(!level.EntityManager.LivingMonsters.Contains(boss),boss.Kind+" death unregisters pool actor");
            }
            var portrait=ChameleonPortrait.Create(level.transform,ChameleonKind.Drift,true);portrait.Sample(11);yield return null;Canvas.ForceUpdateCanvases();
            Vector3 cameraPosition=Camera.main.transform.position;Quaternion cameraRotation=Camera.main.transform.rotation;
            Vector3[] before=new Vector3[4],after=new Vector3[4];portrait.Panel.GetWorldCorners(before);Camera.main.transform.position+=new Vector3(12,7,0);Camera.main.transform.rotation=Quaternion.Euler(0,0,7);yield return null;
            portrait.Panel.GetWorldCorners(after);Check(Enumerable.Range(0,4).All(i=>(before[i]-after[i]).sqrMagnitude<.0001f),"UI panel unaffected by camera movement/tilt");
            Camera.main.transform.SetPositionAndRotation(cameraPosition,cameraRotation);yield return new WaitForSeconds(.2f);Capture("fixed-panel");Object.Destroy(portrait.gameObject);
            var fanta=level.StartCoroutine(AcidToadEvent.Emit(level.transform,new Rect(-8,-5,16,10),true,1,1.6f,1.5f,0,.6f,player));yield return new WaitForSeconds(1.2f);
            Check(Object.FindObjectsOfType<ChameleonSodaWave>().Length==1&&Object.FindObjectsOfType<AcidJet>().Length==0,"field Fanta is one moving wave, no tiled stream");Capture("fanta-field");yield return fanta;
            var foam=level.StartCoroutine(ChameleonPortrait.Foam(level.transform,1.5f));yield return new WaitForSeconds(.8f);Capture("foam-field");yield return foam;
            var coffee=level.StartCoroutine(ChameleonPortrait.Coffee(level.transform));yield return new WaitForSeconds(1.3f);Capture("coffee-cover");yield return coffee;
            yield return null;
            Check(Object.FindObjectsOfType<ChameleonPortrait>().Length==0&&Object.FindObjectsOfType<ChameleonCoffeeWash>().Length==0,"all event panels cleaned");
            yield return Integration(level,director,schedule,index);
            int bosses=level.EntityManager.LivingMonsters.Count(m=>m is AcidToadMonster);typeof(AcidToadSpawn).GetProperty("SpawnAt").SetValue(schedule,0f);
            var tick=typeof(AcidToadSpawn).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);tick.Invoke(schedule,null);tick.Invoke(schedule,null);
            Check(schedule.Spawned&&level.EntityManager.LivingMonsters.Count(m=>m is AcidToadMonster)==bosses+1,"scheduled weighted spawn occurs exactly once");
            Debug.Log("CHAMELEON_CHECKS="+checks);SessionState.SetBool(Key+"Done",true);
        }
        static void CheckNoPortrait(){if(Object.FindObjectsOfType<ChameleonPortrait>().Length!=0)throw new Exception("Spawned skill created portrait");}
    }
}
