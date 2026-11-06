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
    // Observe real runtime submission/selection after every tint provider and StatusTintCycle.
    // WaitForEndOfFrame is not reliable for batch-mode test execution.
    [DefaultExecutionOrder(31000)]
    public sealed class ColonBossTintCaptureProbe : MonoBehaviour
    {
        public Action AfterTint;
        public Exception Failure;
        void LateUpdate()
        {
            if(Failure!=null)return;
            try{AfterTint?.Invoke();}catch(Exception e){Failure=e;}
        }
    }
    [InitializeOnLoad]
    public static class ColonBossPresentationCapture
    {
        const string Key="ColonBossPresentationCapture";
        static bool IdentityReview => Environment.GetCommandLineArgs().Any(a=>a=="-colonIdentityReview=1");
        static string Output => IdentityReview?"Library/ColonBossIdentityReview":"Library/ColonBossPresentation";
        static bool started;static double deadline;
        static ColonBossPresentationCapture(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/errors.txt","");File.WriteAllText(Output+"/result.txt","RUNNING");
            if(IdentityReview){ColonBossArtIdentityChecks.Validate(Output);File.WriteAllText(Output+"/identity-runtime-checks.txt","");}
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
                if(IdentityReview&&kind!=ColonBossKind.Hedgehog&&kind!=ColonBossKind.Manatee)continue;
                if(!string.IsNullOrEmpty(only)&&!string.Equals(kind.ToString(),only,StringComparison.OrdinalIgnoreCase))continue;
                var home=director.Habitats.First(h=>h.Kind==kind);var at=(Vector2)home.transform.position;
                var front=kind==ColonBossKind.Manatee?home.Doors[1]+home.WallNormal*1.5f:at+Vector2.down*2;
                ColonContentSmoke.Position(player,map.ClampToField(front,map.WallWidth+.3f));yield return new WaitForSeconds(1.2f);
                // Overview is a clearly labelled inspection view; subsequent clips use the live gameplay camera.
                var cam=Camera.main;var cp=cam.transform.position;float zoom=cam.orthographicSize;cam.orthographicSize=kind==ColonBossKind.Manatee?6:4.5f;cam.transform.position=new Vector3(at.x,at.y+1.4f,cp.z);Frame(kind+"-home");cam.transform.position=cp;cam.orthographicSize=zoom;
                InteractionFocus.SelectTouched(home);home.TryInteract();yield return null;
                var boss=ColonBossRuntime.Active.FirstOrDefault(b=>b.Kind==kind&&!b.Dead);if(boss==null)throw new Exception("Habitat did not activate: "+kind);boss.AutoPatterns=false;
                yield return Clip(kind+"-arrival",()=>boss.Arriving,12,boss);
                ColonContentSmoke.Position(player,map.ClampToField((Vector2)boss.transform.position+Vector2.right*5.5f+Vector2.down*.5f,map.WallWidth+.4f));yield return new WaitForSeconds(IdentityReview ? .25f : 1.4f);
                float start=Time.time;
                if(IdentityReview)
                {
                    Vector2 walkDirection=Vector2.right;
                    // Move the observer, not the boss rig: otherwise it reaches the 3.8-unit
                    // stop radius halfway through the clip and records idle instead of walking.
                    float lead=kind==ColonBossKind.Manatee?4.4f:6.5f;
                    yield return Clip(kind+"-walk",()=>Time.time-start<3.6f,5,boss,()=>ColonContentSmoke.Position(player,map.ClampToField((Vector2)boss.transform.position+walkDirection*lead,map.WallWidth+.4f)));
                }
                else yield return Clip(kind+"-walk",()=>Time.time-start<2.7f,4,boss);
                ColonContentSmoke.Position(player,map.ClampToField((Vector2)boss.transform.position+Vector2.right*3.5f+Vector2.down*.4f,map.WallWidth+.4f));yield return new WaitForSeconds(.6f);
                if(IdentityReview)
                {
                    // Inspect every authored action cell before adding live VFX. Both directions
                    // expose mirrored appendages and anchors that can be hidden in one screenshot.
                    for(int pose=0;pose<8;pose++)
                    {
                        boss.Visual.ResetPose();boss.Visual.Pose=pose;yield return new WaitForSeconds(.25f);
                        Frame(kind+"-pose-"+pose.ToString("D2")+"-right");
                        boss.Visual.Face(Vector2.left);
                        typeof(ColonBossVisual).GetMethod("Sample",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(boss.Visual,null);
                        Frame(kind+"-pose-"+pose.ToString("D2")+"-left");
                    }
                    boss.Visual.ResetPose();yield return new WaitForSeconds(.3f);
                    for(int pattern=0;pattern<boss.Definition.PatternCount;pattern++)
                    {
                        // Reposition the invulnerable observer so every pattern receives a real target.
                        Vector2 observer=kind==ColonBossKind.Manatee?home.Doors[1]+home.WallNormal*3:(Vector2)boss.transform.position+Vector2.right*3.2f+Vector2.down*.4f;
                        ColonContentSmoke.Position(player,map.ClampToField(observer,map.WallWidth+.4f));yield return new WaitForSeconds(.35f);
                        if(!boss.PlayPattern(pattern))throw new Exception("Cannot start identity review pattern: "+kind+"/"+pattern);
                        yield return Clip(kind+"-pattern-"+pattern.ToString("D2"),()=>boss.Busy,25,boss);
                        if(boss.Busy)throw new Exception("Identity review pattern did not complete: "+kind+"/"+pattern);
                        Frame(kind+"-pattern-"+pattern.ToString("D2")+"-recovered");yield return new WaitForSeconds(.35f);
                    }
                }
                else
                {
                    int pattern=kind==ColonBossKind.Flamingo?0:kind==ColonBossKind.Shark?3:kind==ColonBossKind.Pudding?3:kind==ColonBossKind.Hedgehog?1:kind==ColonBossKind.Turtle?3:4;
                    if(!boss.PlayPattern(pattern))throw new Exception("Cannot start presentation pattern: "+kind);
                    yield return Clip(kind+"-pattern",()=>boss.Busy,15,boss);
                }
                // Presentation cleanup must not award XP and open an augment menu between clips.
                // The separate smoke/defeat suites validate actual death and rewards.
                if(IdentityReview&&kind==ColonBossKind.Hedgehog)yield return CheckForegroundTint(boss,player);
                Frame(kind+"-ready");Object.Destroy(boss.gameObject);yield return new WaitForSeconds(.2f);
                Silence(level);foreach(var c in Object.FindObjectsOfType<Collectable>())c.DissolveInAcidLake();
            }
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator CheckForegroundTint(ColonBossRuntime boss,Character player)
        {
            // Run after every presentation clip so QA-only status cannot change the approved GIFs.
            bool wasBare=boss.Visual.Bare;boss.Visual.Bare=false;boss.Visual.ResetPose();boss.Visual.Pose=4;
            yield return new WaitForSeconds(.25f);
            var body=boss.Visual.Main;
            if(boss.Visual.ForegroundSortingOrder<=body.sortingOrder)throw new Exception("Hedgehog foreground attachment is missing");
            var foreground=body.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r=>r!=body&&r.enabled&&r.sortingOrder==boss.Visual.ForegroundSortingOrder);
            if(foreground==null||foreground.sprite==null)throw new Exception("Foreground tint QA did not expose the authored spine cover");
            FoodBossCombatStatus.Apply(boss,new SyringeSpecialRuntime{poisonEnabled=true,poisonDuration=2.5f,poisonTickInterval=.2f,poisonTickDamage=1,corrosionEnabled=true,corrosionDuration=2.5f,corrosionMaxStacks=3,corrosionBossDamageTakenBonusPerStack=.05f},player,false);
            if(boss.GetComponent<CorrosionStatus>()?.CurrentStacks!=1)throw new Exception("Foreground tint QA did not apply real corrosion");
            float health=boss.Health,until=Time.time+2.4f;bool sawPoison=false,sawCorrosion=false;
            var probe=body.gameObject.AddComponent<ColonBossTintCaptureProbe>();
            probe.AfterTint=()=>
            {
                var cycle=body.GetComponent<StatusTintCycle>();if(cycle==null)return;
                var overlays=(SpriteRenderer[])typeof(StatusTintCycle).GetField("overlays",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cycle);
                var submittedFrames=(int[])typeof(StatusTintCycle).GetField("frames",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cycle);
                int selected=cycle.Selected;if(selected<0||selected>1)return;
                if(submittedFrames[selected]!=Time.frameCount)throw new Exception("Tint QA must observe actual current-frame status submission");
                var active=overlays[selected];
                if(active==null||!active.enabled||active.sortingLayerID!=body.sortingLayerID||active.sortingOrder<=boss.Visual.ForegroundSortingOrder)
                    throw new Exception("Active status tint must be above the hedgehog spine cover");
                if(overlays.Count(r=>r!=null&&r.enabled)!=1)throw new Exception("Poison and corrosion must alternate without additive overlap");
                bool first=selected==0?!sawPoison:!sawCorrosion;
                if(selected==0)sawPoison=true;else sawCorrosion=true;
                if(first)
                {
                    string state=selected==0?"poison":"corrosion";
                    Frame("Hedgehog-status-"+state+"-foreground");
                    File.AppendAllText(Output+"/identity-runtime-checks.txt","PASS "+state+" body="+body.sortingOrder+" foreground="+foreground.sortingOrder+" active-tint="+active.sortingOrder+"; exactly one active status; actual LateUpdate submission frame="+Time.frameCount+"\n");
                }
            };
            while(Time.time<until&&(!sawPoison||!sawCorrosion)&&probe.Failure==null)yield return null;
            probe.enabled=false;var failure=probe.Failure;Object.Destroy(probe);
            if(failure!=null)throw failure;
            if(!sawPoison||!sawCorrosion||boss.Health>=health)throw new Exception("Real poison damage and both foreground status colors must be observed");
            boss.Visual.Bare=wasBare;boss.Visual.ResetPose();yield return new WaitForSeconds(2.6f);
        }
        static IEnumerator Clip(string name,Func<bool> running,float limit,ColonBossRuntime boss=null,Action observerStep=null)
        {
            string dir=Output+"/"+name;Directory.CreateDirectory(dir);File.WriteAllText(dir+"/times.csv","frame,game_seconds\n");float start=Time.time;int frame=0;
            if(IdentityReview)File.WriteAllText(dir+"/geometry.csv","frame,texture,sprite,pivot_x,pivot_y,ppu,scale_x,scale_y,world_x,world_y,width,height,visible,masked\n");
            do{observerStep?.Invoke();yield return null;Frame(name+"/"+frame.ToString("D4"));File.AppendAllText(dir+"/times.csv",frame+","+(Time.time-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"\n");if(IdentityReview&&boss!=null)RecordGeometry(dir,frame,boss);frame++;yield return new WaitForSeconds(1f/12);}while(running()&&Time.time-start<limit);
        }
        static void RecordGeometry(string directory,int frame,ColonBossRuntime boss)
        {
            var body=boss.Visual.Main;var sprite=body.sprite;if(sprite==null)throw new Exception("Missing boss artwork during "+directory);
            string path=AssetDatabase.GetAssetPath(sprite);
            if(!path.StartsWith("Assets/Resources/ColonBosses/"+boss.Definition.Id+"/",StringComparison.Ordinal))throw new Exception("Wrong boss artwork identity: "+path);
            var s=body.transform.lossyScale;var p=body.transform.position;var bounds=body.bounds;
            if(float.IsNaN(s.x)||float.IsNaN(s.y)||float.IsInfinity(s.x)||float.IsInfinity(s.y))throw new Exception("Non-finite boss scale");
            string F(float value)=>value.ToString("0.#####",System.Globalization.CultureInfo.InvariantCulture);
            File.AppendAllText(directory+"/geometry.csv",string.Join(",",new[]{frame.ToString(),sprite.texture.name,sprite.name,F(sprite.pivot.x),F(sprite.pivot.y),F(sprite.pixelsPerUnit),F(s.x),F(s.y),F(p.x),F(p.y),F(bounds.size.x),F(bounds.size.y),body.enabled.ToString(),body.maskInteraction.ToString()})+"\n");
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
