using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class AshiSkillPlayTests
    {
        const string Key="AshiSkillsTest";
        static double next,deadline;
        static int stage,frame;
        static Character player;
        static CharacterSkillRuntime skills;
        static SyringeDartAbility needle;
        static float attack,speed,move,cooldown,drag,pauseCooldown;
        static int count;
        static RunSceneCharacterSnapshot snapshot;
        static Keyboard keyboard;
        static double lastRecord;
        static int recordFrame;
        static string Output=>Path.GetFullPath("Library/AshiSkillProof");
        static AshiSkillPlayTests(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetBool(Key+"Done",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            stage=0;frame=-2;next=0;recordFrame=0;lastRecord=0;deadline=EditorApplication.timeSinceStartup+200;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Check(bool ok,string message){if(!ok)throw new Exception("[AshiSkillsTest] FAIL "+message);Debug.Log("[AshiSkillsTest] PASS "+message);}
        static void Near(float a,float b,string message)=>Check(Mathf.Abs(a-b)<.005f,message+" "+a+" / "+b);
        static Button Button(string name)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.name==name);
        static void Capture(string name){var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
        static void Click(Button button,bool touch=false)
        {
            Check(button.interactable,"enabled "+button.name);Canvas.ForceUpdateCanvases();
            var p=new ExtendedPointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position),button=PointerEventData.InputButton.Left,pointerType=touch?UIPointerType.Touch:UIPointerType.MouseOrPen};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
            Check(hits.Count>0&&hits[0].gameObject==button.gameObject,"real pointer hit "+button.name);
            ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,p,ExecuteEvents.pointerClickHandler);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false)||EditorApplication.timeSinceStartup>deadline)
            {
                if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[AshiSkillsTest] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            if(EditorApplication.isPlaying && skills!=null && skills.IsCutin && EditorApplication.timeSinceStartup>lastRecord+.10)
            {
                lastRecord=EditorApplication.timeSinceStartup;
                string folder=Output+(stage<9?"/cutin-2s":"/cutin-3s");Directory.CreateDirectory(folder);
                Capture((stage<9?"cutin-2s/":"cutin-3s/")+(recordFrame++).ToString("D4"));
            }
            if(!EditorApplication.isPlaying||ApothecaryUI.Instance==null||EditorApplication.timeSinceStartup<next||Time.frameCount<frame+2)return;
            frame=Time.frameCount;next=EditorApplication.timeSinceStartup+.25;
            try
            {
                var ui=ApothecaryUI.Instance;
                switch(stage++)
                {
                    case 0:
                        EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled=false;
                        var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                        Check(ui.Config.characters[0].skills!=null,"Ashi skill data attached");
                        Check(ui.Config.characters.Skip(1).All(c=>c.skills==null || c.skills.kind==CharacterSkillDefinition.SkillKind.Shini),"other characters have no accidental Ashi skills");
                        Capture("01-main");Click(Button("Button 게임 시작"));break;
                    case 1:
                        Check(Button("Passive skill slot")!=null&&Button("Active skill slot")!=null,"two preparation slots");Capture("02-prepare");
                        Click(Button("Active skill slot"));break;
                    case 2:
                        Capture("03-description");Click(Button("Button 닫기"));
                        CrossSceneData.CharacterBlueprint=ui.Config.characters[0];CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();SceneManager.LoadScene(1);next+=2;break;
                    case 3:
                        var level=UnityEngine.Object.FindObjectOfType<LevelManager>();player=level.PlayerCharacter;level.enabled=false;
                        foreach(var m in UnityEngine.Object.FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
                        EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled=false;
                        skills=player.Skills;needle=UnityEngine.Object.FindObjectOfType<SyringeDartAbility>();needle.enabled=false;
                        attack=player.AttackSpeedMultiplier;speed=needle.GetEffectiveSpeed();move=player.CurrentMoveSpeed;count=needle.GetEffectiveProjectileCount();cooldown=needle.GetEffectiveCooldown();drag=player.GetComponent<Rigidbody2D>().drag;
                        Check(skills.Definition.cutin.Length==8&&skills.Definition.activeWind.Length==8,"animation frame imports");
                        Check(!GameObject.Find("MiniMapRoot"),"minimap hidden while TAB map remains");Capture("04-ready");
                        Check(player.TryDash(),"actual dash starts");next+=.5;break;
                    case 4:
                        Check(skills.PassiveActive,"dash triggers Marathon");Near(player.AttackSpeedMultiplier,attack*1.2f,"attack speed x1.2");Near(needle.GetEffectiveCooldown(),cooldown/1.2f,"attack cooldown /1.2");
                        Check(needle.GetEffectiveProjectileCount()==count+2,"passive +2 projectiles");Capture("05-marathon");
                        keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.R));next+=.65;break;
                    case 5:
                        Check(skills.IsCutin&&Time.timeScale==0,"R starts paused 2-second cut-in");InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                        var cutinImage=ui.GetComponentsInChildren<Image>().First(i=>i.name=="Ashi cut-in portrait");
                        Check(cutinImage.sprite!=null&&cutinImage.color.a>.9f&&cutinImage.rectTransform.rect.height>300,"cut-in portrait visible with sprite and opaque tint");
                        UnityEngine.Object.FindObjectOfType<PauseMenu>()?.PlayPause();Check(Time.timeScale==0,"legacy pause button cannot resume cut-in");
                        Check(!skills.TryActivate(),"cannot retrigger during cut-in");Capture("06-cutin-2s");next+=2;break;
                    case 6:
                        Check(skills.Active&&!skills.IsCutin&&Time.timeScale==1,"cut-in resumes gameplay with active buff");
                        Near(player.CurrentMoveSpeed,move*2,"move stat x2");Near(player.GetComponent<Rigidbody2D>().drag,drag/2,"actual movement drag halved");Near(needle.GetEffectiveSpeed(),speed*2,"projectile travel x2");
                        Check(needle.GetEffectiveProjectileCount()==(count+2)*2,"combined count (base +2) x2");Check(skills.CooldownRemaining>40,"45-second cooldown running");Check(!skills.TryActivate(),"cooldown blocks repeated use");
                        Check(needle.GetHeavySnipeSkillProjectileCount()==6,"charged needle passive and active count");
                        int beforeHeavy=UnityEngine.Object.FindObjectsOfType<SyringeProjectile>().Length;
                        typeof(SyringeDartAbility).GetMethod("FireHeavySnipe",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(needle,new object[]{1f});
                        Check(UnityEngine.Object.FindObjectsOfType<SyringeProjectile>().Length==beforeHeavy+6,"charged needle actually emits six projectiles");
                        var cover=Button("Active skill R").GetComponentsInChildren<Image>().First(i=>i.name=="Clockwise cooldown cover");
                        Check(cover.fillOrigin==(int)Image.Origin360.Top&&!cover.fillClockwise,"cover recedes clockwise from twelve");
                        int pool=(int)PacingPatchTests.Get(needle,"projectileIndex");var projectile=needle.SpawnPlayerProjectile(pool,player.Position,1,0,speed*2,0);projectile.Launch(Vector2.right);
                        Check(projectile.GetComponent<SkillProjectileWind>()!=null,"active projectile receives wind wake");
                        ui.OpenRunBook();pauseCooldown=skills.CooldownRemaining;break;
                    case 7:
                        Near(skills.CooldownRemaining,pauseCooldown,"TAB freezes cooldown");Check(Button("Active skill slot")!=null,"TAB profile exposes both skills");Capture("07-tab");ui.CloseRunBook();break;
                    case 8:
                        Capture("08-active-hud");snapshot=player.CaptureRunSceneState();skills.Tick(100);Check(!skills.Active&&!skills.PassiveActive,"buffs expire cleanly");Near(player.CurrentMoveSpeed,move,"move restored");Near(player.AttackSpeedMultiplier,attack,"attack restored");Near(needle.GetEffectiveSpeed(),speed,"projectile speed restored");Check(needle.GetEffectiveProjectileCount()==count,"count restored");
                        player.RestoreRunSceneState(snapshot);Near(skills.CooldownRemaining,snapshot.SkillCooldownRemaining,"scene transfer preserves cooldown");Check(skills.Active,"scene transfer preserves active duration");
                        player.AddProjectileCount(3);skills.Tick(100);Check(needle.GetEffectiveProjectileCount()==count+3,"permanent upgrades survive expiration");
                        skills.Definition.cutinDuration=3;typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,true);break;
                    case 9:
                        Capture("09-mobile-layout");Check(ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Skill HUD").anchoredPosition.y>190,"mobile skills clear movement stick");
                        Click(Button("Active skill R"),true);Check(skills.IsCutin,"touch activates same skill");next+=2.2;break;
                    case 10:
                        Check(skills.IsCutin,"3-second cut-in still playing after 2 seconds");Capture("10-cutin-3s");next+=1.1;break;
                    case 11:
                        Check(skills.Active&&!skills.IsCutin,"3-second cut-in completes");skills.Definition.cutinDuration=2;
                        skills.Restore(0,0,22.5f);break;
                    case 12:
                        Capture("11-cooldown-half");typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,false);
                        var dead=player.CaptureRunSceneState();dead.CurrentHealth=0;player.RestoreRunSceneState(dead);break;
                    case 13:
                        Check(!skills.CanActivate&&skills.CooldownRemaining==0,"death clears skills and input");CrossSceneData.CharacterBlueprint=ui.Config.characters[1];SceneManager.LoadScene(1);next+=2;break;
                    case 14:
                        player=UnityEngine.Object.FindObjectOfType<LevelManager>().PlayerCharacter;Check(player.Blueprint.name=="아리","second character fixture");Check(!player.Skills.TryActivate(),"unconfigured character cannot use Ashi active");player.Skills.DashFinished();Check(!player.Skills.PassiveActive,"unconfigured character cannot use Ashi passive");
                        Capture("12-other-character");SessionState.SetBool(Key+"Done",true);break;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);}
        }
    }
}
