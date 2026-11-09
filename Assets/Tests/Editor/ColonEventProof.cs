using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class ColonEventProof
    {
        const string Key="ColonEventProof",Output="D:/GameDev/Temp/colon-events-20261110/proof";
        static bool started;static double deadline;static string pending;static int frame;
        static ColonEventProof(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run(){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();}
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1200;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string t,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",t+"\n"+s+"\n");SessionState.SetBool(Key+"Failed",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false)){if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;bool fail=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);File.WriteAllText(Output+"/result.txt",fail?"FAIL":"PASS");EditorApplication.Exit(fail?1:0);return;}
            if(!EditorApplication.isPlaying)return;var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;started=true;level.StartCoroutine(Proof(level));
        }
        static void Clear(){foreach(var m in Object.FindObjectsOfType<Monster>())m.DespawnForStageTransfer();foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();}
        static Monster Spawn(LevelManager l,MonsterBlueprint bp,Vector2 at)
        {
            int pool=Array.FindIndex(l.CurrentLevelBlueprint.monsters,c=>c.monsterBlueprints.Contains(bp));
            if(pool<0)pool=Array.FindIndex(l.CurrentLevelBlueprint.monsters,c=>c.monsterBlueprints.Any(b=>ColonMonsterRoster.Active.Resolve(b)==bp));
            return l.EntityManager.SpawnMonster(pool,at,bp,0,false);
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);File.AppendAllText(Output+"/checks.txt",message+"\n");}
        static IEnumerator Proof(LevelManager level)
        {
            File.WriteAllText(Output+"/checks.txt","");var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Colon event runtime QA");level.enabled=false;
            var player=level.PlayerCharacter;ColonContentSmoke.Set(player,"isInvincible",true);ColonContentSmoke.Set(player,"nextLevelExp",1000f);
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            yield return ColonContentSmoke.Enter(level);Clear();
            foreach(var o in level.GetComponent<FieldObjectLayout>().SpawnedObjects)if(o!=null)o.SetActive(false);
            var events=ColonFieldEvents.Instance;Check(events!=null,"Colon event director installed on real stage transfer");events.AutoSchedule=false;
            var directors=Object.FindObjectsOfType<StageEventDirector>();
            Check(directors.Length>0 && directors.All(d=>new[]{"basicEvents","advancedEvents","antacidEvents"}.All(n=>((IList)ColonContentSmoke.Field(d,n).GetValue(d)).Count==0)),"Colon clears all stomach field-event schedules");
            Check(Object.FindObjectsOfType<AcidLakeEvent>().All(a=>!a.Active),"Stomach acid lake remains inactive in colon");
            var map=FiniteStomachMap.Instance;var at=map.ClampToFloor(map.UVToWorld(new Vector2(.74f,.70f)),6);
            for(int y=30;y<70;y++)for(int x=30;x<70;x++){var candidate=map.UVToWorld(new Vector2(x/100f,y/100f));float clearance=map.FloorClearance(candidate);if(clearance>2.5f&&clearance<3&&events.Flow.Direction(candidate).sqrMagnitude>.5f)at=candidate;}
            ColonContentSmoke.Position(player,at);player.Move(Vector2.zero);
            var cam=Camera.main;var oldz=cam.transform.position.z;Vector2 center=at;
            var capture=level.gameObject.AddComponent<StageCaptureProbe>();capture.AfterLateUpdate=()=>{cam.orthographicSize=4.5f;cam.transform.position=new Vector3(center.x,center.y,oldz);foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())bars.SendMessage("LateUpdate");if(pending!=null){string group=pending;pending=null;CaptureNow(group,frame);}};
            yield return null;
            Check(events.Begin(ColonEventKind.Expulsion),"Wind starts only in colon gameplay");float age=events.Age;level.SetRunFlowPaused(true);yield return new WaitForSeconds(.25f);Check(Mathf.Abs(events.Age-age)<.001f,"Event timer pauses with gameplay");level.SetRunFlowPaused(false);
            yield return new WaitForSeconds(4);var start=player.Position;yield return new WaitForSeconds(.4f);Check(Vector2.Distance(start,player.Position)>.05f,"Wind physically moves player");
            var against=events.Flow.Direction(player.Position);start=player.Position;player.Move(-against);yield return new WaitForSeconds(.7f);player.Move(Vector2.zero);Check(Vector2.Dot(player.Position-start,against)>0,"Opposing movement resists wind but still slips downstream");
            var probe=new GameObject("Wall validation");probe.transform.position=at;bool safe=true;
            for(int i=0;i<80;i++){var from=(Vector2)probe.transform.position;events.Push(probe.transform,events.Flow.Direction(from)*.3f,.25f);safe&=map.FloorClearance(probe.transform.position)>.2f;}Check(safe,"80 wind path steps stay off solid walls");Object.Destroy(probe);events.Cancel();
            ColonContentSmoke.Position(player,at);Check(events.Begin(ColonEventKind.Absorption),"Dry event begins");yield return new WaitForSeconds(4);
            Check(events.DryCenters.Count>0,"Dry patches occupy valid floor");ColonContentSmoke.Position(player,events.DryCenters[0]);yield return null;yield return null;Check(player.GetComponent<SnailSlowStatus>().Multiplier<.9f,"Dry patch slows player");
            ColonContentSmoke.Position(player,events.Origin);yield return null;yield return null;Check(player.GetComponent<SnailSlowStatus>().Multiplier>.99f,"Wet lane immediately restores movement");events.Cancel();
            ColonContentSmoke.Position(player,at);events.Begin(ColonEventKind.Residue);yield return new WaitForSeconds(3);Check(events.Residues.Count>0,"Residue event emits breakable debris");var debris=events.Residues[0];debris.TakeDamage(1);debris.TakeDamage(1);Check(events.Broken==1,"Debris breaks only once");events.Cancel();
            ColonContentSmoke.Position(player,at);events.Begin(ColonEventKind.Nutrients);yield return new WaitForSeconds(.6f);var nutrient=events.Nutrients[0];Check(nutrient.GetComponent<IDamageable>()==null,"Nutrients have no damageable target");
            ColonContentSmoke.Position(player,(Vector2)nutrient.transform.position+Vector2.down*.5f);yield return null;var xp=(float)ColonContentSmoke.Field(player,"currentExp").GetValue(player);
            var touch=player.GetComponent<WorldObjectTouchInput>()??player.gameObject.AddComponent<WorldObjectTouchInput>();var hit=cam.WorldToScreenPoint(nutrient.GetComponentInChildren<SpriteRenderer>().bounds.center);
            Check(touch.TryInteract(hit),"World-object touch selects nutrient through real input route");yield return null;yield return null;Check(nutrient.Collected&&events.Collected==1,"Touch interaction acquires nutrient");Check(!nutrient.Interact(),"Collected nutrient cannot be claimed twice");
            yield return new WaitForSeconds(.3f);Check((float)ColonContentSmoke.Field(player,"currentExp").GetValue(player)>xp,"Nutrient awards experience directly");events.Cancel();
            Time.captureFramerate=20;
            foreach(var kind in Environment.GetCommandLineArgs().Contains("-colonEventsSmokeOnly") ? new ColonEventKind[0] : new[]{ColonEventKind.Expulsion,ColonEventKind.Absorption,ColonEventKind.Residue,ColonEventKind.Nutrients})
            {
                Clear();events.Cancel();ColonContentSmoke.Position(player,at);player.Move(Vector2.zero);center=at;
                if(kind==ColonEventKind.Expulsion)for(int i=0;i<9;i++)Spawn(level,ColonMonsterRoster.Active.Normals[i%3].blueprint,map.ClampToFloor(at+new Vector2(-3+(i%3)*2,1+(i/3)*1.1f),.6f));
                events.Begin(kind);Vector2 direction=events.Flow.Direction(at);Vector2 across=new Vector2(-direction.y,direction.x);
                for(int i=0;i<230;i++)
                {
                    if(kind==ColonEventKind.Expulsion){player.Move(i<70?Vector2.zero:i<155?-direction:across*.6f);center=Vector2.Lerp(at,player.Position,.6f);}
                    if(kind==ColonEventKind.Absorption){var target=i<55?at:i<130?events.DryCenters.OrderBy(p=>Vector2.Distance(p,at)).First():at;var d=target-player.Position;player.Move(d.magnitude>.15f?d.normalized:Vector2.zero);}
                    if(kind==ColonEventKind.Residue){player.Move(i<65?Vector2.zero:Mathf.Sin(i*.04f)*across*.55f);if(i>90&&i%25==0){var r=events.Residues.FirstOrDefault(d=>d!=null&&d.gameObject.activeSelf&&!d.Broken&&Vector2.Distance(d.transform.position,player.Position)<4);if(r!=null)r.TakeDamage(1);}}
                    if(kind==ColonEventKind.Nutrients){var target=events.Nutrients.FirstOrDefault(n=>!n.Collected);if(target!=null&&i>35){var d=(Vector2)target.transform.position-player.Position;player.Move(d.magnitude>.55f?d.normalized:Vector2.zero);if(d.magnitude<.65f&&i%30==0){InteractionFocus.SelectTouched(target);MobileGameplayInput.RequestInteraction();}}else player.Move(Vector2.zero);}
                    center=Vector2.Lerp(at,player.Position,.65f);
                    pending=kind.ToString();frame=i;yield return null;while(pending!=null)yield return null;
                }
                player.Move(Vector2.zero);events.Cancel();yield return null;
            }
            Time.captureFramerate=0;Check(events.Nutrients.Count==0&&events.Residues.Count==0,"Cleanup removes all temporary pickups and debris");SessionState.SetBool(Key+"Done",true);
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
                Canvas.ForceUpdateCanvases();
                // Overlay screen coordinates change when recording to a differently sized texture.
                // Reproject the existing meters rather than letting the review camera offset them.
                foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())
                {
                    var owner=(Character)ColonContentSmoke.Field(bars,"owner").GetValue(bars);
                    var canvas=(Canvas)ColonContentSmoke.Field(bars,"canvas").GetValue(bars);
                    var size=((RectTransform)canvas.transform).rect.size;
                    var head=(Vector3)ColonContentSmoke.Field(bars,"headLocal").GetValue(bars);
                    var feet=(Vector3)ColonContentSmoke.Field(bars,"feetLocal").GetValue(bars);
                    Vector2 h=camera.WorldToViewportPoint(owner.transform.TransformPoint(head));
                    Vector2 f=camera.WorldToViewportPoint(owner.transform.TransformPoint(feet));
                    bars.HealthRect.anchoredPosition=Vector2.Scale(h-Vector2.one*.5f,size)+Vector2.up*11;
                    bars.DashRect.anchoredPosition=Vector2.Scale(f-Vector2.one*.5f,size)-Vector2.up*12;
                    bars.ActiveRect.anchoredPosition=bars.DashRect.anchoredPosition-Vector2.up*15;
                }
                Canvas.ForceUpdateCanvases();camera.Render();ui.Render();RenderTexture.active=rt;pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+index.ToString("D4")+".png",pixels.EncodeToPNG());
            }
            finally{for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];}for(int i=0;i<nodes.Length;i++)nodes[i].gameObject.layer=layers[i];camera.cullingMask=mask;camera.targetTexture=oldTarget;ui.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);Object.Destroy(uiGo);Canvas.ForceUpdateCanvases();}

        }


    }
}
