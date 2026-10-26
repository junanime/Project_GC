using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public static class FestivalOctober27Checks
    {
        static void Check(bool value,string label)=>FestivalQaSmoke.Check(value,label);
        static void Capture(string name)=>typeof(FestivalDemoSmoke).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{name});
        public static IEnumerator Run()
        {
            var background=InputSystem.settings.backgroundBehavior;
            var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                foreach(string key in new[]{"Ashi","Ari","Hyuki","Shini"})
                {
                    int before=FestivalQaStore.ReadAll(FestivalQaStore.Folder).runs.Count;
                    var blueprint=FestivalDemo.Catalog.characters.First(c=>OctoberArt.CharacterKey(c)==key);
                    CrossSceneData.CharacterBlueprint=blueprint;
                    Check(StageEntryLoading.Begin(blueprint),"real load "+key);
                    while(StageEntryLoading.IsLoading)yield return null;
                    var level=Object.FindObjectOfType<LevelManager>();var hero=level.PlayerCharacter;var skill=hero.Skills;
                    var current=RunScore.Current;
                    yield return new WaitForSecondsRealtime(.5f);
                    Check(FestivalGaugeTutorial.IsOpen&&Time.timeScale==0&&level.CurrentLevelTime==0,"tutorial blocks timer after loading "+key);
                    Check(!skill.TryActivate(),"tutorial blocks active damage "+key);
                    var bars=hero.GetComponent<PlayerCombatBars>();
                    Check(bars.HealthRect.GetComponentInParent<Canvas>().enabled&&bars.DashRect.gameObject.activeInHierarchy&&bars.ActiveRect.gameObject.activeInHierarchy,"real gauges visible through pause "+key);
                    FestivalQaRecorder.Begin(level,current.FestivalRunId);FestivalQaRecorder.Checkpoint();
                    var rows=FestivalQaStore.ReadAll(FestivalQaStore.Folder).runs;
                    Check(rows.Count==before+1&&rows.Single(r=>r.run==current.FestivalRunId).frames==0,"run counted once and tutorial excluded from FPS "+key);
                    Check(rows.Single(r=>r.run==current.FestivalRunId).character==key,"real selected character persisted "+key);
                    Capture("tutorial-"+key);
                    if(key=="Shini")Check(FestivalGaugeTutorial.Instance.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("불씨 연료")),"Shini tutorial explains fuel instead of cooldown");
                    float cd=skill.EffectiveCooldown;
                    FestivalDemo.EnabledOverride=false;float ordinary=skill.EffectiveCooldown;FestivalDemo.EnabledOverride=null;
                    Check(Mathf.Approximately(cd,Mathf.Min(24,ordinary)*(key=="Ari"||key=="Hyuki"?.6f:1)),"festival cooldown multiplier after old cap "+key);
                    if(key=="Ari"||key=="Hyuki")Check(Mathf.Approximately(cd,14.4f),"starting cooldown is 14.4 seconds "+key);
                    if(key=="Shini")
                    {
                        Check(Mathf.Approximately(skill.Breath.EffectiveDamagePerSecond,63),"Shini active base DPS becomes 63");
                        FestivalDemo.EnabledOverride=false;
                        Check(Mathf.Approximately(skill.Breath.EffectiveDamagePerSecond,35),"ordinary Shini damage unchanged");FestivalDemo.EnabledOverride=null;
                    }
                    int fuel=skill.Breath!=null?skill.Breath.Fuel:0;
                    bool pointer=key=="Ari"||key=="Shini";
                    if(pointer)InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(40,40)}.WithButton(MouseButton.Left));
                    else InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Space));
                    yield return null;yield return null;yield return new WaitForSecondsRealtime(.12f);
                    Check(FestivalGaugeTutorial.IsOpen&&Time.timeScale==0,"held continue input cannot resume or cast "+key);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(40,40)});
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                    for(int i=0;i<7;i++)yield return null;
                    Check(!FestivalGaugeTutorial.IsOpen&&Time.timeScale==1&&level.CurrentLevelTime>0,"continue on release resumes combat "+key);
                    Check(!skill.Active&&skill.CooldownRemaining==0&&(skill.Breath==null||skill.Breath.Fuel==fuel),"dismiss does not activate skill or spend fuel "+key);
                    if(key=="Hyuki")yield return ColdVisualChecks(level);
                    if(key=="Ashi")RetryStorageCheck(level,current.FestivalRunId);
                    else current.Finish(false);
                    Check(FestivalQaStore.ReadAll(FestivalQaStore.Folder).runs.Single(r=>r.run==current.FestivalRunId).closed,"end checkpoint retained "+key);
                    SceneManager.LoadScene(0);yield return null;yield return null;yield return null;
                    Check(!FestivalGaugeTutorial.IsOpen,"scene exit cleans tutorial "+key);
                }
                FestivalDemo.EnabledOverride=false;FestivalGaugeTutorial.Show(Object.FindObjectOfType<LevelManager>());
                Check(!FestivalGaugeTutorial.IsOpen,"normal mode has no festival tutorial");FestivalDemo.EnabledOverride=null;
            }
            finally
            {
                FestivalDemo.EnabledOverride=null;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            }
        }
        static void RetryStorageCheck(LevelManager level,string id)
        {
            var recorder=Object.FindObjectOfType<FestivalQaRecorder>();var field=typeof(FestivalQaRecorder).GetField("folder",BindingFlags.Instance|BindingFlags.NonPublic);
            string folder=(string)field.GetValue(recorder),block=Path.Combine("Library/FestivalQA","blocked-"+Guid.NewGuid().ToString("N"));
            File.WriteAllText(block,"not a directory");
            try
            {
                field.SetValue(recorder,block);RunScore.Current.Finish(false);
                Check(FestivalQaRecorder.Problem!=null,"failed final save remains visibly pending");
                FestivalQaRecorder.Checkpoint();Check(FestivalQaRecorder.Problem!=null,"empty flush does not erase unsaved final run");
            }
            finally{field.SetValue(recorder,folder);}
            FestivalQaRecorder.Checkpoint();
            Check(FestivalQaRecorder.Problem==null&&FestivalQaStore.ReadAll(folder).runs.Single(r=>r.run==id).closed,"restored storage retries closed run without duplication");
        }
        static IEnumerator ColdVisualChecks(LevelManager level)
        {
            var point=(Vector2)level.PlayerCharacter.transform.position+Vector2.right*1.4f;
            Ver4GroundEffect.Create(point,1.6f,false,0,level.PlayerCharacter,0);
            var effect=Object.FindObjectsOfType<ColdGroundVisual>().OrderBy(v=>Vector2.Distance(v.transform.position,point)).First();
            yield return new WaitForSeconds(.32f);
            Check(effect.GetComponent<LineRenderer>()==null&&effect.GetComponent<MeshRenderer>().sortingLayerName==GroundVisualSorting.LayerName,"cold ground replaces wire ring and stays below actors");
            var bounds=effect.GetComponent<MeshFilter>().sharedMesh.bounds;
            Check(bounds.size.x<=2.01f&&bounds.size.y<=2.01f&&bounds.size.x>1.8f,"cold mesh matches radius one gameplay footprint");
            Capture("cold-ground-gameplay");
            float opacity=GamePreferences.Current.augmentVfxOpacity;
            GamePreferences.Current.augmentVfxOpacity=0;yield return null;yield return null;
            var block=new MaterialPropertyBlock();effect.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            Check(block.GetFloat("_AugmentOpacity")==0,"cold ground honors augment opacity preference");
            GamePreferences.Current.augmentVfxOpacity=opacity;
            Time.timeScale=0;yield return new WaitForSecondsRealtime(1.7f);
            Check(effect!=null,"cold lifetime pauses with combat");Time.timeScale=1;
            yield return new WaitForSeconds(1.5f);yield return null;
            Check(effect==null,"cold ground mesh cleaned after original lifetime");
            Ver4GroundEffect.Create(point,.2f,true,0,level.PlayerCharacter,0);
            Check(Object.FindObjectsOfType<Ver4GroundEffect>().Any(e=>e.name=="목침 회복 장판"&&e.GetComponent<LineRenderer>()!=null&&e.GetComponent<ColdGroundVisual>()==null),"wood healing ground unchanged");
        }
    }
}
