using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class StageEntryLoadingSmoke
    {
        const string Key="StageEntryLoadingSmoke";
        const string Output="Library/StageEntryLoadingQA";
        static bool started;
        static double deadline;
        static int passed;
        static StageEntryLoadingSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+360;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);Debug.Log("ENTRY_TIMEOUT");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("ENTRY_FINISHED failed="+failed+" checks="+passed);EditorApplication.Exit(failed?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;
                var host=new GameObject("Stage loading smoke host").AddComponent<StageEntryLoadingTestHost>();
                Object.DontDestroyOnLoad(host);host.StartCoroutine(Checks());
            }
        }
        static void Check(bool ok,string message)
        {if(!ok)throw new Exception("ENTRY_FAIL "+message);passed++;Debug.Log("ENTRY_PASS "+message);}
        static IEnumerator Checks()
        {
            Application.runInBackground=true;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.reducedMotion=false;GamePreferences.Apply(prefs,false);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var art=Resources.Load<StageEntryLoadingArt>("StageEntryLoadingArt");
            Check(art!=null&&art.corridor!=null&&art.bacteria.Length>0&&art.slime.Length>0,"background and original scene monsters linked");
            Check(StageEntryLoading.NormalizedProgress(0)==0&&Mathf.Abs(StageEntryLoading.NormalizedProgress(.9f)-.94f)<.001f,"real async .0-.9 progress normalized; last 6% is entry transition");
            foreach(var key in new[]{"Hyuki","Shini","Ari","Ashi"})
            {
                var bp=config.characters.First(c=>OctoberArt.CharacterKey(c)==key);
                Check(art.Runner(key).Length==8,key+" has 8 rear-facing frames");
                Check(art.Runner(key).All(s=>s.rect.size==art.Runner(key)[0].rect.size),key+" consistent frame canvas size");
                var idle=bp.idleSpriteSequence[0];var visible=VisibleBodyGeometry.Bounds(idle);
                Check(visible.height<idle.bounds.size.y*.9f,key+" baked silhouette omits transparent margins");
                var preview=StageEntryLoading.Create(bp);yield return null;
                preview.SetProgress(.15f);Capture(preview,key+"-loading-15");
                var start=preview.RunnerRect.anchoredPosition;float size=preview.RunnerRect.localScale.x;
                preview.SetProgress(.68f);yield return null;Capture(preview,key+"-loading-68");
                Check(preview.RunnerRect.anchoredPosition.x>start.x&&preview.RunnerRect.anchoredPosition.y>start.y&&preview.RunnerRect.localScale.x<size,key+" moves and shrinks toward opening with progress");
                preview.SetProgress(1);Check(preview.RunnerAlpha==0,key+" disappears on completion");
                Object.Destroy(preview.gameObject);yield return null;

                CrossSceneData.CharacterBlueprint=bp;CrossSceneData.ClearStartingLobbyItems();
                Check(StageEntryLoading.Begin(bp),key+" starts real async scene load");
                Check(!StageEntryLoading.Begin(bp),key+" duplicate departure blocked");
                float last=0;int samples=0;bool captured=false;
                while(StageEntryLoading.IsLoading)
                {
                    var view=StageEntryLoading.Instance;
                    if(view.Progress<last-.0001f)throw new Exception("Loading progress regressed");
                    last=view.Progress;samples++;
                    if(!captured&&view.Progress>.3f&&view.Progress<.8f){Capture(view,key+"-actual-load");captured=true;}
                    yield return null;
                }
                yield return null;
                Check(SceneManager.GetActiveScene().buildIndex==1&&samples>2&&last>=.99f,key+" scene activates only after visible progress finishes");
                var level=Object.FindObjectOfType<LevelManager>();
                Check(level?.PlayerCharacter?.Blueprint==bp,key+" selection survives scene transition");
                Check(!StageEntryLoading.IsLoading&&Object.FindObjectsOfType<StageEntryLoading>().Length==0&&Time.timeScale==1,key+" overlay and loading pause released");
                if(TutorialGuide.Instance!=null)TutorialGuide.Instance.enabled=false;
                foreach(var d in Object.FindObjectsOfType<StageEventDirector>())d.enabled=false;
                foreach(var d in Object.FindObjectsOfType<TimedSpecialMonsterSpawner>())d.enabled=false;
                foreach(var d in Object.FindObjectsOfType<AcidToadSpawn>())d.enabled=false;
                var player=level.PlayerCharacter;player.Move(Vector2.zero);
                var travel=BloodClotTravel.Ensure(player);
                var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
                var portal=new GameObject("Scale QA portal");portal.transform.position=player.transform.position+Vector3.right;
                var sr=portal.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/mini_open.png");
                Vector3 origin=player.transform.position;
                float expected=body.transform.TransformVector(Vector3.up*visible.height).magnitude;
                for(int repeat=0;repeat<2;repeat++)
                {
                    var dive=player.StartCoroutine(travel.Dive(portal.transform));
                    Check(Mathf.Abs(travel.ReferenceHeight-expected)<.0001f,key+" travel uses opaque idle height "+repeat);
                    Check(Mathf.Abs(travel.Visual.bounds.size.y-expected)<.012f,key+" opening pose matches original visible height "+repeat);
                    float journeyScale=travel.Visual.transform.localScale.x;
                    yield return dive;
                    yield return travel.Eject(portal.transform,origin);
                    Check(travel.HoldingPose&&Mathf.Abs(travel.Visual.transform.localScale.x-journeyScale)<.0001f,key+" fixed calibrated scale through dive/eject "+repeat);
                    if(key=="Hyuki")Check(Mathf.Abs(travel.Visual.sprite.bounds.size.y*journeyScale/expected-.72f)<.005f,"Hyuki curl stays at 72% of true original height");
                    player.Move(Vector2.left);yield return null;yield return null;
                    Check(!travel.HoldingPose,key+" movement restores regular art "+repeat);player.Move(Vector2.zero);
                }
                Object.Destroy(portal);
                var returnLoad=SceneManager.LoadSceneAsync(0);while(!returnLoad.isDone)yield return null;yield return null;
            }
            SessionState.SetBool(Key+"Done",true);
        }
        static void Capture(StageEntryLoading view,string name)
        {
            var canvas=view.GetComponent<Canvas>();
            var cam=new GameObject("Loading QA camera").AddComponent<Camera>();cam.orthographic=true;cam.orthographicSize=5;cam.clearFlags=CameraClearFlags.SolidColor;
            cam.transform.position=new Vector3(0,0,-20);
            var rt=RenderTexture.GetTemporary(1280,720,24);cam.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;
            Canvas.ForceUpdateCanvases();cam.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
            RenderTexture.active=old;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
            cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(cam.gameObject);
        }
    }
    public sealed class StageEntryLoadingTestHost:MonoBehaviour {}
}
