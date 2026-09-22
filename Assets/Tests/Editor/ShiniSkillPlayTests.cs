using System;
using System.Collections;
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
    public static class ShiniSkillPlayTests
    {
        const string Key="ShiniSkillTest";
        static double next,deadline;static int stage;static Character player;static CharacterSkillRuntime skill;static ShiniSkillRuntime fire;static SyringeDartAbility needle;
        static float move,attack,damage,pause;static int count;static Vector3 walkStart;static Monster target;static Keyboard keyboard;
        static ShiniSkillPlayTests(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Failed",false);SessionState.SetBool(Key+"Done",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach(){stage=0;next=0;deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
        static void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)SessionState.SetBool(Key+"Failed",true);}
        static void Check(bool ok,string message){if(!ok)throw new Exception("[ShiniTest] FAIL "+message);Debug.Log("[ShiniTest] PASS "+message);}
        static void Near(float a,float b,string m)=>Check(Mathf.Abs(a-b)<.01f,m+" "+a+" / "+b);
        static Button Button(string n)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.name==n);
        static void Capture(string n){Directory.CreateDirectory("Library/ShiniProof");var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("Library/ShiniProof/"+n+".png",t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false)||EditorApplication.timeSinceStartup>deadline)
            {
                if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);Debug.Log("[ShiniTest] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            if(!EditorApplication.isPlaying||ApothecaryUI.Instance==null||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.3;
            try
            {
                var ui=ApothecaryUI.Instance;
                switch(stage++)
                {
                    case 0:
                        var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                        Check(!ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Passive skill slot"||b.name=="Active skill slot"),"main page has no skill icons");Capture("01-main");
                        Button("Button 게임 시작").onClick.Invoke();break;
                    case 1:
                        Button("Active skill slot").onClick.Invoke();break;
                    case 2:
                        Check(ui.GetComponentsInChildren<Image>().Any(i=>i.name=="Skill detail icon"&&i.sprite!=null),"detail panel includes left icon");Capture("02-details");
                        CrossSceneData.CharacterBlueprint=ui.Config.characters.Single(c=>c.name=="신이");CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();SceneManager.LoadScene(1);next+=2;break;
                    case 3:
                        var level=UnityEngine.Object.FindObjectOfType<LevelManager>();player=level.PlayerCharacter;level.enabled=false;
                        foreach(var m in UnityEngine.Object.FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
                        needle=UnityEngine.Object.FindObjectOfType<SyringeDartAbility>();needle.enabled=false;skill=player.Skills;fire=player.GetComponent<ShiniSkillRuntime>();
                        Check(skill.IsShini,"Shini skill identity");Check(needle.HasVer4Special(SyringeSpecialAugmentAbility.SpecialAugmentType.FireNeedle),"starts with fire needle enabled");
                        Check(needle.GetCurrentSpecialRuntime().ver4.Has(SyringeSpecialAugmentAbility.SpecialAugmentType.FireNeedle),"starting fire is registered in shared progression");
                        Check(skill.Definition.burningWalk.Length==8&&skill.Definition.burningIdle.Length==8&&skill.Definition.burningDash.Length==8,"three transformed animations imported");
                        Check(skill.Definition.fireTrail.Length==7&&skill.Definition.burnVfx.Length==3,"trail and body burn frames imported");
                        move=player.CurrentMoveSpeed;attack=player.AttackSpeedMultiplier;count=needle.GetEffectiveProjectileCount();
                        Check(player.TryDash(),"real dash starts");next+=.4;break;
                    case 4:
                        Check(fire.SegmentCount>2,"resolved dash path creates continuous segments");Near(player.CurrentMoveSpeed,move,"passive has no Ashi move buff");Near(player.AttackSpeedMultiplier,attack,"passive has no Ashi attack buff");Check(needle.GetEffectiveProjectileCount()==count,"passive has no Ashi projectile buff");Capture("03-dash-trail");
                        keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.R));break;
                    case 5:
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState());Check(skill.IsSummoning&&!skill.Active&&!skill.IsCutin,"R starts in-world phoenix summoning before active state");
                        Near(skill.CooldownRemaining,0,"cooldown waits for summon completion");Check(!skill.TryActivate(),"summoning rejects repeat activation");
                        var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();Check(player.Blueprint.idleSpriteSequence.Contains(body.sprite)||player.Blueprint.walkSpriteSequence.Contains(body.sprite),"original body retained during phoenix summoning");
                        var casting=player.CaptureRunSceneState();skill.Restore(0,0,0);player.RestoreRunSceneState(casting);Near(skill.SummonRemaining,casting.SkillSummonRemaining,"scene snapshot restores pending summon");
                        ui.OpenRunBook();pause=skill.SummonRemaining;break;
                    case 6:
                        Near(skill.SummonRemaining,pause,"TAB pauses summoning and delays cooldown start");ui.CloseRunBook();stage=60;next+=3;break;
                    case 60:
                        Check(skill.Active&&!skill.IsSummoning&&skill.ActiveRemaining>14&&skill.CooldownRemaining>34,"full fifteen-second active and thirty-five-second cooldown start after summon");
                        Check(player.GetComponent<PhoenixSkillVisual>().Absorbed,"phoenix finishes before transformation");
                        var transformed=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();Check(skill.Definition.burningIdle.Contains(transformed.sprite)||skill.Definition.burningWalk.Contains(transformed.sprite),"existing burning active images resume after summon");
                        Near(player.CurrentMoveSpeed,move,"active does not alter movement speed");Check(needle.GetEffectiveProjectileCount()==count,"active does not double needles");
                        walkStart=player.transform.position;player.Move(Vector2.right);next+=.7;break;
                    case 61:
                        stage=7;
                        Check(Vector3.Distance(walkStart,player.transform.position)>.05f,"ordinary movement actually changes world position");Check(fire.SegmentCount>4,"ordinary movement generates active trail");Capture("04-active-walk");player.Move(Vector2.zero);
                        ui.OpenRunBook();pause=skill.CooldownRemaining;break;
                    case 7:
                        Near(skill.CooldownRemaining,pause,"TAB pause freezes skill timers");Check(Button("Active skill slot")!=null,"TAB retains skill profile slots");Capture("05-tab");ui.CloseRunBook();
                        var snapshot=player.CaptureRunSceneState();skill.Tick(100);Check(!skill.Active,"active expires");player.RestoreRunSceneState(snapshot);Near(skill.ActiveRemaining,snapshot.SkillActiveRemaining,"scene snapshot restores active timer");Near(skill.CooldownRemaining,snapshot.SkillCooldownRemaining,"scene snapshot restores cooldown");
                        skill.Tick(100);next+=3.2;break;
                    case 8:
                        Check(fire.SegmentCount==0,"all old path sections expire after three seconds");
                        fire.Sample(Vector3.zero,Vector3.right,true,Time.time);fire.Sample(Vector3.right,Vector3.right*2,true,Time.time+1);fire.Expire(Time.time+3.1f);Check(fire.SegmentCount>0,"later-created path survives earlier expiry");fire.Expire(Time.time+4.1f);Check(fire.SegmentCount==0,"later path expires at its own three seconds");
                        var l=UnityEngine.Object.FindObjectOfType<LevelManager>();var data=l.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];target=l.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*3,data,500);target.enabled=false;
                        float hp=target.HP;fire.Hit(target);Check(target.HP<hp,"trail uses real needle damage pipeline");
                        fire.Sample(target.transform.position+Vector3.up*.27f-Vector3.right,target.transform.position+Vector3.up*.27f+Vector3.right,true,Time.time);
                        fire.Sample(target.transform.position+Vector3.up*.27f+Vector3.right,target.transform.position+Vector3.up*.27f-Vector3.right,true,Time.time);
                        var scan=typeof(ShiniSkillRuntime).GetMethod("DamageTrail",BindingFlags.NonPublic|BindingFlags.Instance);
                        hp=target.HP;scan.Invoke(fire,null);Check(target.HP<hp,"overlapping path colliders hit real enemy");
                        float after=target.HP;scan.Invoke(fire,null);Near(target.HP,after,"overlapping path cannot duplicate direct hit within needle interval");
                        fire.Expire(Time.time+4);
                        var status=target.GetComponent<Ver4NeedleStatus>()??target.gameObject.AddComponent<Ver4NeedleStatus>();
                        var runtime=needle.GetCurrentSpecialRuntime();runtime.ver4HitDamage=100;
                        for(int i=0;i<200&&status.BurnStacks<2;i++)Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
                        Check(status.BurnStacks>=2,"needle creates shared burn stacks");
                        for(int i=0;i<200&&status.BurnStacks<3;i++)status.ApplyFire(runtime,player);
                        Check(status.BurnStacks==3,"trail adds to needle stack list up to shared cap");
                        ForceBurnTick(status);float before=target.HP;PacingPatchTests.Get(status,"burns");
                        typeof(Ver4NeedleStatus).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(status,null);
                        damage=target.SpawnMaxHealth*.005f*3;Near(before-target.HP,damage,"ALL three burn stacks deal their own tick damage");Capture("06-burn");
                        target.gameObject.SetActive(false);Check(status.BurnStacks==0,"pooled enemy clears burn state");
                        typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,true);break;
                    case 9:
                        Check(ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="Skill HUD").anchoredPosition.y>190,"mobile HUD clears joystick");Button("Active skill R").onClick.Invoke();Check(skill.IsSummoning&&!skill.Active,"HUD button starts Shini summoning");Capture("07-mobile");
                        var dead=player.CaptureRunSceneState();dead.CurrentHealth=0;player.RestoreRunSceneState(dead);break;
                    case 10:
                        Check(!skill.CanActivate&&skill.CooldownRemaining==0,"death clears timers and blocks input");Check(fire.SegmentCount==0,"death clears fire path");typeof(MobileGameplayInput).GetProperty("Active").SetValue(null,false);SessionState.SetBool(Key+"Done",true);break;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key+"Failed",true);}
        }
        static void ForceBurnTick(Ver4NeedleStatus status)
        {
            var list=(IList)PacingPatchTests.Get(status,"burns");
            for(int i=0;i<list.Count;i++){object entry=list[i];var t=entry.GetType();t.GetField("nextTick").SetValue(entry,Time.time-.01f);t.GetField("end").SetValue(entry,Time.time+3);list[i]=entry;}
        }
    }
}
