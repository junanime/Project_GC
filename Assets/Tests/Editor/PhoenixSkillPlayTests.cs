using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class PhoenixSkillPlayTests
    {
        const string Key="PhoenixSkillTest";
        static double next,deadline;static int stage;static Character player;static CharacterSkillRuntime skill;static PhoenixSkillVisual visual;
        static Monster near,far;static SyringeDartAbility needle;static float speed,timer,phase,projectileSpeed;static Keyboard keyboard;static int projectiles;
        static PhoenixSkillPlayTests(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetBool(Key+"Done",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){stage=0;next=0;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Check(bool ok,string message){if(!ok)throw new Exception("[PhoenixTest] FAIL "+message);Debug.Log("[PhoenixTest] PASS "+message);}
        static void Near(float a,float b,string m)=>Check(Mathf.Abs(a-b)<.02f,m+" "+a+" / "+b);
        static Button Button(string n)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.name==n);
        static void Capture(string n){Directory.CreateDirectory("Library/PhoenixProof");var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/PhoenixProof/"+n+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false)||EditorApplication.timeSinceStartup>deadline)
            {
                if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("[PhoenixTest] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            // Editor wall time can advance while the game has not rendered a frame (imports/captures).
            // Use game-frame unscaled time so pause checks still run without racing LateUpdate.
            if(!EditorApplication.isPlaying||ApothecaryUI.Instance==null||Time.unscaledTime<next)return;
            next=Time.unscaledTime+.25;
            try
            {
                var ui=ApothecaryUI.Instance;
                switch(stage++)
                {
                    case 0:
                        var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                        CrossSceneData.CharacterBlueprint=ui.Config.characters.Single(c=>c.name=="혁이");CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();SceneManager.LoadScene(1);next+=2;break;
                    case 1:
                        var level=UnityEngine.Object.FindObjectOfType<LevelManager>();player=level.PlayerCharacter;level.enabled=false;
                        foreach(var m in UnityEngine.Object.FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
                        needle=UnityEngine.Object.FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();skill=player.Skills;visual=player.GetComponent<PhoenixSkillVisual>();
                        foreach(var projectile in UnityEngine.Object.FindObjectsOfType<Projectile>())projectile.gameObject.SetActive(false);
                        Check(skill.IsHyuki&&!skill.AshiActive&&!skill.AshiPassive,"Hyuki has independent skills");
                        Check(needle.GetCurrentSpecialRuntime().ver4.Has(SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle),"starts with owned ice needle");
                        Check(skill.Definition.phoenixFrames.Length==16&&skill.Definition.icePrison.Length==8,"phoenix and prison animation frames imported");
                        Check(skill.Definition.passiveIcon!=null&&skill.Definition.activeIcon!=null,"both profile icons installed");
                        player.Move(Vector2.zero);player.StartIdleAnimation();skill.RestoreSleep(4.8f,0);speed=player.CurrentMoveSpeed;projectileSpeed=player.ProjectileSpeedMultiplier;projectiles=needle.GetEffectiveProjectileCount();break;
                    case 2:
                        Check(skill.SleepStacks==0&&visual.CrystalCount==0,"retired sleep passive stays inactive");
                        skill.RollInstantFreeze(.99f);break;
                    case 3:
                        Check(skill.IceProcFailures==1&&visual.CrystalCount==0,"failed roll increases chance without old crystals");Capture("01-shivering-idle");player.Move(Vector2.right);break;
                    case 4:
                        Check(skill.IceProcFailures==1&&!skill.PassiveActive,"movement preserves ice probability counter");Near(player.CurrentMoveSpeed,speed,"movement has no retired sleep boost");
                        Check(!skill.AshiPassive&&needle.GetEffectiveProjectileCount()==projectiles,"Hyuki passive does not inherit Ashi projectiles");Near(player.ProjectileSpeedMultiplier,projectileSpeed,"sleep boost affects movement only");Capture("02-passive-wake");
                        var snapshot=player.CaptureRunSceneState();skill.RestoreIceProcFailures(0);player.RestoreRunSceneState(snapshot);Check(skill.IceProcFailures==1,"scene snapshot restores ice failures");
                        skill.Tick(5);Near(player.CurrentMoveSpeed,speed,"four-second movement boost expires");player.Move(Vector2.zero);
                        var l=UnityEngine.Object.FindObjectOfType<LevelManager>();var data=l.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
                        near=l.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*2,data,500);near.enabled=true;
                        far=l.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*7,data,500);far.enabled=false;
                        keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.R));break;
                    case 5:
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());Check(skill.Active&&!skill.IsCutin&&Time.timeScale>0,"R activates in world without pausing");
                        Check(skill.CooldownRemaining>59&&skill.CooldownRemaining<=60,"sixty-second cooldown starts");Check(!skill.TryActivate(),"cooldown prevents repeated cast");
                        Check(near.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"active waits for blizzard before freezing");
                        Check(far.GetComponent<NeuralBlockedMonsterStatus>()==null,"enemy outside radius four is unaffected");
                        var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();Check(player.Blueprint.idleSpriteSequence.Contains(body.sprite),"original Hyuki sprite retained");Capture("03-ice-summon");next+=.7;break;
                    case 6:
                        if(skill.SummonRemaining>.3f){stage--;break;}
                        Check(visual.BlizzardStrength>.7f,"blizzard covers field at its peak");Capture("04-blizzard");phase=visual.BlizzardPhase;next+=1.0;break;
                    case 7:
                        if(skill.IsSummoning||visual.BlizzardStrength>=1){stage--;break;}
                        Check(near.GetComponent<NeuralBlockedMonsterStatus>()?.IceRemaining>3.5f,"active releases five-second freeze when blizzard begins fading");
                        Check(visual.BlizzardPhase>phase&&visual.BlizzardStrength>0&&visual.BlizzardStrength<1,"snow keeps moving while fading");
                        var shell=near.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="Translucent ice prison");Check(shell.enabled&&shell.color.a>.2f&&shell.color.a<.6f,"monster remains visible through translucent prison");Capture("05-snow-clearing");
                        timer=near.GetComponent<NeuralBlockedMonsterStatus>().IceRemaining;ui.OpenRunBook();break;
                    case 8:
                        Near(near.GetComponent<NeuralBlockedMonsterStatus>().IceRemaining,timer,"TAB pause freezes ice timer");Check(Button("Active skill slot")!=null&&Button("Passive skill slot")!=null,"TAB contains both skill profiles");Capture("06-tab");ui.CloseRunBook();next+=5;break;
                    case 9:
                        if(near.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen==true){stage--;break;}
                        Check(near.GetComponent<NeuralBlockedMonsterStatus>()==null&&near.enabled,"five-second freeze expires and restores movement");
                        var status=near.GetComponent<Ver4NeedleStatus>()??near.gameObject.AddComponent<Ver4NeedleStatus>();
                        for(int i=0;i<500&&near.GetComponent<NeuralBlockedMonsterStatus>()==null;i++)status.Hit(needle.GetCurrentSpecialRuntime(),player,needle.SkillTargetLayer);
                        Check(near.GetComponent<NeuralBlockedMonsterStatus>()?.IceRemaining>4.9f,"ice needle also uses five-second freeze");
                        Check(status.ConsumeFrozen()&&!near.GetComponent<NeuralBlockedMonsterStatus>().IceFrozen,"next ice needle still shatters frozen status");
                        IceSkillRules.Freeze(near);near.gameObject.SetActive(false);Check(!near.GetComponent<NeuralBlockedMonsterStatus>().IceFrozen,"pooled enemy clears ice state");
                        skill.Tick(100);typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,true);break;
                    case 10:
                        Button("Active skill R").onClick.Invoke();Check(skill.Active,"mobile skill button activates Hyuki");
                        var dead=player.CaptureRunSceneState();dead.CurrentHealth=0;player.RestoreRunSceneState(dead);break;
                    case 11:
                        Check(!skill.CanActivate&&!visual.Playing&&visual.CrystalCount==0,"death clears skills and visual effects");typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,false);SessionState.SetBool(Key+"Done",true);break;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);}
        }
    }
}
