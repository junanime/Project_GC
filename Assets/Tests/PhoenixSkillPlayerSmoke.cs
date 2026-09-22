#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Vampire.Tests
{
    public sealed class PhoenixSkillPlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Environment.GetCommandLineArgs().Contains("-phoenixSkillsSmoke")||Environment.GetCommandLineArgs().Contains("-hyukiTimingSmoke"))
            {var go=new GameObject("Phoenix player verification");DontDestroyOnLoad(go);go.AddComponent<PhoenixSkillPlayerSmoke>();}
        }
        bool errors;
        void Log(string m,string s,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors=true;}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;bool passed=false;var original=GamePreferences.Current.Copy();
            try
            {
                var prefs=original.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
                yield return new WaitForSecondsRealtime(2);
                var config=ApothecaryUI.Instance.Config;
                foreach(string name in new[]{"혁이"})
                {
                    CrossSceneData.CharacterBlueprint=config.characters.Single(c=>c.name==name);CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                    SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                    var level=FindObjectOfType<LevelManager>();level.enabled=false;var player=level.PlayerCharacter;var skill=player.Skills;
                    foreach(var m in FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
                    var needle=FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();
                    foreach(var projectile in FindObjectsOfType<Projectile>())projectile.gameObject.SetActive(false);
                    var v=player.GetComponent<PhoenixSkillVisual>();
                    Monster target=null;
                    if(skill.IsHyuki)
                    {
                        bool timingOnly=Environment.GetCommandLineArgs().Contains("-hyukiTimingSmoke");
                        if(!timingOnly)
                        {
                        skill.RestoreSleep(10,0);yield return new WaitForEndOfFrame();Check(v.CrystalCount==2,"native two stack crystals");
                        Check(player.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name=="Sleep stack crystal").All(x=>x.sprite==skill.Definition.iceComponents[0]),"new diamond art replaces prison-shaped stack crystals");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"Hyuki-polished-idle.png"));
                        player.Move(Vector2.right);yield return new WaitForSeconds(.25f);yield return new WaitForEndOfFrame();
                        var snow=player.GetComponent<HyukiSnowVisual>();Check(v.CrystalCount==0&&snow.WakeCount>0&&!snow.DashingWake,"walking uses drifting snow without orbiting crystals");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"Hyuki-polished-walk.png"));
                        Check(player.TryDash(),"Hyuki dash starts");yield return new WaitForSeconds(.04f);yield return new WaitForEndOfFrame();
                        Check(v.CrystalCount==0&&snow.DashingWake&&snow.WakeCount>0,"dash has ice wake without crystal orbit");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"Hyuki-polished-dash.png"));
                        yield return new WaitForSeconds(.4f);player.Move(Vector2.zero);yield return new WaitForSeconds(.1f);
                        }
                        var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];target=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*2,data,500);target.enabled=false;
                        Physics2D.SyncTransforms();
                        if(!timingOnly)yield return IceChecks(target,needle.GetCurrentSpecialRuntime(),player);
                    }
                    Check(skill.TryActivate(),name+" native skill activation");
                    if(skill.IsHyuki)
                    {
                        Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"no freeze before blizzard appears");
                        yield return new WaitForSeconds(.7f);
                        Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true&&v.BlizzardStrength>0,"snow builds before freeze");
                        float pending=skill.SummonRemaining;
                        Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);
                        Check(skill.SummonRemaining==pending,"pause holds delayed freeze");Time.timeScale=1;
                        var saved=player.CaptureRunSceneState();player.RestoreRunSceneState(saved);
                        Check(Mathf.Abs(skill.SummonRemaining-pending)<.001f,"pending freeze survives run restore");
                        yield return new WaitForSeconds(pending+.08f);yield return new WaitForEndOfFrame();
                        Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceRemaining>4.7f&&v.BlizzardStrength>.8f,"freeze begins as full blizzard starts fading");
                        Check(player.GetComponent<HyukiSnowVisual>().StormCount>=100,"storm combines snowflakes and ice fragments");
                    }
                    if(skill.IsShini)Check(skill.IsSummoning&&!skill.Active&&skill.CooldownRemaining==0,"Shini summon delays both timers");
                    for(int i=0;i<15;i++)
                    {
                        yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
                        if(i==2)foreach(var r in player.GetComponentsInChildren<Renderer>())if(r.sharedMaterial!=null)Check(r.sharedMaterial.shader.isSupported,name+" shader supported: "+r.sharedMaterial.shader.name);
                        if(skill.IsShini&&i==11)
                        {
                            var wrap=player.GetComponent<ShiniPhoenixWrapVisual>();
                            Check(wrap.WingsVisible&&wrap.CorrectLayerOrder&&wrap.WrapProgress>.7f,"crossing wings occlude Shini while phoenix body stays behind");
                        }
                        if(i==2||i==5||i==11)ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,(skill.IsHyuki?"Hyuki":"Shini")+"-phoenix-"+i+".png"));
                    }
                    if(skill.IsHyuki)
                    {
                        Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen==true,"freeze persists beyond old three-second duration");
                        yield return new WaitForSeconds(2.2f);Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"freeze releases five seconds after delayed application");
                        var saved=player.CaptureRunSceneState();player.RestoreRunSceneState(saved);
                        yield return new WaitForSeconds(.1f);
                        Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"restore after release does not repeat freeze");
                        skill.Restore(0,0,0);Check(skill.TryActivate(),"second cast available after test reset");
                        skill.enabled=false;skill.enabled=true;yield return new WaitForSeconds(1.8f);
                        Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"disable cancels pending freeze");
                    }

                }
                Check(!errors,"native session has no errors");passed=true;
            }
            finally{Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;Debug.Log("[PhoenixPlayer] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
        static void SeedProc(bool proc)
        {
            for(int seed=0;seed<10000;seed++)
            { UnityEngine.Random.InitState(seed);if((UnityEngine.Random.value<.05f)==proc){UnityEngine.Random.InitState(seed);return;} }
            throw new Exception("Cannot select deterministic random seed");
        }
        static IEnumerator IceChecks(Monster target,SyringeSpecialRuntime runtime,Character player)
        {
            var random=UnityEngine.Random.state;var rb=target.GetComponent<Rigidbody2D>();float originalDrag=rb.drag;
            for(int n=1;n<=3;n++)
            {
                SeedProc(false);Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
                var chill=target.GetComponent<IceChillStatus>();
                Check(chill.Stacks==n,"ice hit adds exactly one stack "+n);
                Check(Mathf.Abs(chill.SpeedMultiplier-(.9f-.1f*n))<.001f,"ice uses original speed ratio "+n);
                Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"first three non-proc hits do not freeze");
                Check(Mathf.Abs(rb.drag-originalDrag/chill.SpeedMultiplier)<.01f,"slow does not compound across hits");
                yield return new WaitForEndOfFrame();
                var visual=target.GetComponent<IceChillVisual>();
                Check(visual.Visible&&visual.DisplayedSprite==SyringeAugmentVfx.FindTarget(target).sprite,"shared tint follows current monster silhouette");
                ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"Ice-slow-stack-"+n+".png"));
                yield return new WaitForSeconds(.2f);
            }
            SeedProc(false);Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
            Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceRemaining>4.9f,"fourth hit guarantees five-second freeze");
            Check(target.GetComponent<IceChillStatus>().Stacks==0&&Mathf.Abs(rb.drag-originalDrag)<.01f,"freeze consumes stacks and restores chill drag");
            yield return new WaitForEndOfFrame();Check(!target.GetComponent<IceChillVisual>().Visible,"slow overlay hides during freeze");
            Check(Mathf.Abs(Ver4HitEffects.BeforeHit(target,runtime)-1.2f)<.001f,"existing shatter bonus preserved");
            SeedProc(false);Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
            Check(target.GetComponent<IceChillStatus>().Stacks==1,"shatter hit starts next stack cycle without ignored hits");
            target.GetComponent<IceChillStatus>().Clear();
            SeedProc(true);Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
            Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen==true,"five-percent roll can freeze on first hit");
            target.GetComponent<NeuralBlockedMonsterStatus>().ReleaseIce();
            SeedProc(false);Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
            var honey=target.gameObject.AddComponent<HoneySlowStatus>();honey.Apply(.1f,.8f);
            yield return new WaitForSeconds(.2f);
            var state=target.GetComponent<IceChillStatus>();
            Check(state.Stacks==1&&Mathf.Abs(rb.drag-originalDrag/.8f)<.01f,"honey expiry preserves independent ice slow");
            IceSkillRules.Freeze(target);Check(state.Stacks==0,"Hyuki direct freeze consumes chill stacks");
            target.GetComponent<NeuralBlockedMonsterStatus>().ReleaseIce();
            SeedProc(false);Ver4HitEffects.AfterHit(target,runtime,player,~0,false);
            target.gameObject.SetActive(false);Check(state.Stacks==0&&state.SpeedMultiplier==1,"pooled monster clears chill");
            target.gameObject.SetActive(true);target.enabled=false;yield return new WaitForEndOfFrame();yield return null;
            Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen!=true,"pooled monster has no remaining freeze before next cast");
            UnityEngine.Random.state=random;
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("[PhoenixPlayer] FAIL "+message);Debug.Log("[PhoenixPlayer] PASS "+message);}
    }
}
#endif
