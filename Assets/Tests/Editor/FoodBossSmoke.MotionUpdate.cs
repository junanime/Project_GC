using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Vampire.Tests.Editor
{
    public static partial class FoodBossSmoke
    {
        static IEnumerator MotionUpdateReview(Character player,Vector2 origin)
        {
            foreach(var tooltip in Object.FindObjectsOfType<HudTooltip>())tooltip.enabled=false;
            var quest=Object.FindObjectOfType<PrescriptionScrollView>();if(quest!=null)quest.SetExpanded(false);
            Check(GameInput.ActiveSkillKey==KeyCode.Space&&GameInput.ActiveSkillKeyLabel=="Space","Space is the shared active skill binding");
            Time.captureDeltaTime=.04f;
            var args=Environment.GetCommandLineArgs();int selector=Array.IndexOf(args,"-motionBoss");
            var selected=selector>=0&&selector+1<args.Length?args[selector+1].Split(',').Select(int.Parse).ToArray():null;
            foreach(FoodBossKind kind in Enum.GetValues(typeof(FoodBossKind))){
                if(selected!=null&&!selected.Contains((int)kind))continue;
                var boss=FoodBossRuntime.Spawn(kind,player,origin);boss.AutoPatterns=false;
                for(int phase=0;phase<2;phase++){
                    Position(player,origin+Vector2.right*3.5f);
                    if(phase==1){boss.SetPhaseTwoForTest();while(boss.Busy)yield return null;}
                    boss.transform.position=origin;boss.GetComponent<Rigidbody2D>().position=origin;
                    yield return new WaitForSeconds(.25f);
                    Check(boss.Visual.IdlePlaying,"idle starts when inactive "+kind+phase);
                    Check(FoodBossNeedleSurface.HasMask(boss.Visual.Body.sprite),"compressed sprite has opaque hit geometry "+kind+phase);
                    Vector3 restScale=boss.Visual.Body.transform.localScale;Vector3 first=boss.Visual.Body.transform.localPosition;bool changed=false;
                    for(int f=0;f<40;f++){yield return null;changed|=(boss.Visual.Body.transform.localPosition-first).sqrMagnitude>1e-7f;CheckScale(boss,restScale);if(f%2==0)CaptureTang($"idle-{(int)kind}-p{phase+1}-{f/2:D3}",boss,player);}
                    Check(changed,"concept idle animates without resizing "+kind+phase);
                    Time.timeScale=0;var paused=boss.Visual.Body.transform.localPosition;yield return new WaitForSecondsRealtime(.12f);Check(paused==boss.Visual.Body.transform.localPosition,"idle pauses "+kind+phase);Time.timeScale=1;
                    var needle=Object.FindObjectOfType<SyringeDartAbility>();Physics2D.SyncTransforms();float hp=boss.Health;
                    Vector2 aim=(boss.GetComponent<Collider2D>().bounds.center-player.transform.position).normalized;
                    Check((bool)Call(needle,"LaunchAimedSyringe",aim,false,1f,0),"real needle launches at "+kind+phase);
                    float until=Time.time+2;while(boss.Health==hp&&Time.time<until)yield return null;
                    var stuck=boss.Visual.Body.GetComponentInChildren<StuckNeedleVisual>();
                    Check(boss.Health<hp&&stuck!=null,"boss takes hit and retains embedded needle "+kind+phase);
                    Check(stuck.transform.parent==boss.Visual.Body.transform,"needle follows animated body "+kind+phase);
                    CaptureTang($"embedded-{(int)kind}-p{phase+1}",boss,player);
                    if(kind==FoodBossKind.Hurumi){
                        Position(player,origin+(phase==0?Vector2.right*3.3f:Vector2.down*3.3f));boss.Play(FoodBossAttack.UniqueOne);bool joined=false,thin=true;int f=0;
                        while(boss.Busy){yield return null;var tubes=boss.Effects.GetComponentsInChildren<FoodBossCurtainTube>();joined|=boss.Visual.Curtaining&&tubes.Length==23;foreach(var tube in tubes)foreach(var line in tube.GetComponentsInChildren<LineRenderer>())if(line.enabled)thin&=line.widthMultiplier<=.70f;if(f%3==0)CaptureTang($"curtain-p{phase+1}-{f/3:D3}",boss,player);f++;}
                        Check(thin,"curtain outline and fill retain authored thickness "+phase);
                        Check(joined&&!boss.Visual.Curtaining,"two roots and paired emergence/tip lanes restore "+phase);
                        yield return null;Check(boss.Effects.GetComponentsInChildren<FoodBossCurtainTube>().Length==0,"curtain meshes clean up "+phase);
                        Check(boss.Play(FoodBossAttack.UniqueTwo),"approved original whip still starts");while(boss.Busy)yield return null;
                    }
                    if(kind==FoodBossKind.Tangdengi){
                        float expected=phase==0?4.2f:5.082f,px=phase==0?245:257;
                        Check(Mathf.Abs(restScale.x*px/100-expected)<.001f,"beetle original world width retained "+phase);
                        foreach(var attack in new[]{FoodBossAttack.Basic,FoodBossAttack.Radial,FoodBossAttack.Dash,FoodBossAttack.UniqueOne,FoodBossAttack.UniqueTwo}){
                            boss.Play(attack);bool stable=true;int f=0;
                            while(boss.Busy){yield return null;stable&=boss.Visual.Body.sprite.texture.name==$"Walk3P{phase+1}";stable&=!boss.Visual.IdlePlaying;if(f++%16==0)CaptureTang($"beetle-p{phase+1}-{attack}-{f:D3}",boss,player);}
                            Check(stable,"approved beetle art in "+attack+phase);
                        }
                        if(phase==0){
                            Check(boss.Play(FoodBossAttack.Recovery),"approved beetle recovery begins");yield return new WaitForSeconds(.6f);
                            var bands=boss.Visual.Body.GetComponentsInChildren<LineRenderer>().Where(l=>l.name=="Translucent sugar cocoon band").ToArray();
                            Check(bands.Length==7&&bands.All(l=>l.enabled&&l.transform.localPosition==Vector3.zero),"sugar cocoon stays attached to approved body");
                            Check(boss.Visual.Body.sprite==FoodBossArt.Walk(kind,false,17),"recovery keeps approved folded six-leg body");CaptureTang("beetle-final-recovery",boss,player);
                            foreach(var node in boss.Effects.GetComponentsInChildren<FoodBossWeakpoint>())node.TakeDamage(node.Health+1);
                            while(boss.Busy)yield return null;yield return new WaitForSeconds(.5f);
                            Check(bands.All(l=>!l.enabled),"recovery clears sugar wrap");
                        }
                    }else if(kind!=FoodBossKind.Hurumi){boss.Play(FoodBossAttack.Basic);yield return null;yield return null;Check(!boss.Visual.IdlePlaying,"attack overrides idle "+kind+phase);while(boss.Busy)yield return null;}
                }
                Object.Destroy(boss.gameObject);yield return null;Check(Object.FindObjectsOfType<StuckNeedleVisual>().Length==0,"despawn clears embedded needles "+kind);
            }
            Time.captureDeltaTime=0;
        }
        static void CheckScale(FoodBossRuntime boss,Vector3 expected){if(boss.Visual.Body.transform.localScale!=expected)throw new Exception("idle changed boss size");}
    }
}
