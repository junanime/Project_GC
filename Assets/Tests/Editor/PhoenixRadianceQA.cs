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
    public static class PhoenixRadianceQA
    {
        const string Session="PhoenixRadianceQA";
        static double deadline;
        static bool started;
        static int checks;
        static string output;
        static PhoenixRadianceQA(){if(SessionState.GetBool(Session,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Session,true);SessionState.SetBool(Session+"Done",false);SessionState.SetBool(Session+"Fail",false);
            Directory.CreateDirectory("Library/PhoenixRadianceQA");
            File.WriteAllText("Library/PhoenixRadianceQA/errors.txt", "");
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+600;started=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {
            if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)
            {SessionState.SetBool(Session+"Fail",true);File.AppendAllText("Library/PhoenixRadianceQA/errors.txt",message+"\n"+trace+"\n");}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Session,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Session+"Fail",true);
            if(SessionState.GetBool(Session+"Done",false)||SessionState.GetBool(Session+"Fail",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool fail=SessionState.GetBool(Session+"Fail",false);SessionState.SetBool(Session,false);
                Debug.Log("RADIANCE_FINISHED failed="+fail+" checks="+checks);
                EditorApplication.Exit(fail?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Radiance QA host").AddComponent<PhoenixNobleTestHost>();
                Object.DontDestroyOnLoad(host);host.StartCoroutine(Test());
            }
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("RADIANCE_FAIL "+message);checks++;Debug.Log("RADIANCE_PASS "+message);}
        static FieldInfo F(object target,string name)
        {
            for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}
            throw new Exception("Missing field "+name);
        }
        static void Set(object o,string n,object v)=>F(o,n).SetValue(o,v);
        static object Get(object o,string n)=>F(o,n).GetValue(o);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static IEnumerator Test()
        {
            Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            string[] sheets={"RainbowFan","CloneWings","RequiemPhoenix","GoldPhoenix","FeatherKit","BlueWing","QuillNeedle","HeavyCharge","GoldRing","CometRibbon"};
            foreach(var name in sheets)
            {
                var texture=Resources.Load<Texture2D>("PhoenixRadiance/"+name);
                Check(texture!=null&&texture.width%4==0&&texture.height%3==0,name+" twelve-cell atlas");
                var test=new Texture2D(2,2);test.LoadImage(File.ReadAllBytes("Assets/Resources/PhoenixRadiance/"+name+".png"));
                var pixels=test.GetPixels32();Check(pixels.Count(p=>p.a<5)>pixels.Length*.12f,name+" actual transparent alpha");Object.Destroy(test);
            }
            var shader=Resources.Load<Shader>("PhoenixRadiance/PhoenixRadiance");Check(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"radiance shader compiles");
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            CrossSceneData.CharacterBlueprint=config.characters[0];CrossSceneData.ClearStartingLobbyItems();
            Check(StageEntryLoading.Begin(config.characters[0]),"enter actual level");
            while(StageEntryLoading.IsLoading)yield return null;
            yield return null;yield return null;
            var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;
            var needle=Object.FindObjectOfType<SyringeDartAbility>();Check(needle!=null&&player!=null,"live ability and player");
            level.SetRunFlowPaused(true);if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            if(TutorialGuide.Instance!=null){Set(TutorialGuide.Instance,"ready",false);}
            Set(player,"isInvincible",true);needle.enabled=false;Time.timeScale=1;Time.captureFramerate=20;
            var body=SyringeAugmentVfx.FindTarget(player);
            var abilities=Object.FindObjectsOfType<SyringeLegendaryAugmentAbility>(true);
            Check(abilities.Where(a=>a.Type==SyringeLegendaryAugmentAbility.LegendaryAugmentType.NeedleShotgun).All(a=>!a.AvailableAsNoble),"scattered feathers withheld from offer pool");
            output="Library/PhoenixRadianceQA";

            needle.EnableLifeBurnLegendary();Call(needle,"UpdateLifeBurnVisual");
            yield return Record("LifeBurn",player,30);
            var life=Object.FindObjectsOfType<PhoenixRadianceVisual>().First(f=>f.Key=="LifeBurn");
            float paused=life.Age;Time.timeScale=0;yield return null;paused=life.Age;yield return null;yield return null;
            Check(Mathf.Approximately(paused,life.Age),"animation respects pause");Time.timeScale=1;
            Set(needle,"lifeBurnEnabled",false);Call(needle,"UpdateLifeBurnVisual");

            var clone=SyringeCloneController.Create(player,level.EntityManager,needle);
            yield return Record("CloneCulture",player,30);
            var cloneBody=clone.GetComponent<SpriteRenderer>();
            Check(Mathf.Abs(cloneBody.bounds.size.y/body.bounds.size.y-1f/3)<.015f,"clone body exactly one-third");Object.Destroy(clone.gameObject);yield return null;

            needle.EnableHedgehogNeedleLegendary();yield return null;
            var orbit=Object.FindObjectOfType<HedgehogNeedleController>();
            yield return Record("HedgehogNeedle",player,30);
            foreach(var feather in orbit.GetComponentsInChildren<PhoenixRadianceVisual>())
                if(feather.Key=="OrbitFeather")Check(Vector3.Dot(feather.transform.right,(feather.transform.position-orbit.transform.position).normalized)>.999f,"cogwheel needle outward");
            Object.Destroy(orbit.gameObject);Set(needle,"hedgehogNeedleEnabled",false);yield return null;

            needle.EnableHeavySnipeLegendary();
            yield return Record("HeavySnipe",player,40,i=>
            {
                if(i<28)Call(needle,"UpdateHeavySnipeChargePreview",Mathf.Clamp01(i/24f),Vector2.right);
                if(i==28){Call(needle,"FireHeavySnipe",1f);Call(needle,"HideHeavySnipeChargePreview");}
            });
            Call(needle,"HideHeavySnipeChargePreview");Set(needle,"heavySnipeEnabled",false);

            needle.EnableCursorControlLegendary();
            needle.EnablePoisonAugment();needle.EnableExplosionAugment();needle.EnableHomingAugment();needle.EnableFiberNeedleAugment();
            needle.EnableCorrosionNeedleAugment();needle.EnablePressureNeedleAugment();needle.EnableMarkNeedleAugment();needle.EnableHoneyAugment();
            yield return Record("CursorControl",player,40);
            var cursor=Object.FindObjectOfType<CursorControlledNeedleController>();
            var glow=Object.FindObjectsOfType<PhoenixRadianceVisual>().First(f=>f.Key=="CursorControlGlow");
            Check(glow.ColorStages==7,"comet clamps to seven colours with eight weapons");
            Check(!cursor.transform.Find("Cursor Needle Back Display").gameObject.activeSelf,"no player-back ornament");
            Object.Destroy(cursor.gameObject);Set(needle,"cursorControlEnabled",false);yield return null;

            needle.EnableNeuralBlockLegendary();var neural=Object.FindObjectOfType<NeuralBlockController>();
            for(int i=0;i<3;i++)
            {
                var spawnAt=(Vector2)player.transform.position+new Vector2(1.8f+i*.35f,.3f+i*.7f);
                level.EntityManager.SpawnMonster(0,spawnAt,level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0],1000);
            }
            Set(neural,"interval",100f);Set(neural,"freezeDuration",2f);
            neural.StartCoroutine((IEnumerator)Call(neural,"SummonThenBlock"));
            yield return Record("NeuralBlock",player,44);
            Check(Object.FindObjectsOfType<NeuralBlockedMonsterStatus>().Any(s=>s.Active),"summon completes then enemies receive feather freeze");
            Object.Destroy(neural);Set(needle,"neuralBlockEnabled",false);yield return null;

            needle.EnableGastricPeristalsisWaveAugment();
            var wave=player.GetComponent<GastricPeristalsisWaveController>();
            Set(wave,"waveDuration",1.3f);Set(wave,"maxRadius",3.4f);
            yield return Record("GastricPeristalsisWave",player,34);
            Object.Destroy(wave);Set(needle,"gastricPeristalsisWaveEnabled",false);yield return null;

            needle.EnableMucosalFortressAugment();var shield=player.GetComponent<MucosalFortressShieldController>();
            Set(shield,"noDamageSeconds",.7f);
            yield return Record("MucosalFortress",player,56);
            Check(shield.CurrentStacks==3,"three feathered shield layers acquired");
            Check(shield.TryConsumeShieldStack()&&shield.CurrentStacks==2,"one hit consumes exactly one layer");
            Object.Destroy(shield);Set(needle,"mucosalFortressEnabled",false);yield return null;

            needle.EnableHungrySpiritLegendary();var hunger=player.GetComponent<HungrySpiritController>();
            hunger.Configure(player,.2f,9,.01f,.01f,.01f,false);
            yield return Record("HungrySpirit",player,52,i=>hunger.Configure(player,.2f,9,.01f,.01f,.01f,false));
            Check(hunger.CurrentStacks==9,"repeated configure preserves accumulation to max");
            Call(hunger,"HandleExpOrCoinPickedUp",player);Check(hunger.CurrentStacks==0,"pickup clears peacock tail stacks");
            Object.Destroy(hunger);Set(needle,"hungrySpiritEnabled",false);
            Time.captureFramerate=0;
            SessionState.SetBool(Session+"Done",true);
        }
        static IEnumerator Record(string name,Character player,int frames,Action<int> before=null)
        {
            Directory.CreateDirectory(output+"/"+name);
            bool visible=false;
            for(int i=0;i<frames;i++)
            {
                before?.Invoke(i);Time.timeScale=1;
                yield return null;
                visible |= Object.FindObjectsOfType<PhoenixRadianceVisual>().Any() || Object.FindObjectsOfType<PhoenixSummonVisual>().Any();
                // Render after all LateUpdates without blocking batch mode on WaitForEndOfFrame.
                Capture(name,i,player);
            }
            Check(visible,name+" live radiance present during sequence");
        }
        static void Capture(string name,int index,Character player)
        {
            var source=Camera.main;var go=new GameObject("QA render camera");var cam=go.AddComponent<Camera>();cam.CopyFrom(source);
            cam.enabled=false;cam.orthographic=true;cam.orthographicSize=3.5f;
            cam.transform.position=new Vector3(player.transform.position.x+.6f,player.transform.position.y+1,source.transform.position.z);
            cam.transform.rotation=source.transform.rotation;
            var rt=RenderTexture.GetTemporary(960,600,24);cam.targetTexture=rt;cam.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;
            var pixels=new Texture2D(960,600,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,960,600),0,0);pixels.Apply();
            File.WriteAllBytes(output+"/"+name+"/"+index.ToString("D3")+".png",pixels.EncodeToPNG());
            Object.Destroy(pixels);RenderTexture.active=old;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(go);
        }
    }
}
