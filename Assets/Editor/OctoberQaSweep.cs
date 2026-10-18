using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Vampire.Editor
{
    // Reproducible visual sweep. Test preferences are restored even after a failed run.
    [InitializeOnLoad]
    public static class OctoberQaSweep
    {
        const string Key = "OctoberQaSweep", Output = "Library/OctoberQa";
        static bool started;
        static double deadline;
        [Serializable] class Saved { public string key; public bool exists; public int value; }
        [Serializable] class Backup { public Saved[] values; }
        [Serializable] class Entry { public string id; }
        [Serializable] class Catalog { public Entry[] entries; }
        static OctoberQaSweep() { if (SessionState.GetBool(Key, false)) Attach(); }
        [MenuItem("Tools/QA/October full visual sweep")]
        public static void Run() => Begin(false);
        public static void RunRegression() => Begin(true);
        public static void RunSoak()
        {
            Vampire.EditorTools.SupplyCapsuleArtImporter.Prepare();
            Begin(true,true);
        }
        static void Begin(bool regression,bool soak=false)
        {
            Directory.CreateDirectory(Output); Restore();
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys = catalog.entries.Select(e => TutorialGuide.SeenKey(e.id)).Concat(new[] { "Coins", "LobbySilverCoins" }).Distinct();
            File.WriteAllText(Output+"/prefs.json", JsonUtility.ToJson(new Backup { values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray() }));
            foreach(var entry in catalog.entries) PlayerPrefs.SetInt(TutorialGuide.SeenKey(entry.id),1);
            foreach(string name in new[]{"errors","text","assets","monsters","checks"}) File.WriteAllText(Output+"/"+name+".tsv", "");
            if(!regression)AssetAudit();
            SessionState.SetBool(Key+"Regression",regression);
            SessionState.SetBool(Key+"Soak",soak);
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+"Done",false); SessionState.SetBool(Key+"Failed",false); SessionState.SetInt(Key+"Phase",regression?1:0);
            EditorSceneManager.OpenScene(regression?"Assets/Scenes/Game/Level 1.unity":"Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,761);
            Attach(); EditorApplication.EnterPlaymode();
        }
        static void Restore()
        {
            if(!File.Exists(Output+"/prefs.json")) return;
            foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs.json")).values)
                if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);
            PlayerPrefs.Save();File.Delete(Output+"/prefs.json");
        }
        static void Attach() { started=false;deadline=EditorApplication.timeSinceStartup+1800;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log; }
        static void Log(string text,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){File.AppendAllText(Output+"/errors.tsv",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);if(type==LogType.Exception)SessionState.SetBool(Key+"Done",true);} }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Done",true);SessionState.SetBool(Key+"Failed",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();SessionState.SetBool(Key,false);bool fail=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",fail?"ERRORS_RECORDED":"SWEEP_COMPLETED");EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
                if(Application.isBatchMode)EditorApplication.Exit(fail?1:0);return;
            }
            if(!started&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                int phase=SessionState.GetInt(Key+"Phase",0);
                if(phase==0){started=true;ApothecaryUI.Instance.StartCoroutine(Lobby());}
                else if(Object.FindObjectOfType<LevelManager>()?.PlayerCharacter!=null){started=true;ApothecaryUI.Instance.StartCoroutine(Field());}
            }
        }
        public static FieldInfo FindField(object target,string name)
        {for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(f!=null)return f;}return null;}
        static void Set(object target,string field,object value)=>FindField(target,field).SetValue(target,value);
        static void Check(bool ok,string label){File.AppendAllText(Output+"/checks.tsv",(ok?"PASS":"FAIL")+"\t"+label+"\n");if(!ok)SessionState.SetBool(Key+"Failed",true);}
        static IEnumerator Frame(){yield return null;yield return new WaitForEndOfFrame();}
        static void Capture(string name)
        { var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());Object.Destroy(tex); }
        static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
        static void TextAudit(string page)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var t in Object.FindObjectsOfType<TMP_Text>())
            {
                if(string.IsNullOrWhiteSpace(t.text)||t.GetComponentInParent<Canvas>()==null)continue;
                t.ForceMeshUpdate();
                bool scroll=t.GetComponentInParent<ScrollRect>()!=null;
                var rect=t.rectTransform.rect;
                var corners=new Vector3[4];t.rectTransform.GetWorldCorners(corners);
                bool off=corners.Any(v=>v.x < -2 || v.y < -2 || v.x > Screen.width+2 || v.y>Screen.height+2);
                if(t.isTextOverflowing||off)
                    File.AppendAllText(Output+"/text.tsv",$"{page}\t{(scroll?"SCROLL":"STATIC")}\t{off}\t{t.isTextOverflowing}\t{rect.width:0}x{rect.height:0}\t{t.preferredWidth:0}x{t.preferredHeight:0}\t{PathOf(t.transform)}\t{t.text.Replace('\n',' ')}\n");
            }
        }
        static void AssetAudit()
        {
            var textures=new HashSet<string>();
            foreach(var guid in AssetDatabase.FindAssets("t:MonsterBlueprint"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var bp=AssetDatabase.LoadAssetAtPath<MonsterBlueprint>(path);
                if(bp==null||path.ToLowerInvariant().Contains("snail")||path.ToLowerInvariant().Contains("boss"))continue;
                var frames=bp.walkSpriteSequence;
                if(frames==null||frames.Length==0){File.AppendAllText(Output+"/assets.tsv","NO_WALK\t"+path+"\n");continue;}
                var sizes=frames.Where(s=>s!=null).Select(s=>s.bounds.size).ToArray();
                if(sizes.Length!=frames.Length)File.AppendAllText(Output+"/assets.tsv","NULL_FRAME\t"+path+"\n");
                float min=sizes.Length>0?sizes.Min(s=>s.y):0,max=sizes.Length>0?sizes.Max(s=>s.y):0;
                File.AppendAllText(Output+"/assets.tsv",$"SEQUENCE\t{path}\t{frames.Length}\t{min:0.000}\t{max:0.000}\n");
                foreach(var s in frames.Where(s=>s!=null)) textures.Add(AssetDatabase.GetAssetPath(s.texture));
            }
            foreach(string path in textures)
            {
                var raw=new Texture2D(2,2);if(!raw.LoadImage(File.ReadAllBytes(path))){Object.DestroyImmediate(raw);continue;}
                var px=raw.GetPixels32();int alpha=px.Count(p=>p.a==0),magenta=px.Count(p=>p.a>100&&p.r>200&&p.b>180&&p.g<65);
                File.AppendAllText(Output+"/assets.tsv",$"ALPHA\t{path}\t{raw.width}x{raw.height}\ttransparent={alpha}\tmagenta={magenta}\n");Object.DestroyImmediate(raw);
            }
        }
        static void Preferences()
        {Application.runInBackground=true;var p=GamePreferences.Current.Copy();p.pauseOnFocusLoss=false;p.muteOnFocusLoss=false;GamePreferences.Apply(p,false);}
        static IEnumerator Lobby()
        {
            Preferences();yield return new WaitForSecondsRealtime(1);var ui=ApothecaryUI.Instance;
            foreach(string page in new[]{"main","prepare","unlock","settings"})
            {
                int tabs=page=="prepare"?4:page=="unlock"?3:1;
                for(int tab=0;tab<tabs;tab++){ui.Show(page,tab);yield return Frame();TextAudit(page+tab);Capture("lobby-"+page+tab);}
            }
            ui.Show("prepare");
            for(int i=0;i<ui.Config.characters.Length;i++)
            {Set(ui,"character",ui.Config.characters[i]);Set(ui,"characterIndex",i);ui.Show("prepare");yield return Frame();TextAudit("character"+i);Capture("lobby-character"+i);}
            CrossSceneData.CharacterBlueprint=ui.Config.characters[0];CrossSceneData.ClearStartingLobbyItems();
            SessionState.SetInt(Key+"Phase",1);started=false;Time.timeScale=1;SceneManager.LoadScene("Level 1");
        }
        static IEnumerator Field()
        {
            Preferences();yield return new WaitForSecondsRealtime(2);var ui=ApothecaryUI.Instance;var level=Object.FindObjectOfType<LevelManager>();var actor=level.PlayerCharacter;
            if(TutorialGuide.Instance!=null){TutorialGuide.Instance.Close();TutorialGuide.Instance.enabled=false;}
            if(level.EntityManager.AbilitySelectionDialog.MenuOpen)level.EntityManager.AbilitySelectionDialog.Close();
            if(SessionState.GetBool(Key+"Soak",false))
            {
                yield return OctoberQaSoak.Run(level);
                SessionState.SetBool(Key+"Done",true);yield break;
            }
            Time.timeScale=1;level.enabled=false;level.EntityManager.enabled=false;
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner||b is SupplyCapsuleDirector){b.enabled=false;b.StopAllCoroutines();}
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.DespawnForStageTransfer();
            foreach(var a in Object.FindObjectsOfType<Ability>())a.enabled=false;
            Set(actor,"isInvincible",true);actor.Move(Vector2.zero);
            if(SessionState.GetBool(Key+"Regression",false))
            {
                yield return OctoberQaRegression.Run(level);
                SessionState.SetBool(Key+"Done",true);yield break;
            }
            yield return Frame();TextAudit("hud");Capture("field-hud");
            ui.OpenRunBook();yield return Frame();TextAudit("tab-empty");Capture("field-tab-empty");
            var items=OctoberItemRuntime.Get(actor);foreach(var item in ui.Config.items.Where(i=>i!=null))items.Give(item);
            foreach(var relic in ui.Config.relics.Where(r=>r!=null))actor.Relics.Equip(relic);
            ui.Show("run");yield return Frame();TextAudit("tab-full");Capture("field-tab-full");
            ui.Show("skillReward");yield return Frame();TextAudit("skillReward");Capture("field-skill-reward");
            ui.Show("settings");yield return Frame();TextAudit("field-settings");Capture("field-settings");
            ui.CloseRunBook();Time.timeScale=1;
            var cameraObject=new GameObject("QA monster camera");var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.orthographicSize=2.2f;camera.aspect=1;
            var pools=level.CurrentLevelBlueprint.monsters;
            int serial=0;
            for(int p=0;p<pools.Length;p++)for(int b=0;b<pools[p].monsterBlueprints.Length;b++)
            {
                var bp=pools[p].monsterBlueprints[b];if(bp==null||bp is BossMonsterBlueprint||bp.name.ToLowerInvariant().Contains("snail")||pools[p].monstersPrefab.name.ToLowerInvariant().Contains("snail"))continue;
                var monster=level.EntityManager.SpawnMonster(p,(Vector2)actor.transform.position+Vector2.right*4,bp,0,true);
                if(monster==null){Check(false,"spawn "+bp.name);continue;}
                Set(monster,"currentHealth",1000000f);
                yield return new WaitForSecondsRealtime(.3f);
                var body=monster.BodyRenderer;Check(body!=null&&body.sprite!=null,"visible body "+bp.name);
                if(body!=null)
                {
                    string shader=body.sharedMaterial!=null?body.sharedMaterial.shader.name:"NULL";
                    File.AppendAllText(Output+"/monsters.tsv",$"{serial}\t{bp.name}\t{monster.GetType().Name}\t{body.bounds.size}\t{monster.transform.localScale}\t{shader}\n");
                    Check(body.sharedMaterial!=null&&body.sharedMaterial.shader.isSupported&&!shader.Contains("Error"),"supported idle shader "+bp.name);
                    for(int f=0;f<3;f++)
                    {
                        camera.transform.position=new Vector3(monster.transform.position.x,monster.transform.position.y+body.bounds.size.y*.4f,Camera.main.transform.position.z);
                        CaptureCamera(camera,$"monster-{serial:D2}-{f}");yield return new WaitForSecondsRealtime(.13f);
                    }
                    monster.TakeDamage(.01f);yield return null;
                    shader=body.sharedMaterial!=null?body.sharedMaterial.shader.name:"NULL";
                    Check(body.sharedMaterial!=null&&body.sharedMaterial.shader.isSupported&&!shader.Contains("Error"),"supported hit shader "+bp.name);
                    CaptureCamera(camera,$"monster-{serial:D2}-hit");
                }
                if(monster.gameObject.activeInHierarchy&&monster.HP>0)monster.DespawnForStageTransfer();serial++;
            }
            Object.Destroy(cameraObject);ui.OpenRunBook();yield return Frame();Capture("field-final-tab");
            ApothecaryUI.TryShowResult(false);yield return Frame();TextAudit("failure");Capture("result-failure");
            ApothecaryUI.TryShowResult(true);yield return Frame();TextAudit("success");Capture("result-success");
            SessionState.SetBool(Key+"Done",true);
        }
        static void CaptureCamera(Camera camera,string name)
        {
            var target=RenderTexture.GetTemporary(480,480,24);var prev=camera.targetTexture;var active=RenderTexture.active;var tex=new Texture2D(480,480,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,480,480),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());}
            finally{camera.targetTexture=prev;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Object.Destroy(tex);}
        }
    }
}
