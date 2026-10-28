using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static partial class ItemProcProof
    {
        const string Key="ItemProcProof",Output="Library/ItemProcProof";
        static bool started;static double deadline;
        [Serializable] class Entry{public string id,name,description,mode;public bool retired;}
        [Serializable] class Manifest{public List<Entry> entries=new List<Entry>();}
        [Serializable] class TutorialEntry{public string id;}
        [Serializable] class Tutorials{public TutorialEntry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}
        [Serializable] class Backup{public Saved[] values;}
        static ItemProcProof(){if(SessionState.GetBool(Key,false))Attach();}
        public static void RunStaticChecks()
        {
            Directory.CreateDirectory(Output);
            try
            {
                foreach(var type in new[]{typeof(ItemBalancePatchTests),typeof(ItemFeedbackPatchTests)})
                {var suite=Activator.CreateInstance(type);foreach(var m in type.GetMethods().Where(m=>m.GetCustomAttribute<TestAttribute>()!=null))m.Invoke(suite,null);}
                GeometryChecks();File.WriteAllText(Output+"/static-result.txt","PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){File.WriteAllText(Output+"/static-result.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void RunFocusedProof(){SessionState.SetBool(Key+"RelicsOnly",true);Run();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);if(File.Exists(Output+"/result.txt"))File.Delete(Output+"/result.txt");File.WriteAllText(Output+"/checks.txt","");File.WriteAllText(Output+"/errors.txt","");
            try
            {
                foreach(var type in new[]{typeof(ItemBalancePatchTests),typeof(ItemFeedbackPatchTests)})
                {var suite=Activator.CreateInstance(type);foreach(var m in type.GetMethods().Where(m=>m.GetCustomAttribute<TestAttribute>()!=null)){m.Invoke(suite,null);Check(true,m.Name);}}
                GeometryChecks();
            }
            catch(Exception e){File.WriteAllText(Output+"/errors.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);return;}
            Restore();
            var catalog=JsonUtility.FromJson<Tutorials>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Distinct();
            File.WriteAllText(Output+"/prefs-backup.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void GeometryChecks()
        {
            var go=new GameObject("All item VFX checks");var fx=ItemFeedback.For(go.transform);
            try
            {
                for(int id=1;id<=66;id++)
                {
                    var v=fx.Item(id,Vector3.zero,1,1,sustained:true,sound:false);
                    Check(v!=null&&v.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"item geometry "+id);
                    Check(v.GetComponent<SpriteRenderer>()==null,"no catalog sprite in item VFX "+id);
                    int lease=v.Lease;v.Release();var reuse=fx.Item(id,Vector3.zero,1,1,sustained:true,sound:false);
                    v.Release(lease);Check(reuse.gameObject.activeSelf,"pooled lease protection "+id);reuse.Release();
                }
                foreach(var r in Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").relics.Where(r=>r!=null))
                {var v=fx.Relic(r,true);Check(v!=null&&v.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"relic geometry "+r.relicId);v.Release();}
                foreach(var name in new[]{"acquire","pop","bubble","bloom","chime","crystal","sweep"})Check(Resources.Load<AudioClip>("OctoberContent/ProcAudio/"+name)!=null,"sound exists "+name);
            }
            finally{Object.DestroyImmediate(go);}
        }
        static void Attach(){started=false;deadline=EditorApplication.timeSinceStartup+1800;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){File.AppendAllText(Output+"/errors.txt","Deadline reached");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;Restore();SessionState.SetBool(Key,false);SessionState.SetBool(Key+"RelicsOnly",false);bool failed=SessionState.GetBool(Key+"Failed",false);File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");EditorApplication.Exit(failed?1:0);return;}
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null){started=true;var go=new GameObject("Item proof fixture");Object.DontDestroyOnLoad(go);go.AddComponent<StageEntryLoadingTestHost>().StartCoroutine(Checks());}
        }
        static void Restore(){string p=Output+"/prefs-backup.json";if(!File.Exists(p))return;foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(p)).values)if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);PlayerPrefs.Save();File.Delete(p);}
        static FieldInfo Field(object o,string n){for(var t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}throw new Exception(n);}
        static void Set(object o,string n,object value)=>Field(o,n).SetValue(o,value);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,args);
        static void Check(bool ok,string text){if(!ok)throw new Exception("ITEM_PROC_FAIL "+text);File.AppendAllText(Output+"/checks.txt",text+"\n");}
        static void Clear(Character p)
        {
            foreach(var z in Object.FindObjectsOfType<OctoberItemZone>())Object.Destroy(z.gameObject);
            var runtime=p.GetComponent<OctoberItemRuntime>();if(runtime!=null)Object.DestroyImmediate(runtime);
            foreach(var v in Object.FindObjectsOfType<ItemFeedbackVisual>())v.Release();
            var audio=p.GetComponent<ItemProcAudio>();if(audio!=null)audio.enabled=false;
        }
        static IEnumerator Checks()
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;prefs.augmentVfxOpacity=1;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            var intro=Object.FindObjectOfType<IntroMovie>();if(intro!=null)Call(intro,"Finish");
            yield return null;Check(StageEntryLoading.Begin(CrossSceneData.CharacterBlueprint),"actual Level1 loading");while(StageEntryLoading.IsLoading)yield return null;
            var level=Object.FindObjectOfType<LevelManager>();var p=level.PlayerCharacter;Vector2 origin=p.transform.position;
            TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SnailBossDebugSpawn){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;foreach(var a in Object.FindObjectsOfType<Ability>()){a.enabled=false;a.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            Set(p,"isInvincible",true);Time.timeScale=1;Time.captureDeltaTime=1f/15;RunScore.Exclude("아이템 연출 자동 검증");
            var relics=RelicRuntime.Ensure(p);relics.Equip(null);relics.enabled=false;
            yield return Integration(p,origin);
            bool focus=SessionState.GetBool(Key+"RelicsOnly",false);
            var manifest=focus?JsonUtility.FromJson<Manifest>(File.ReadAllText(Output+"/manifest.json")):new Manifest();
            foreach(var item in Resources.LoadAll<MerchantItemBlueprint>("OctoberContent/Catalog").OrderBy(i=>i.octoberId))
            {
                if(focus&&!new[]{14,22,28,29,31,32,49,58,65,66}.Contains(item.octoberId))continue;
                Clear(p);yield return null;
                var r=OctoberItemRuntime.Get(p);r.enabled=true;var cds=(float[])Field(r,"cooldown").GetValue(r);for(int c=0;c<cds.Length;c++)cds[c]=Time.time+3600;int id=item.octoberId;bool proc=Active(id);
                manifest.entries.RemoveAll(e=>e.id=="item-"+id.ToString("00"));
                manifest.entries.Add(new Entry{id="item-"+id.ToString("00"),name=item.itemName,description=item.description,mode=proc?"발동 연출 · 조건을 설정한 Level1 촬영":"상시 능력 · 획득 연출 (지속 VFX 없음)",retired=ItemOfferPolicy.Retired(id)});
                for(int f=0;f<32;f++)
                {
                    if(f==4){if(proc){r.State.owned.Add(id);Trigger(r,id,origin);}else r.Give(item);}
                    yield return null;Capture("item-"+id.ToString("00"),f);
                }
                Check(true,"captured item "+id);
            }
            Clear(p);yield return null;
            foreach(var relic in Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").relics.Where(r=>r!=null))
            {
                foreach(var v in Object.FindObjectsOfType<ItemFeedbackVisual>())v.Release();
                bool proc=RelicProc(relic);if(focus&&!proc)continue;
                relics.Equip(null);
                manifest.entries.RemoveAll(e=>e.id=="relic-"+relic.relicId);
                manifest.entries.Add(new Entry{id="relic-"+relic.relicId,name=relic.relicName,description=relic.Description,mode=proc?"획득 후 실제 회복·방어·반사·부활 발동 · Level1 조건 설정 촬영":"상시 능력 · 획득 연출 (지속 VFX 없음)",retired=relic.relicId=="lotus_chips"});
                FoodBossRuntime target=null;
                if(relic.effectType==RelicBlueprint.RelicEffectType.Thorns){target=FoodBossRuntime.Spawn(FoodBossKind.Denikani,p,origin+Vector2.right*1.8f);target.AutoPatterns=false;target.enabled=false;}
                for(int f=0;f<32;f++)
                {
                    if(f==4)relics.AddReward(relic);
                    if(f==13 && proc)
                    {
                        if(relic.effectType==RelicBlueprint.RelicEffectType.Barrier)relics.Absorb(5);
                        else if(relic.effectType==RelicBlueprint.RelicEffectType.Thorns)relics.Hurt(10);
                        else if(relic.effectType==RelicBlueprint.RelicEffectType.Revival)Call(p,"Revive");
                        else {Set(p,"currentHealth",p.MaxHealth*.5f);if(relic.effectType==RelicBlueprint.RelicEffectType.HealingPickup)p.GainPickupHealth(10);else {relics.State.idleTime=2;relics.Tick(.5f);}}
                    }
                    yield return null;Capture("relic-"+relic.relicId,f);
                }
                if(target!=null)Object.Destroy(target.gameObject);
                Check(true,"captured relic "+relic.relicId);
            }
            File.WriteAllText(Output+"/manifest.json",JsonUtility.ToJson(manifest,true));
            Time.captureDeltaTime=0;SessionState.SetBool(Key+"Done",true);
        }
        static bool RelicProc(RelicBlueprint r)=>r.effectType==RelicBlueprint.RelicEffectType.HealingPickup||r.effectType==RelicBlueprint.RelicEffectType.Regeneration||r.effectType==RelicBlueprint.RelicEffectType.IdleRecovery||r.effectType==RelicBlueprint.RelicEffectType.Barrier||r.effectType==RelicBlueprint.RelicEffectType.Thorns||r.effectType==RelicBlueprint.RelicEffectType.Revival;
        static bool Active(int i)=>new[]{6,7,8,9,10,11,13,14,15,16,17,19,21,22,26,27,28,29,30,31,32,33,35,36,38,39,40,41,42,43,44,45,46,49,50,52,57,58,59,60,61,62,63,64,65,66}.Contains(i);
        static void Trigger(OctoberItemRuntime r,int id,Vector2 p)
        {
            // Use production ability/visual entry points. These are deterministic presentation fixtures,
            // not a claim that all passive items attack or that random procs fire every hit.
            switch(id)
            {
                case 13:case 30:r.enabled=true;break;
                case 14:r.Pulse(p,2.5f,2,14,2);break;
                case 22:r.Pulse(p,1.5f,3,22,1.2f);break;
                case 28:case 31:r.Pulse(p,3,3,id);break;
                case 29:r.Pulse(p,5,12,id);break;
                case 32:r.Pulse(p,4,12,id);break;
                case 16:r.Zone(p+Vector2.right*.8f,1.25f,1.8f,2,id);break;
                case 33:r.Zone(p+Vector2.right,1.6f,1.8f,2,id);break;
                case 17:case 45:r.Zone(p,3,1.8f,0,id,true,true);break;
                case 49:r.Zone(p+Vector2.right,2,1.5f,0,id,true);break;
                case 59:r.Zone(p+Vector2.right,2,.7f,2,id,true);break;
                case 61:((float[])Field(r,"cooldown").GetValue(r))[61]=Time.time;r.DashDestination(p+Vector2.right*2);break;
                case 62:r.StartCoroutine((IEnumerator)Call(r,"Backtrack",new Vector2[]{p+Vector2.left*2,p+Vector2.left,p,p+Vector2.right,p+Vector2.right*2}));break;
                case 65:r.Pulse(p,2,12,id);break;
                case 66:r.Wave(Vector2.right,6,12,id);break;
                case 58:r.StartCoroutine(HomingProof(r,p));break;
                case 21:ItemFeedback.For(r).Show(ItemFeedbackKind.PressureBurst,p,.65f,.8f);break;
                case 57:ItemFeedback.For(r).Item(id,p,.65f,2.5f);break;
                default:r.Flash(p,id);break;
            }
        }
        static void Capture(string id,int frame)
        {
            string dir=Output+"/frames/"+id;Directory.CreateDirectory(dir);
            var camera=Camera.main;var overlays=Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=overlays.Select(c=>c.worldCamera).ToArray();var distances=overlays.Select(c=>c.planeDistance).ToArray();var orders=overlays.Select(c=>c.sortingOrder).ToArray();var sorts=overlays.Select(c=>c.overrideSorting).ToArray();var layers=overlays.Select(c=>c.sortingLayerID).ToArray();
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var rt=RenderTexture.GetTemporary(768,432,24);Texture2D pixels=null;
            try
            {
                camera.targetTexture=rt;
                foreach(var c in overlays){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=camera.nearClipPlane+.1f;c.overrideSorting=true;c.sortingLayerName="Monster Full";c.sortingOrder=Mathf.Clamp(32100+c.sortingOrder,32100,32760);}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;pixels=new Texture2D(768,432,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,768,432),0,0);pixels.Apply();File.WriteAllBytes(dir+"/"+frame.ToString("000")+".png",pixels.EncodeToPNG());
            }
            finally
            {for(int i=0;i<overlays.Length;i++){overlays[i].renderMode=RenderMode.ScreenSpaceOverlay;overlays[i].worldCamera=cameras[i];overlays[i].planeDistance=distances[i];overlays[i].sortingOrder=orders[i];overlays[i].overrideSorting=sorts[i];overlays[i].sortingLayerID=layers[i];}camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);if(pixels!=null)Object.Destroy(pixels);}
        }
    }
}
