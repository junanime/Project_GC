using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ColonVisualProof
    {
        const string Key="ColonVisualProof",Output="D:/Project_GC/work/colon-visual-proof";
        static bool started;static double deadline;static string pending;static int frame;
        static ColonVisualProof(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();}
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+900;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string t,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",t+"\n"+s+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false)){if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;bool fail=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",fail?"FAIL":"PASS");EditorApplication.Exit(fail?1:0);return;}
            if(!EditorApplication.isPlaying)return;var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;started=true;level.StartCoroutine(Proof(level));
        }
        static void Clear(){foreach(var m in Object.FindObjectsOfType<Monster>())m.DespawnForStageTransfer();foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();}
        static Monster Spawn(LevelManager l,MonsterBlueprint bp,Vector2 at,bool original=false)
        {
            int pool=Array.FindIndex(l.CurrentLevelBlueprint.monsters,c=>c.monsterBlueprints.Contains(bp));
            if(pool<0)pool=Array.FindIndex(l.CurrentLevelBlueprint.monsters,c=>c.monsterBlueprints.Any(b=>ColonMonsterRoster.Active.Resolve(b)==bp));
            var monster=l.EntityManager.SpawnMonster(pool,at,bp,0,original);
            // A display lineup intentionally overrides the sniper/trap's gameplay spawn offset.
            Place(monster,at);return monster;
        }
        static void Place(Monster monster,Vector2 at)
        {monster.transform.position=at;var rb=monster.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=at;Physics2D.SyncTransforms();}
        static void Lineup(Monster monster,float groundY)
        {
            monster.SetFieldRuntimeSuspended(true);
            var seaweed=monster.GetComponent<SeaweedTrapVisual>();if(seaweed!=null)seaweed.ResetDormant();
            var body=monster.BodyRenderer;
            if(body!=null&&body.enabled)Place(monster,(Vector2)monster.transform.position+Vector2.up*(groundY-body.bounds.min.y));
        }
        static IEnumerator Proof(LevelManager level)
        {
            CheckTransparentMaterial();
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Colon visual QA");level.enabled=false;ColonContentSmoke.Set(level.PlayerCharacter,"isInvincible",true);
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            var original=level.CurrentLevelBlueprint;yield return ColonContentSmoke.Enter(level);Clear();
            foreach(var o in level.GetComponent<FieldObjectLayout>().SpawnedObjects)if(o!=null)o.SetActive(false);
            var map=FiniteStomachMap.Instance;var at=map.ClampToField(map.UVToWorld(new Vector2(.78f,.72f)),14);
            ColonContentSmoke.Position(level.PlayerCharacter,at+Vector2.down*1.5f);level.PlayerCharacter.Move(Vector2.zero);level.PlayerCharacter.CollectableCollider.enabled=false;
            var cam=Camera.main;cam.orthographicSize=3.8f;var oldz=cam.transform.position.z;
            var capture=level.gameObject.AddComponent<StageCaptureProbe>();capture.AfterLateUpdate=()=>{cam.transform.position=new Vector3(at.x,at.y,oldz);if(pending!=null){string group=pending;pending=null;CaptureNow(group,frame);}};
            var roster=ColonMonsterRoster.Active;
            foreach(string group in new[]{"ordinary","elite","special"})
            {
                Clear();var list=group=="ordinary"?roster.Normals.OrderBy(e=>e.height).ToList():group=="elite"?roster.Elites.OrderBy(e=>e.height).ToList():roster.Specials.OrderBy(e=>e.height).ToList();
                var types=list.Select(e=>e.blueprint).ToList();if(group=="special")types.AddRange(new[]{roster.Resolve(original.monsters.SelectMany(p=>p.monsterBlueprints).ToArray()[25]),roster.Resolve(original.monsters.SelectMany(p=>p.monsterBlueprints).ToArray()[24]),original.monsters.SelectMany(p=>p.monsterBlueprints).ToArray()[16]});
                float step=group=="elite"?1.12f:1.02f,start=-(types.Count)*step*.5f;
                var slime=Spawn(level,original.monsters[0].monsterBlueprints[0],at+new Vector2(start,.4f),true);Lineup(slime,at.y+.4f);
                for(int i=0;i<types.Count;i++){var m=Spawn(level,types[i],at+new Vector2(start+(i+1)*step,.4f));Lineup(m,at.y+.4f);}
                yield return new WaitForSeconds(.2f);pending=group;frame=0;yield return new WaitForSeconds(.15f);
            }
            // Real walking/casting art: no substituted image mockup.
            Clear();var moving=new List<Monster>();
            for(int i=0;i<roster.Specials.Count;i++){var m=Spawn(level,roster.Specials[i].blueprint,at+new Vector2(-3+i*1.2f,1.3f));moving.Add(m);}
            for(int i=0;i<48;i++){pending="special-motion";frame=i;yield return new WaitForSeconds(1f/12f);}
            Clear();
            for(int i=0;i<roster.Normals.Count;i++)Spawn(level,roster.Normals[i].blueprint,at+new Vector2(-3.5f+i,1.3f));
            for(int i=0;i<36;i++){pending="ordinary-motion";frame=i;yield return new WaitForSeconds(1f/12f);}
            Clear();
            var colony=Spawn(level,roster.Specials.First(e=>e.id=="S06").blueprint,at+Vector2.up);
            for(int i=0;i<48;i++)
            {
                if(i==10)colony.TakeDamage(99999);
                if(i==25)foreach(var child in level.EntityManager.LivingMonsters.ToArray())child.TakeDamage(99999);
                pending="split-motion";frame=i;yield return new WaitForSeconds(1f/12f);
            }
            yield return new WaitForSeconds(.2f);SessionState.SetBool(Key+"Done",true);
        }
        static void CaptureNow(string group,int index)
        {
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

        static void CheckTransparentMaterial()
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/MonsterRemake/SeaweedRear_00.png");
            var obj=new GameObject("Seaweed alpha regression");obj.layer=30;obj.transform.position=new Vector3(10000,10000,0);
            var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Dissolve.mat");
            var view=new GameObject("Alpha probe camera");var cam=view.AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=sprite.bounds.size.y*.5f;cam.aspect=1;
            cam.transform.position=new Vector3(10000,10000,-5);cam.nearClipPlane=.1f;cam.farClipPlane=10;cam.cullingMask=1<<30;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.08f,.16f,.26f,1);
            var rt=RenderTexture.GetTemporary(64,64,24);var prior=RenderTexture.active;var pixels=new Texture2D(64,64,TextureFormat.RGB24,false);
            try
            {
                cam.targetTexture=rt;sr.enabled=false;cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();var background=pixels.GetPixel(32,51);
                sr.enabled=true;cam.Render();pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();var clear=pixels.GetPixel(32,51);var leaf=pixels.GetPixel(32,5);
                float distance(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b);
                if(distance(clear,background)>.03f||distance(leaf,background)<.1f)throw new Exception("Dissolve material must preserve transparent seaweed pixels and display the leaf");
                File.WriteAllText(Output+"/alpha-check.txt","PASS: transparent PNG RGB does not leak; opaque leaf remains visible.");
            }
            finally{cam.targetTexture=null;RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);Object.Destroy(pixels);Object.Destroy(obj);Object.Destroy(view);}
        }

    }
}
