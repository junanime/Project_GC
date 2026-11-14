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
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    /// <summary>Actual camera proof plus navigation / tooth-light / jaw alignment regression.</summary>
    [InitializeOnLoad]
    public static class OralWalkabilitySmoke
    {
        const string Key="OralWalkabilitySmoke", Output="Library/OralWalkabilityProof";
        static bool started; static double deadline; static int checks;
        static readonly BindingFlags InstanceFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        [Serializable] sealed class Pref {public string key;public bool had;public int value;}
        [Serializable] sealed class Backup {public Pref[] prefs;}
        static OralWalkabilitySmoke(){if(SessionState.GetBool(Key,false))Attach();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SeedSelection()
        {
            if(!SessionState.GetBool(Key,false))return;
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);Restore();
            File.WriteAllText(Output+"/result.txt","RUNNING");File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            File.WriteAllText(Output+"/capture-method.txt","Actual Unity play-mode camera renders. Lighting before/after uses the same frame with only the new terrain material disabled/enabled. Full-map shot deliberately zoomed out; closeups preserve actual player and terrain rendering. Controlled event triggers and player positioning are used for reproducible QA. No image editing or interpolated frames.\n");
            var keys=new[]{"Coins","LobbySilverCoins"};
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{prefs=keys.Select(k=>new Pref{key=k,had=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.ClearPendingRunSceneTransfer();SeedSelection();
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            started=false;deadline=EditorApplication.timeSinceStartup+900;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);
        }
        static void Restore()
        {
            string path=Output+"/prefs.json";if(!File.Exists(path))return;
            foreach(var p in JsonUtility.FromJson<Backup>(File.ReadAllText(path)).prefs)if(p.had)PlayerPrefs.SetInt(p.key,p.value);else PlayerPrefs.DeleteKey(p.key);
            PlayerPrefs.Save();File.Delete(path);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline\n");SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();Time.timeScale=1;Time.captureFramerate=0;SessionState.SetBool(Key,false);
                bool failed=SessionState.GetBool(Key+"Failed",false);File.WriteAllText(Output+"/result.txt",(failed?"FAIL":"PASS")+" checks="+checks);
                EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying)return;
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            var level=Object.FindObjectOfType<LevelManager>();
            if(started||level==null||!level.InitialFieldObjectsReady||level.PlayerCharacter==null)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool ok,string message)
        {
            if(!ok)throw new Exception("ORAL_WALKABILITY_FAIL "+message);
            checks++;File.AppendAllText(Output+"/checks.txt","PASS "+message+"\n");Debug.Log("ORAL_WALKABILITY_PASS "+message);
        }
        static void Set(object owner,string name,object value)
        {
            for(var t=owner.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,InstanceFlags);if(f!=null){f.SetValue(owner,value);return;}}
            throw new MissingFieldException(owner.GetType().Name,name);
        }
        static void Silence(LevelManager level)
        {
            level.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
            foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
        }
        static void Clear(LevelManager level)
        {
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())if(m!=null)m.DespawnForStageTransfer();
            foreach(var p in Object.FindObjectsOfType<Projectile>())p.RemoveForItem();
            foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
        }
        static void Place(Character player,Vector2 position)
        {
            var runtime=OralStageRuntime.Instance;runtime.TeleportPlayer(position,OralMapGeometry.ZoneAt(runtime.Map,position)==OralZone.Teeth?OralZone.Teeth:OralZone.Tongue);
            player.Move(Vector2.zero);Physics2D.SyncTransforms();
        }
        static IEnumerator Checks(LevelManager level)
        {
            checks=0;var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            Application.runInBackground=true;Time.timeScale=1;RunScore.Exclude("Stage zero tooth light alignment QA");
            Silence(level);level.PlayerCharacter.CollectableCollider.enabled=false;
            Check(StageProgression.Ensure(level).BeginDirectOral(true),"actual Stage 0 entry begins");
            float until=Time.realtimeSinceStartup+120;
            while((OralStageRuntime.Instance==null||StageProgression.Ensure(level).Travelling)&&Time.realtimeSinceStartup<until)yield return null;
            var runtime=OralStageRuntime.Instance;var map=FiniteStomachMap.Instance;var events=OralFieldEvents.Instance;var player=level.PlayerCharacter;var visual=Object.FindObjectOfType<OralMapVisual>();
            Check(runtime!=null&&runtime.Ready&&visual!=null&&visual.Ready,"actual Stage 0 scene and visual ready");
            Silence(level);Clear(level);events.AutoSchedule=false;foreach(var cavity in events.Cavities){cavity.Healed=true;cavity.Active=false;}
            Set(player,"isInvincible",false);player.GainHealth(9999);Place(player,OralMapGeometry.TongueCenter(map));yield return null;
            var lighting=visual.ToothSurfaceLighting;
            Check(lighting!=null&&lighting.enabled&&lighting.sharedMaterial==visual.ToothSurfaceLightingMaterial,"single active terrain-light renderer uses owned material");
            Check(lighting.sharedMaterial.shader!=null&&lighting.sharedMaterial.shader.isSupported&&lighting.sharedMaterial.GetFloat("_Mode")==10,"supported dedicated soft tooth-light shader mode");
            Check(lighting.sortingLayerName==GroundVisualSorting.BackgroundLayerName&&lighting.sortingOrder>-920&&lighting.sortingOrder<-800,"tooth lighting is above painted map but behind upper jaw, actors and warnings");
            var mesh=lighting.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var uv2=mesh.uv2;
            Check(vertices.Length>100&&uv2.Length==vertices.Length&&mesh.triangles.Length>0,"terrain-light strip has complete mesh coordinates");
            bool finite=true,exact=true;int topVertices=0;
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];finite&=!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y);
                if(uv2[i].y<0||uv2[i].y>1)continue;
                topVertices++;exact&=Vector2.Distance(p,OralMapGeometry.ToothSurfacePoint(map,uv2[i].x,uv2[i].y))<.0001f;
            }
            Check(finite&&topVertices>100&&exact,"lighted crown vertices match exact navigable tooth cross sections");
            Check(mesh.triangles.All(i=>i>=0&&i<vertices.Length),"terrain-light triangles have valid indices");
            float overviewSize=Mathf.Max(map.WorldRect.height*.5f,map.WorldRect.width*.5f/(1440f/1000))*1.01f;
            lighting.enabled=false;var before=Frame("01-full-map-before",map.WorldRect.center,overviewSize,1440,1000,false);
            lighting.enabled=true;var after=Frame("02-full-map-final",map.WorldRect.center,overviewSize,1440,1000,false);
            int changed=0;for(int i=0;i<before.Length;i++)if(ColorDifference(before[i],after[i])>3)changed++;
            Check(changed>1000&&changed<before.Length/3,"new shading visibly changes teeth without recoloring whole map");
            // Same frame, fixed camera: the tongue center is deliberately outside the light mesh.
            var tonguePixel=Project(OralMapGeometry.TongueCenter(map),map.WorldRect.center,overviewSize,1440,1000);bool tongueUntouched=true;
            for(int dy=-10;dy<=10;dy++)for(int dx=-10;dx<=10;dx++){int at=(tonguePixel.y+dy)*1440+tonguePixel.x+dx;tongueUntouched&=ColorDifference(before[at],after[at])==0;}
            Check(tongueUntouched,"tongue pixels remain identical when only tooth readability is enabled");
            for(int i=0;i<14;i++)
            {
                var center=OralMapGeometry.ToothWalkCenter(map,i);
                Check(OralMapGeometry.ZoneAt(map,center)==OralZone.Teeth&&OralMapGeometry.IsJawDanger(map,center),"lit crown center is both walkable and crushable "+i);
                Check(OralMapGeometry.CanJumpToTooth(i)==(i<4||i>=10),"only rear eight teeth accept a direct dash jump "+i);
            }
            for(int i=0;i<13;i++)
            {
                var gap=OralMapGeometry.ToothSafeGap(map,i);
                Check(OralMapGeometry.ZoneAt(map,gap)==OralZone.Teeth&&!events.JawThreatens(gap),"lightly connected seam stays walkable and safe "+i);
                Place(player,gap);yield return new WaitForFixedUpdate();
                Check(Vector2.Distance(player.Position,gap)<.12f,"normal player collision preserves narrow safe seam "+i);
            }
            Place(player,OralMapGeometry.ToothWalkCenter(map,1));yield return null;Frame("03-molar-player",player.Position,7.4f);
            Place(player,OralMapGeometry.ToothWalkCenter(map,6));yield return null;Frame("04-incisor-player",player.Position,7.4f);
            // Warning checks cover all individual teeth while real damage and escape remain intact.
            var crown=OralMapGeometry.ToothWalkCenter(map,1);var gapTarget=OralMapGeometry.ToothSafeGap(map,1);
            Place(player,crown);float hp=player.CurrentHealth;int previousRate=Time.captureFramerate;Time.captureFramerate=20;
            Check(events.ForceEvent(OralEventKind.Jaw),"jaw starts on newly lit crown");
            bool inspected=false,moved=false;until=Time.realtimeSinceStartup+20;
            while(events.CurrentEvent!=OralEventKind.None&&Time.realtimeSinceStartup<until)
            {
                yield return null;
                if(!inspected&&events.Elapsed>.55f)
                {
                    WarningChecks(visual,map);Frame("05-warning-molar",player.Position,7.4f);Frame("06-warning-full-map",map.WorldRect.center,overviewSize,1440,1000,false);inspected=true;
                }
                if(events.Elapsed>.35f){var delta=gapTarget-player.Position;player.Move(delta.normalized*Mathf.Clamp01(delta.magnitude/.6f));if(delta.magnitude<.12f){moved=true;player.Move(Vector2.zero);player.GetComponent<Rigidbody2D>().velocity=Vector2.zero;}}
            }
            player.Move(Vector2.zero);Time.captureFramerate=previousRate;
            Check(inspected&&events.CurrentEvent==OralEventKind.None,"fourteen updated warnings and jaw release execute");
            Check(moved,"real movement reaches lit safe seam; remaining distance="+Vector2.Distance(player.Position,gapTarget).ToString("F3",CultureInfo.InvariantCulture));
            Check(!events.JawThreatens(player.Position),"real escape ends outside jaw damage polygons");
            Check(Mathf.Approximately(player.CurrentHealth,hp),"real movement into lit seam avoids actual crush damage; health="+player.CurrentHealth+" original="+hp);
            Frame("07-safe-seam",player.Position,7.4f);
            Place(player,OralMapGeometry.ToothWalkCenter(map,6));Check(events.ForceEvent(OralEventKind.Jaw),"front ridge warning trial starts");
            yield return RecordJaw(visual,map,player,events);
            Check(player.IsAlive&&player.CurrentHealth<hp,"standing in narrow front warning still causes real damage");
            Check(visual.JawWarningCount==0&&visual.JawWarningFillCount==0,"all threat outlines and filled hatching disappear after closure");
            Set(player,"isInvincible",true);player.GainHealth(9999);
            yield return JumpAndSlide(level,runtime,map,player,events);
            var savedMesh=mesh;var savedMaterial=lighting.sharedMaterial;
            Check(StageProgression.Ensure(level).BeginOralToStomach(),"exit begins after tooth traversal and crush cycles");
            until=Time.realtimeSinceStartup+120;while(StageProgression.Ensure(level).Travelling&&Time.realtimeSinceStartup<until)yield return null;
            yield return null;
            Check(OralStageRuntime.Instance==null&&!map.IsOral&&lighting==null,"stage exit removes tooth lighting with oral runtime");
            Check(savedMesh==null&&savedMaterial==null,"stage exit releases generated tooth mesh and owned material");
            SessionState.SetBool(Key+"Done",true);
        }
        static void WarningChecks(OralMapVisual visual,FiniteStomachMap map)
        {
            Check(visual.JawWarningCount==14&&visual.JawWarningFillCount==14,"14 separate outlines and filled warnings appear");
            Check(visual.JawWarningFillOpacity>=.219f&&visual.JawWarningFillOpacity<=.321f,"RGB hatching remains translucent above tooth shading");
            for(int i=0;i<14;i++)
            {
                var polygon=OralMapGeometry.ToothDamagePolygon(map,i);var line=visual.JawWarningLines[i];var fill=visual.JawWarningFills[i];var mesh=fill.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
                Check(line.loop&&line.positionCount==polygon.Length&&Enumerable.Range(0,polygon.Length).All(p=>Vector2.Distance(line.GetPosition(p),polygon[p])<.001f),"outline exactly matches damage boundary "+i);
                Check(vertices.Length==polygon.Length&&vertices.Select((p,j)=>Vector2.Distance(p,polygon[j])<.001f).All(x=>x),"filled hatch exactly matches same tooth damage boundary "+i);
                bool gapClear=true;
                for(int g=0;g<13;g++)for(int t=0;t<triangles.Length;t+=3)gapClear&=!InTriangle(OralMapGeometry.ToothSafeGap(map,g),vertices[triangles[t]],vertices[triangles[t+1]],vertices[triangles[t+2]]);
                Check(gapClear,"warning triangles exclude all safe seams "+i);
            }
        }
        static IEnumerator RecordJaw(OralMapVisual visual,FiniteStomachMap map,Character player,OralFieldEvents events)
        {
            string dir=Output+"/jaw-closure";Directory.CreateDirectory(dir);File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");
            int oldRate=Time.captureFramerate;Time.captureFramerate=15;int n=0;float start=Time.time,until=Time.realtimeSinceStartup+60;bool snapped=false;
            do
            {
                yield return null;
                Frame("jaw-closure/"+n.ToString("D4"),player.Position,7.4f,960,540);
                File.AppendAllText(dir+"/times.csv",n+","+(Time.time-start).ToString("F4",CultureInfo.InvariantCulture)+"\n");n++;
                if(!snapped&&events.Elapsed>.65f){Frame("08-warning-incisor",player.Position,7.4f);snapped=true;}
            }while((events.CurrentEvent!=OralEventKind.None||JawCrushVisual.ForActor(player)!=null)&&Time.realtimeSinceStartup<until);
            Time.captureFramerate=oldRate;Check(events.CurrentEvent==OralEventKind.None&&snapped,"front warning and closure clip completes");
        }
        static IEnumerator JumpAndSlide(LevelManager level,OralStageRuntime runtime,FiniteStomachMap map,Character player,OralFieldEvents events)
        {
            var center=OralMapGeometry.TongueCenter(map);var radii=OralMapGeometry.TongueRadii(map);
            var front=OralMapGeometry.Clamp(map,center+Vector2.up*radii.y,.4f,OralZone.Tongue);
            Check(!OralMapGeometry.TryJump(map,front,Vector2.up,out _),"tongue direct jump to front ridge remains blocked");
            Vector2 from=default,direction=default,landing=default;bool found=false;
            for(int j=0;j<360&&!found;j++)
            {
                float a=j*Mathf.Deg2Rad;var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                for(float s=.8f;s<1.02f&&!found;s+=.01f){var p=center+Vector2.Scale(dir,radii)*s;if(OralMapGeometry.ZoneAt(map,p)==OralZone.Tongue&&OralMapGeometry.TryJump(map,p,dir,out var end)){from=p;landing=end;direction=dir;found=true;}}
            }
            Check(found,"tongue edge retains reachable rear tooth departure");Place(player,from);Set(player,"lookDirection",direction);Set(player,"currentDashCharges",1);
            Check(player.TryDash()&&runtime.IsJumping,"actual dash enters tooth jump");float until=Time.realtimeSinceStartup+6;
            while(runtime.IsJumping&&Time.realtimeSinceStartup<until)yield return null;
            Check(!runtime.IsJumping&&OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Teeth&&OralMapGeometry.CanJumpToTooth(OralMapGeometry.NearestTooth(map,player.Position)),"actual dash lands on highlighted rear tooth only");
            Frame("09-jump-landing",player.Position,7.4f);
            Place(player,OralMapGeometry.ToothWalkCenter(map,4));var origin=player.Position;var inward=(center-origin).normalized;bool slide=false,hop=false;until=Time.realtimeSinceStartup+12;
            while(Time.realtimeSinceStartup<until)
            {
                if(!slide)player.Move(inward);else player.Move(Vector2.zero);
                if(runtime.IsSliding){slide=true;if(runtime.SlideVisualHeight>.55f&&!hop){hop=true;Frame("10-slide-hop",player.Position,7.4f);}}
                else if(slide)break;
                yield return null;
            }
            player.Move(Vector2.zero);
            Check(slide&&hop&&!runtime.IsSliding&&runtime.SlideVisualHeight==0,"walking over shaded inner canine slope slides then visibly hops");
            Check(OralMapGeometry.ZoneAt(map,player.Position)==OralZone.Tongue,"slope return lands on original tongue safely");Frame("11-slide-landing",player.Position,7.4f);
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c){float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);return (x>=0&&y>=0&&z>=0)||(x<=0&&y<=0&&z<=0);}
        static int ColorDifference(Color32 a,Color32 b)=>Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
        static Vector2Int Project(Vector2 p,Vector2 center,float size,int width,int height)=>new Vector2Int(Mathf.RoundToInt((p.x-center.x)/(size*2*width/height)*width+width*.5f),Mathf.RoundToInt((p.y-center.y)/(size*2)*height+height*.5f));
        static Color32[] Frame(string name,Vector2 focus,float size,int width=1280,int height=720,bool hud=true)
        {
            var camera=Camera.main;if(camera==null)throw new Exception("Capture camera missing");
            var roots=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=roots.Select(c=>c.worldCamera).ToArray();var distances=roots.Select(c=>c.planeDistance).ToArray();var orders=roots.Select(c=>c.sortingOrder).ToArray();var flags=roots.Select(c=>c.overrideSorting).ToArray();var enabled=roots.Select(c=>c.enabled).ToArray();
            var target=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;float zoom=camera.orthographicSize,aspect=camera.aspect;
            var rt=RenderTexture.GetTemporary(width,height,24);Texture2D image=null;
            try
            {
                camera.transform.position=new Vector3(focus.x,focus.y,position.z);camera.orthographicSize=size;camera.aspect=width/(float)height;
                foreach(var bars in Object.FindObjectsOfType<PlayerCombatBars>())typeof(PlayerCombatBars).GetMethod("LateUpdate",InstanceFlags).Invoke(bars,null);
                camera.targetTexture=rt;
                foreach(var c in roots){if(!hud){c.enabled=false;continue;}c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();
            }
            finally
            {
                for(int i=0;i<roots.Length;i++){roots[i].renderMode=RenderMode.ScreenSpaceOverlay;roots[i].worldCamera=cameras[i];roots[i].planeDistance=distances[i];roots[i].sortingOrder=orders[i];roots[i].overrideSorting=flags[i];roots[i].enabled=enabled[i];}
                camera.targetTexture=target;camera.transform.position=position;camera.orthographicSize=zoom;camera.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
