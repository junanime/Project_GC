using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Vampire.Editor
{
    public sealed class StatusVfxSmokeHost:MonoBehaviour
    {
        public SpriteRenderer TintTarget,Tint;
        void LateUpdate(){if(Tint!=null)StatusTintCycle.Submit(TintTarget,Tint,StatusTintKind.Fire);}
    }
    [InitializeOnLoad]
    public static class StatusVfxSmoke
    {
        const string Key="StatusVfxSmoke";
        const string Out="Library/StatusVfxSmoke";
        static bool started;
        static double deadline;
        static int checks;
        static StatusVfxSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Out);SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Done",false)||SessionState.GetBool(Key+"Failed",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("STATUS_VFX_FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            if(EditorApplication.isPlaying&&!started){started=true;new GameObject("Status audit host").AddComponent<StatusVfxSmokeHost>().StartCoroutine(Checks());}
        }
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Debug.Log("STATUS_CHECK "+label);}
        static void Invoke(object o,string method){o.GetType().GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(o,null);}
        static SyringeSpecialRuntime Settings()=>new SyringeSpecialRuntime{
            poisonEnabled=true,poisonDuration=12,poisonTickInterval=1,poisonTickDamage=0,
            honeyEnabled=true,honeyDuration=3,honeySlowMultiplier=.8f,
            corrosionEnabled=true,corrosionDuration=12,corrosionDamageTakenBonusPerStack=.15f,corrosionBossDamageTakenBonusPerStack=.05f,corrosionMaxStacks=3,
            gutBacteriaEnabled=true,gutBacteriaStackDuration=12,gutBacteriaRequiredStacks=3,gutBacteriaMaxStacks=5
        };
        static IEnumerator Checks()
        {
            Application.runInBackground=true;Time.timeScale=1;
            var original=GamePreferences.Current.Copy();var prefs=original.Copy();prefs.pauseOnFocusLoss=false;prefs.augmentVfxOpacity=1;GamePreferences.Apply(prefs,false);
            var camera=new GameObject("Audit camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.35f;camera.transform.position=new Vector3(0,.1f,-10);camera.backgroundColor=new Color(.97f,.87f,.88f);camera.clearFlags=CameraClearFlags.SolidColor;
            var body=new GameObject("Normal monster art").AddComponent<SpriteRenderer>();body.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/SugarCubeAnimations/Single/Single_Walk_01.png");
            body.transform.localScale=Vector3.one*(1.05f/body.sprite.bounds.size.y);body.transform.position=-body.bounds.center;
            var poison=SyringeAugmentVfx.Play("Poison",body.transform.position,body);
            Check(poison.GetComponent<PoisonNeedleVisual>()!=null&&!poison.GetComponent<SpriteRenderer>().enabled,"poison replaces legacy aura");
            yield return null;yield return null;Capture(camera,"01-poison");
            var tint=poison.GetComponentsInChildren<SpriteRenderer>().Single(r=>r!=poison.GetComponent<SpriteRenderer>());
            body.flipX=true;body.transform.position+=Vector3.right*.25f;body.transform.rotation=Quaternion.Euler(0,0,12);
            yield return null;yield return null;
            Check(tint.sprite==body.sprite&&tint.flipX&&Vector3.Distance(tint.transform.position,body.transform.position)<.001f&&Quaternion.Angle(tint.transform.rotation,body.transform.rotation)<.01f,"poison follows sprite flip rotation and movement");
            prefs.augmentVfxOpacity=0;GamePreferences.Apply(prefs,false);yield return null;yield return null;
            var block=new MaterialPropertyBlock();tint.GetPropertyBlock(block);
            Check(block.GetFloat("_AugmentOpacity")==0&&body.color==Color.white&&body.GetComponent<AugmentVfxTag>()==null,"opacity hides effect without hiding enemy");
            prefs.augmentVfxOpacity=1;GamePreferences.Apply(prefs,false);
            body.transform.rotation=Quaternion.identity;body.transform.position=Vector3.zero;body.flipX=false;
            poison.Release();Check(!poison.gameObject.activeSelf,"poison cleanup returns lease");
            poison=SyringeAugmentVfx.Play("Poison",body.transform.position,body);Check(poison.GetComponentsInChildren<LineRenderer>().Length==3,"pooled poison retains exactly three bubbles");
            poison.Release();
            var other=new GameObject("Second live target").AddComponent<SpriteRenderer>();other.sprite=body.sprite;other.transform.position=Vector3.right*10;
            poison=SyringeAugmentVfx.Play("Poison",other.transform.position,other);yield return null;yield return null;
            Check(poison.GetComponentsInChildren<SpriteRenderer>().Any(r=>r!=poison.GetComponent<SpriteRenderer>()&&r.enabled),"reused lease is not hidden by previous living target color cycle");
            poison.Release();Object.Destroy(other.gameObject);poison=SyringeAugmentVfx.Play("Poison",body.transform.position,body);
            var corrosion=SyringeAugmentVfx.Play("CorrosionNeedle",body.transform.position,body);
            var ice=body.gameObject.AddComponent<IceChillVisual>();ice.ForceFor(8);
            var fire=new GameObject("Test fire tint").AddComponent<SpriteRenderer>();fire.transform.SetParent(body.transform,false);fire.sprite=body.sprite;fire.color=new Color(1,.62f,.28f,.25f);fire.sharedMaterial=new Material(Resources.Load<Shader>("AugmentVfx/PoisonTint"));fire.sortingOrder=1;AugmentVfxTag.Mark(fire.gameObject);
            var observed=new HashSet<int>();
            var host=Object.FindObjectOfType<StatusVfxSmokeHost>();host.TintTarget=body;host.Tint=fire;
            for(int f=0;f<36;f++)
            {
                // Real fire uses the same submission path, exercised on all bosses below.
                StatusTintCycle.Submit(body,fire,StatusTintKind.Fire);
                yield return null;var cycle=body.GetComponent<StatusTintCycle>();observed.Add(cycle.Selected);
                var colors=Object.FindObjectsOfType<SpriteRenderer>().Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Vampire/PoisonBodyTint"&&r.enabled).ToArray();
                Check(colors.Length==1,"one simultaneous body tint frame "+f);
                if(f%6==0)Capture(camera,"cycle-"+f.ToString("00"));
                yield return new WaitForSeconds(.1f);
            }
            Check(observed.Count==4,"poison corrosion ice and fire all receive turns");
            Object.Destroy(fire.sharedMaterial);Object.Destroy(fire.gameObject);ice.enabled=false;corrosion.Release();
            yield return null;yield return null;Check(body.GetComponent<StatusTintCycle>().Selected==(int)StatusTintKind.Poison,"expired colors removed immediately");
            poison.Release();var bacteria=SyringeAugmentVfx.Play("GutBacteriaNeedle",body.transform.position,body);yield return null;yield return null;Capture(camera,"02-bacteria");
            Check(bacteria.GetComponent<NeedleAttachmentVisual>()!=null&&bacteria.GetComponentsInChildren<LineRenderer>().Length==9,"three attached microbes with clean highlights");
            bacteria.Release();
            poison=SyringeAugmentVfx.Play("Poison",body.transform.position,body);
            var frozen=body.gameObject.AddComponent<NeuralBlockedMonsterStatus>();frozen.ApplyIce(1.8f);
            var frozenColors=new HashSet<int>();
            for(int i=0;i<16;i++){yield return new WaitForSeconds(.1f);frozenColors.Add(body.GetComponent<StatusTintCycle>().Selected);}
            Check(frozenColors.Contains((int)StatusTintKind.Poison)&&frozenColors.Contains((int)StatusTintKind.Ice),"fully frozen enemy alternates ice and poison without losing prison art");
            frozen.ReleaseIce();poison.Release();yield return null;yield return null;
            HoneyTrailEmitter.Touch(body);var honey=body.GetComponent<HoneyTrailEmitter>();honey.enabled=false;HoneyTrailEmitter.Touch(body);
            honey.Advance(1);Check(honey.Emitted==0,"stationary honey does not accumulate puddles");
            body.transform.position+=Vector3.right*.3f;honey.Advance(1);body.transform.position+=Vector3.right*.3f;honey.Advance(1);body.transform.position+=Vector3.right*.3f;honey.Advance(1);
            Check(honey.Emitted==2,"honey emits once per second and stops at three seconds");
            HoneyTrailEmitter.Touch(body);int before=honey.Emitted;
            for(int i=0;i<3;i++){body.transform.position+=Vector3.right*.3f;honey.Advance(1);}
            Check(honey.Emitted-before==3,"moving honey gives three stains over three seconds");
            camera.transform.position=new Vector3(body.bounds.center.x,.1f,-10);Capture(camera,"03-honey-trail");
            Check(Object.FindObjectsOfType<HoneyTrailStamp>().All(s=>s.GetComponentsInChildren<Collider2D>().Length==0),"honey stains never introduce damage or slow colliders");
            body.gameObject.SetActive(false);
            var progress=new OriginalAugmentProgress();progress.RegisterOwnedParent(SyringeSpecialAugmentAbility.SpecialAugmentType.FireNeedle.ToString());
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind)))
            {
                var boss=FoodBossRuntime.Spawn(kind,null,Vector2.zero);boss.AutoPatterns=false;boss.transform.position=Vector3.zero;
                FoodBossCombatStatus.Apply(boss,Settings(),null,false);
                var fireStatus=boss.gameObject.AddComponent<Ver4NeedleStatus>();fireStatus.ApplyFire(new SyringeSpecialRuntime{ver4=new Ver4CombatSnapshot(progress),ver4HitDamage=100},null,true);
                IceSkillRules.Freeze(boss);
                yield return null;yield return null;
                var target=boss.Visual.Body;
                Check(target.GetComponent<StatusTintCycle>()!=null&&target.GetComponent<HoneyTrailEmitter>()!=null,kind+" uses shared color cycle and honey trail");
                Check(Object.FindObjectsOfType<SyringeAugmentVfx>().Count(v=>v.Target==target)==3,kind+" has poison corrosion bacteria leases");
                Check(boss.GetComponent<IceChillVisual>().Visible||boss.GetComponent<ShiniBurnVisual>()!=null,kind+" ice and fire visuals connected");
                Check(Mathf.Abs(boss.GetComponent<CorrosionStatus>().GetDamageTakenMultiplier()-1.05f)<.001f,kind+" corrosion balance unchanged");
                camera.orthographicSize=3.4f;camera.transform.position=new Vector3(0,1.4f,-10);Capture(camera,"boss-"+(int)kind);
                boss.Visual.Charge=true;boss.Visual.Whipping=kind==FoodBossKind.Hurumi;
                yield return null;yield return null;Capture(camera,"boss-pose-"+(int)kind);
                var visibleTint=Object.FindObjectsOfType<SpriteRenderer>().Single(r=>r.enabled&&r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Vampire/PoisonBodyTint");
                visibleTint.GetPropertyBlock(block);
                Check(block.GetFloat("_BodyMask")== (kind==FoodBossKind.Tangdengi?1:kind==FoodBossKind.Hurumi?2:0),kind+" tint preserves bespoke body clipping");
                typeof(FoodBossRuntime).GetProperty("PhaseTwo").SetValue(boss,true);yield return null;yield return null;
                visibleTint=Object.FindObjectsOfType<SpriteRenderer>().Single(r=>r.enabled&&r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Vampire/PoisonBodyTint");
                Check(visibleTint.sprite==target.sprite,kind+" phase two pose and tint sprites match");Capture(camera,"boss-phase2-"+(int)kind);
                Object.Destroy(boss.gameObject);yield return null;yield return null;
                Check(!Object.FindObjectsOfType<SyringeAugmentVfx>().Any(v=>v.Target==target),kind+" releases all status leases on destruction");
            }
            yield return new WaitForSeconds(3.1f);Check(Object.FindObjectsOfType<HoneyTrailStamp>().Length==0,"all honey stains fade and return to pool");
            GamePreferences.Apply(original,false);Debug.Log("STATUS_VFX_CHECKS "+checks);SessionState.SetBool(Key+"Done",true);
        }
        static void Capture(Camera camera,string file)
        {
            var rt=RenderTexture.GetTemporary(512,512,24);var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var pixels=new Texture2D(512,512,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,512,512),0,0);pixels.Apply();File.WriteAllBytes(Out+"/"+file+".png",pixels.EncodeToPNG());
            Object.Destroy(pixels);camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        }
    }
}
