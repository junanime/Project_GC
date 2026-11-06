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
    public static class ColonBossPresentationCapture
    {
        const string Key="ColonBossPresentationCapture",Output="Library/ColonBossPresentation";
        static bool started;static double deadline;
        static ColonBossPresentationCapture(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");File.WriteAllText(Output+"/result.txt","RUNNING");
            foreach(string pref in new[]{"Coins","LobbySilverCoins"}){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);CrossSceneData.ClearPendingRunSceneTransfer();
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Key+"Load",true);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1200;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                foreach(string pref in new[]{"Coins","LobbySilverCoins"}){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();Time.timeScale=1;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            if(SessionState.GetBool(Key+"Load",false)){SessionState.SetBool(Key+"Load",false);UnityEngine.SceneManagement.SceneManager.LoadScene("Level 1");return;}
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady)return;started=true;level.StartCoroutine(CaptureAll(level));
        }
        static void Silence(LevelManager level)
        {
            level.enabled=false;RunScore.Exclude("Stage-two artwork playback QA");
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(!(m is BloodClotMonster))m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
        }
        static IEnumerator CaptureAll(LevelManager level)
        {
            var pref=GamePreferences.Current.Copy();pref.pauseOnFocusLoss=false;pref.muteOnFocusLoss=false;GamePreferences.Apply(pref,false);Application.runInBackground=true;
            Silence(level);var player=level.PlayerCharacter;ColonContentSmoke.Set(player,"isInvincible",true);yield return ColonContentSmoke.Enter(level);Silence(level);Time.timeScale=1;
            ApothecaryUI.Instance.Show("hud");var map=FiniteStomachMap.Instance;var director=level.GetComponent<ColonBossDirector>();
            string only=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-colonCapture="))?.Substring("-colonCapture=".Length);
            foreach(ColonBossKind kind in Enum.GetValues(typeof(ColonBossKind)))
            {
                if(!string.IsNullOrEmpty(only)&&!string.Equals(kind.ToString(),only,StringComparison.OrdinalIgnoreCase))continue;
                var home=director.Habitats.First(h=>h.Kind==kind);var at=(Vector2)home.transform.position;
                var front=kind==ColonBossKind.Manatee?home.Doors[1]+home.WallNormal*1.5f:at+Vector2.down*2;
                ColonContentSmoke.Position(player,map.ClampToField(front,map.WallWidth+.3f));yield return new WaitForSeconds(1.2f);
                // Overview is a clearly labelled inspection view; subsequent clips use the live gameplay camera.
                var cam=Camera.main;var cp=cam.transform.position;float zoom=cam.orthographicSize;cam.orthographicSize=kind==ColonBossKind.Manatee?6:4.5f;cam.transform.position=new Vector3(at.x,at.y+1.4f,cp.z);Frame(kind+"-home");cam.transform.position=cp;cam.orthographicSize=zoom;
                InteractionFocus.SelectTouched(home);home.TryInteract();yield return null;
                var boss=ColonBossRuntime.Active.FirstOrDefault(b=>b.Kind==kind&&!b.Dead);if(boss==null)throw new Exception("Habitat did not activate: "+kind);boss.AutoPatterns=false;
                yield return Clip(kind+"-arrival",()=>boss.Arriving,12);
                ColonContentSmoke.Position(player,map.ClampToField((Vector2)boss.transform.position+Vector2.right*5.5f+Vector2.down*.5f,map.WallWidth+.4f));yield return new WaitForSeconds(1.4f);
                float start=Time.time;yield return Clip(kind+"-walk",()=>Time.time-start<2.7f,4);
                ColonContentSmoke.Position(player,map.ClampToField((Vector2)boss.transform.position+Vector2.right*3.5f+Vector2.down*.4f,map.WallWidth+.4f));yield return new WaitForSeconds(.6f);
                int pattern=kind==ColonBossKind.Flamingo?0:kind==ColonBossKind.Shark?3:kind==ColonBossKind.Pudding?3:kind==ColonBossKind.Hedgehog?1:kind==ColonBossKind.Turtle?3:4;
                if(!boss.PlayPattern(pattern))throw new Exception("Cannot start presentation pattern: "+kind);
                yield return Clip(kind+"-pattern",()=>boss.Busy,15);
                // Presentation cleanup must not award XP and open an augment menu between clips.
                // The separate smoke/defeat suites validate actual death and rewards.
                Frame(kind+"-ready");Object.Destroy(boss.gameObject);yield return new WaitForSeconds(.2f);
                Silence(level);foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
            }
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator Clip(string name,Func<bool> running,float limit)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");float start=Time.time;int frame=0;
            do{yield return null;Frame(name+"/"+frame.ToString("D4"));File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"\n");frame++;yield return new WaitForSeconds(1f/12);}while(running()&&Time.time-start<limit);
        }
        static void Frame(string name)
        {
            var camera=Camera.main;if(camera==null)return;
            var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var flags=overlays.Select(c=>c.overrideSorting).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);Texture2D image=null;
            try{camera.targetTexture=rt;foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image=new Texture2D(960,540,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());}
            finally{for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=flags[i];}camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);Canvas.ForceUpdateCanvases();}
        }
    }
}
#endif
