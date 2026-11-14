#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
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
    public static class OralStageRoofCapture
    {
        const string Key="OralStageRoofCapture",Root="Library/OralRoofProof";
        static bool started; static double deadline;
        [Serializable]class Pref{public string key;public bool had;public int value;}
        [Serializable]class Backup{public Pref[] prefs;}
        static OralStageRoofCapture(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Root+"/06-ceiling"); Restore();
            foreach(var p in Directory.GetFiles(Root+"/06-ceiling","*.png"))File.Delete(p);
            File.WriteAllText(Root+"/result.txt","RUNNING");File.WriteAllText(Root+"/errors.txt","");
            File.WriteAllText(Root+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=new[]{"Coins","LobbySilverCoins"}.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.ClearPendingRunSceneTransfer();CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string s,string trace,LogType type){if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;File.AppendAllText(Root+"/errors.txt",s+"\n"+trace+"\n");SessionState.SetBool(Key+"Failed",true);}
        static void Restore(){if(!File.Exists(Root+"/prefs.json"))return;foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(Root+"/prefs.json")).prefs)if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);PlayerPrefs.Save();File.Delete(Root+"/prefs.json");}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;Restore();SessionState.SetBool(Key,false);bool fail=SessionState.GetBool(Key+"Failed",false);File.WriteAllText(Root+"/result.txt",fail?"FAIL":"PASS native-camera roof recording, real event enemy deaths and safe return");EditorApplication.Exit(fail?1:0);return;}
            if(!EditorApplication.isPlaying)return;
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;started=true;level.StartCoroutine(Record(level));
        }
        static void Call(string name,params object[] args)=>typeof(OralStageSmoke).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        static IEnumerator Record(LevelManager level)
        {
            RunScore.Exclude("Stage zero native-camera visual recording");var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;
            Call("Silence",level);var player=level.PlayerCharacter;Call("Set",player,"isInvincible",true);player.CollectableCollider.enabled=false;
            if(!StageProgression.Ensure(level).BeginDirectOral(true))throw new Exception("Oral transition refused");
            float timeout=Time.realtimeSinceStartup+120;
            while(StageProgression.Ensure(level).Travelling&&Time.realtimeSinceStartup<timeout)yield return null;
            if(OralStageRuntime.Instance==null)throw new Exception("Oral runtime absent");
            Call("Silence",level);Call("Clear",level);Call("Place",player,OralMapGeometry.TongueCenter(FiniteStomachMap.Instance));
            var events=OralFieldEvents.Instance;events.AutoSchedule=false;
            var weapon=Object.FindObjectsOfType<ProjectileAbility>().First(a=>a.Owned);
            yield return new WaitForSeconds(.7f);
            if(!events.ForceEvent(OralEventKind.Ceiling))throw new Exception("Ceiling trigger refused");
            float start=Time.time,combatStart=-1,lastHit=-1,ended=-1;int frame=0;bool sawCombat=false;
            level.enabled=true;File.WriteAllText(Root+"/06-ceiling/times.csv","frame,game_seconds\n");
            do
            {
                if(events.CeilingPhase==2)
                {
                    sawCombat=true;if(combatStart<0){combatStart=Time.time;weapon.enabled=true;Call("Set",weapon,"timeSinceLastAttack",100f);}
                    player.Move(Time.time-combatStart<1.8f?Vector2.right:Vector2.zero);
                    if(Time.time-combatStart>2.8f&&Time.time-lastHit>.8f)
                    {weapon.enabled=false;weapon.StopAllCoroutines();var target=events.CeilingMonsters.FirstOrDefault(m=>m!=null&&m.HP>0);if(target!=null)target.TakeDamage(target.HP+1);lastHit=Time.time;}
                }
                if(events.CurrentEvent==OralEventKind.None&&ended<0)ended=Time.time;
                yield return null; Frame(frame++);File.AppendAllText(Root+"/06-ceiling/times.csv",(frame-1)+","+(Time.time-start).ToString("F4",CultureInfo.InvariantCulture)+"\n");yield return new WaitForSeconds(1f/12);
            }while(Time.time-start<30&&(ended<0||Time.time-ended<1));
            player.Move(Vector2.zero);weapon.enabled=false;level.enabled=false;
            if(!sawCombat||events.CurrentEvent!=OralEventKind.None||OralStageRuntime.Instance.OnCeiling)throw new Exception("Roof recording did not complete actual combat and return");
            File.WriteAllText(Root+"/capture-method.txt","Native follow camera and actual HUD throughout. Scripted rightward input demonstrates ceiling movement. Equipped weapon fires; remaining event enemies receive HP+1 test damage individually to demonstrate the real kill-gated return. Original 850-check integration report retained separately.\n");
            SessionState.SetBool(Key+"Done",true);
        }
        static void Frame(int number)
        {
            var camera=Camera.main;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var flags=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D image=null;
            try{camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Root+"/06-ceiling/"+number.ToString("D4")+".png",image.EncodeToPNG());}
            finally{for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=flags[i];}camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);Canvas.ForceUpdateCanvases();}
        }
    }
}
#endif
