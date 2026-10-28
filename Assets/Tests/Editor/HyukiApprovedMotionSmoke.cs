using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class HyukiApprovedMotionSmoke
    {
        const string Key="HyukiApprovedMotionSmoke",Output="Library/HyukiApprovedMotionProof";
        static bool started;static double deadline;static int checks;
        static HyukiApprovedMotionSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Select()
        {
            if(!SessionState.GetBool(Key,false))return;
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>(EditorTools.HyukiApprovedMotionInstaller.Blueprint);
            typeof(IntroMovie).GetField("attempted",BindingFlags.NonPublic|BindingFlags.Static)?.SetValue(null,true);
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/motion");File.WriteAllText(Output+"/errors.txt","");
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;checks=0;deadline=EditorApplication.timeSinceStartup+480;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string m,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",m+"\n"+trace+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);File.AppendAllText(Output+"/errors.txt","timeout");}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Time.captureDeltaTime=0;Time.timeScale=1;SessionState.SetBool(Key,false);
                File.WriteAllText(Output+"/result.txt",(SessionState.GetBool(Key+"Failed",false)?"FAIL":"PASS")+" checks="+checks);
                EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                started=true;var host=new GameObject("Hyuki motion QA").AddComponent<HyukiMotionQAHost>();Object.DontDestroyOnLoad(host);host.StartCoroutine(Verify());
            }
        }
        static void Check(bool ok,string m){if(!ok)throw new Exception("[HyukiMotionQA] "+m);checks++;Debug.Log("[HyukiMotionQA] PASS "+m);}
        static IEnumerator Verify()
        {
            Application.runInBackground=true;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.reducedMotion=false;GamePreferences.Apply(prefs,false);
            Time.captureDeltaTime=0;Time.timeScale=1;
            var bp=CrossSceneData.CharacterBlueprint;
            var loadingArt=Resources.Load<StageEntryLoadingArt>("StageEntryLoadingArt");
            Check(loadingArt.hyuki.Length==8&&loadingArt.hyuki.All(s=>s.texture.name=="HyukiApproved"),"loading rear run uses updated silhouette");
            Check(bp.walkSpriteSequence.Length==16&&bp.idleSpriteSequence.Length==16,"both approved 16-frame sequences installed");
            Check(Mathf.Abs(bp.walkFrameTime*16-.96f)<.0001f&&Mathf.Abs(bp.idleFrameTime*16-.8f)<.0001f,"approved cycle durations");
            Check(bp.resultIdleSpriteSequence.SequenceEqual(bp.idleSpriteSequence)&&bp.resultIdleFrameTime==bp.idleFrameTime,"legacy result idle shares approved frames and timing");
            var all=bp.walkSpriteSequence.Concat(bp.idleSpriteSequence).ToArray();
            Check(all.All(s=>s.pixelsPerUnit==all[0].pixelsPerUnit&&s.pivot==all[0].pivot&&s.rect.size==all[0].rect.size),"fixed scale, pivot and cell size across states");
            Check(bp.dashSpriteSequence.Concat(bp.capturedSpriteSequence).Concat(BloodClotTravel.Frames("Hyuki")).All(s=>s.pixelsPerUnit==all[0].pixelsPerUnit&&s.pivot==all[0].pivot),"dash, capture and travel share gameplay scale and pivot");
            foreach(var s in all)
            {
                var b=VisibleBodyGeometry.Bounds(s);Check(b.yMin>=-.272f&&b.yMin<=-.253f&&b.height>.57f&&b.height<.61f,"body height and authored foot lift "+s.name);
            }
            Capture("00-title",Camera.main);
            var ui=ApothecaryUI.Instance;ui.Show("prepare");yield return null;yield return null;
            var preview=ui.GetComponentInChildren<PreparationSkillPreview>();Check(preview!=null&&preview.character==bp,"preparation uses Hyuki blueprint");
            var seen=new HashSet<Sprite>();
            float previewStart=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-previewStart<3.3f){yield return null;seen.Add(preview.body.sprite);}
            Check(seen.SetEquals(bp.idleSpriteSequence),"preparation plays every new idle frame");
            Check(preview.body.rectTransform.anchorMin==new Vector2(.5f,0)&&preview.body.rectTransform.anchorMax==new Vector2(.5f,0),"preparation uses grounded pivot, no extra UI bob");
            Capture("01-preparation",Camera.main);
            ui.GetComponentsInChildren<Button>().First(b=>b.name=="Button 출전하기").onClick.Invoke();
            while(Object.FindObjectOfType<LevelManager>()==null||StageEntryLoading.IsLoading)yield return null;
            var level=Object.FindObjectOfType<LevelManager>();
            while(level.PlayerCharacter==null||level.GetComponent<FieldObjectLayout>()?.Ready!=true)yield return null;
            RunScore.Exclude("Hyuki approved motion QA");
            Time.captureDeltaTime=.02f;
            level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            var player=level.PlayerCharacter;Check(player.Blueprint==bp,"actual player uses approved blueprint");
            var sr=player.GetComponentInChildren<SpriteAnimator>().SpriteRenderer;var cam=Camera.main;float zoom=cam.orthographicSize;
            player.Move(Vector2.zero);yield return new WaitForSeconds(.12f);seen.Clear();
            for(int f=0;f<82;f++){yield return null;seen.Add(sr.sprite);if(f<40&&f%2==0)Capture("motion/idle-"+(f/2).ToString("00"),cam);}
            Check(seen.SetEquals(bp.idleSpriteSequence),"Level1 plays every idle frame");Capture("02-ingame-idle",cam);
            cam.orthographicSize=zoom*.40f;yield return null;Capture("03-idle-close",cam);seen.Clear();Vector3 start=player.transform.position;float travelled=0;
            for(int f=0;f<122;f++)
            {
                player.Move(f<61?Vector2.right:Vector2.left);yield return null;
                travelled=Mathf.Max(travelled,Vector3.Distance(start,player.transform.position));
                if(f>1)seen.Add(sr.sprite);
                if(f<48&&f%3==0)Capture("motion/walk-"+(f/3).ToString("00"),cam);
                if(f==45)Capture("04-walk-right",cam);if(f==105)Capture("05-walk-left",cam);
            }
            Check(seen.SetEquals(bp.walkSpriteSequence),"Level1 plays all walk frames with right/left movement");
            Check(travelled>.1f,"actual character movement executed");
            Check(player.TryDash(),"existing dash still starts");yield return new WaitForSeconds(.04f);
            Check(bp.dashSpriteSequence.Contains(sr.sprite),"dash uses the updated character art");Capture("07-dash",cam);
            for(int f=0;f<8;f++){Capture("motion/dash-"+f.ToString("00"),cam);yield return null;}
            yield return new WaitForSeconds(.5f);
            player.Move(Vector2.zero);yield return new WaitForSeconds(.2f);Check(bp.idleSpriteSequence.Contains(sr.sprite),"dash returns to approved idle");
            var originalScale=sr.transform.lossyScale;
            Check(player.Skills.TryActivate(),"Hyuki active skill starts");
            yield return new WaitForSeconds(.8f);Capture("08-active",cam);
            Check(bp.idleSpriteSequence.Contains(sr.sprite)&&sr.transform.lossyScale==originalScale,"active VFX preserves current body and scale");
            yield return new WaitForSeconds(2.5f);
            var trap=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Monsters/Trap Monster.prefab")).GetComponent<TrapMonster>();trap.enabled=false;
            var bind=PlayerTrapBindRuntime.GetOrCreate(player);Check(bind.TryBind(trap,player.transform.position),"trap binds actor");
            yield return new WaitForSeconds(.12f);Capture("09-captured",cam);
            Check(bp.capturedSpriteSequence.Contains(sr.sprite)&&sr.transform.lossyScale==originalScale,"captured body uses new art at unchanged scale");
            bind.Release(trap);Object.Destroy(trap.gameObject);player.Move(Vector2.zero);yield return null;
            var travel=BloodClotTravel.Ensure(player);var portal=new GameObject("QA travel mouth");portal.transform.position=player.transform.position+Vector3.right*.8f;
            var runner=Object.FindObjectOfType<HyukiMotionQAHost>();
            runner.StartCoroutine(travel.Dive(portal.transform));
            yield return new WaitForSeconds(1.4f);Capture("10-travel-curl",cam);
            Check(Mathf.Abs(travel.Visual.transform.lossyScale.y-originalScale.y)<.0001f,"travel retains body scale instead of fitting transparent rectangle");
            Check(travel.Visual.sprite.name.StartsWith("HyukiApproved"),"travel uses updated tucked pose");
            yield return new WaitForSeconds(.5f);
            runner.StartCoroutine(travel.Eject(portal.transform,portal.transform.position+Vector3.left*.8f));
            yield return new WaitForSeconds(.35f);Capture("11-travel-eject",cam);
            yield return new WaitForSeconds(1.6f);travel.ReleasePose();Object.Destroy(portal);yield return null;
            Check(bp.idleSpriteSequence.Contains(sr.sprite)&&sr.transform.lossyScale==originalScale,"travel returns to approved idle without scale changes");
            cam.orthographicSize=zoom;ui=ApothecaryUI.Instance;ui.OpenRunBook();yield return null;Capture("06-runbook",cam);ui.CloseRunBook();
            cam.orthographicSize=zoom*.4f;yield return null;
            var exit=player.gameObject.AddComponent<CharacterExitSequence>();runner.StartCoroutine(exit.Play(player,sr));
            yield return new WaitForSecondsRealtime(.6f);Capture("12-exit-start",cam);
            Check(Mathf.Abs(exit.BodyHeight-VisibleBodyGeometry.Bounds(bp.idleSpriteSequence[0]).height*Mathf.Abs(originalScale.y))<.001f,"exit calibration ignores transparent padding and breath VFX");
            int lastExit=-1;
            while(!exit.Completed){if(exit.FrameIndex!=lastExit){lastExit=exit.FrameIndex;Capture("motion/exit-"+lastExit.ToString("00"),cam);}yield return null;}
            Capture("13-exit-frozen",cam);Check(exit.Visual.sprite.texture.width==2048,"frozen result uses new character atlas");
            Time.captureDeltaTime=0;SessionState.SetBool(Key+"Done",true);
        }
        static void Capture(string name,Camera cam)
        {
            if(cam==null)cam=Object.FindObjectOfType<Camera>();
            var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cams=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
            var old=cam.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);Texture2D tex=null;
            try
            {
                cam.targetTexture=rt;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=cam.nearClipPlane+.1f;}
                Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());
            }
            finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cams[i];canvases[i].planeDistance=distances[i];}cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(tex!=null)Object.Destroy(tex);}
        }
    }
    public sealed class HyukiMotionQAHost:MonoBehaviour{}
}
