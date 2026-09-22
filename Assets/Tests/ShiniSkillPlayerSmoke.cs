#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Vampire.Tests
{
    public sealed class ShiniSkillPlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Environment.GetCommandLineArgs().Contains("-shiniSkillsSmoke"))
            {Application.runInBackground=true;var go=new GameObject("Shini player verification");DontDestroyOnLoad(go);go.AddComponent<ShiniSkillPlayerSmoke>();}
        }
        bool errors;
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors=true;}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;bool passed=false;var original=GamePreferences.Current.Copy();
            try
            {
                var preferences=original.Copy();preferences.pauseOnFocusLoss=false;preferences.muteOnFocusLoss=false;GamePreferences.Apply(preferences,false);
                yield return new WaitForSecondsRealtime(2);
                var ui=ApothecaryUI.Instance;Check(ui!=null,"main loads");
                Check(!ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Active skill slot"||b.name=="Passive skill slot"),"main skill slots removed");
                CrossSceneData.CharacterBlueprint=ui.Config.characters.Single(c=>c.name=="신이");CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                var level=FindObjectOfType<LevelManager>();level.enabled=false;var player=level.PlayerCharacter;var skill=player.Skills;var fire=player.GetComponent<ShiniSkillRuntime>();
                foreach(var monster in FindObjectsOfType<Monster>())monster.gameObject.SetActive(false);
                var needle=FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();
                foreach(var projectile in FindObjectsOfType<Projectile>())projectile.gameObject.SetActive(false);
                Check(needle.GetCurrentSpecialRuntime().ver4.Has(SyringeSpecialAugmentAbility.SpecialAugmentType.FireNeedle),"starts with shared fire needle progression");
                Check(skill.Definition.lavaFrames.Length==24,"approved 24-frame art imported");
                Check(skill.Definition.activeDuration==8&&skill.Definition.cooldown==35,"eight-second active and thirty-five-second cooldown");
                fire.enabled=false;fire.enabled=true;fire.AdvanceMovement(true,1.9f);Check(fire.PoolCount==0,"movement requires two seconds");
                fire.AdvanceMovement(false,20);Check(fire.PoolCount==0,"idle does not advance passive timer");
                fire.AdvanceMovement(true,.11f);Check(fire.PoolCount==1,"movement resumes accumulated timer");
                var ground=FindObjectsOfType<SpriteRenderer>().Single(x=>x.name=="Shini lava pool");Vector3 anchor=ground.transform.position;
                Check(ground.sortingLayerName=="GroundEffects","pool uses ground sorting layer");
                Check(SortingLayer.GetLayerValueFromName("GroundEffects")>SortingLayer.GetLayerValueFromName("Background")&&SortingLayer.GetLayerValueFromName("GroundEffects")<SortingLayer.GetLayerValueFromName("Default"),"ground stays above background and below actors");
                yield return new WaitForSeconds(.4f);Check(ground.transform.position==anchor,"animated pool anchor remains stationary");
                var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];var target=level.EntityManager.SpawnMonster(0,(Vector2)anchor,data,500);target.enabled=false;
                Physics2D.SyncTransforms();float hp=target.HP;fire.UpdatePools(Time.time);
                var status=target.GetComponent<Ver4NeedleStatus>();Check(status!=null&&status.BurnStacks==1,"pool contact guarantees one burn stack");
                Check(Mathf.Approximately(hp,target.HP),"pool has no direct contact damage");
                fire.SpawnPool(anchor,Time.time);fire.UpdatePools(Time.time);Check(status.BurnStacks==1,"overlapping pools share target burn interval");
                yield return new WaitForSeconds(1.1f);
                Check(target.HP<hp,"pool-applied burn actually lowers health after its tick");
                var tint=target.GetComponentsInChildren<SpriteRenderer>().Single(x=>x.name=="Burn orange silhouette");
                var flames=target.GetComponentsInChildren<SpriteRenderer>().Single(x=>x.name=="Body burn flames");
                var monsterBody=tint.transform.parent.GetComponent<SpriteRenderer>();
                Check(tint.enabled&&flames.enabled,"burn keeps flames and adds orange overlay");
                Check(tint.sprite==monsterBody.sprite&&tint.flipX==monsterBody.flipX&&tint.transform.localPosition==Vector3.zero&&tint.transform.localScale==Vector3.one,"burn overlay matches animated monster silhouette");
                Check(tint.sortingOrder==monsterBody.sortingOrder+1&&flames.sortingOrder==tint.sortingOrder+1,"orange layer sits between monster and flames");
                Check(Mathf.Approximately(tint.color.a,.25f)&&tint.sharedMaterial.shader.isSupported,"quarter-opacity orange shader works in player");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Shini-orange-burn.png"));
                status.enabled=false;status.enabled=true;
                var runtime=needle.GetCurrentSpecialRuntime();runtime.ver4HitDamage=needle.GetEffectiveDamage();status.ApplyFire(runtime,player,true);status.ApplyFire(runtime,player,true);
                status.ApplyFire(runtime,player,true);
                Check(status.BurnStacks==3,"needle and pool share three-stack cap");
                float max=Ver4HitEffects.MaxHealth(target);float expected=needle.GetEffectiveDamage()*5.5f+max*.005f*9;
                hp=target.HP;fire.Detonate(target);Check(Mathf.Abs(hp-target.HP-expected)<.1f,"detonation equals base plus all stacks plus remaining ticks");
                Check(status.BurnStacks==0,"detonation atomically consumes every stack");
                Check(status.ConsumeBurn(out var consumed)==0&&consumed==0,"spent stacks cannot be cashed out twice");
                var small=level.EntityManager.SpawnMonster(0,(Vector2)anchor+Vector2.up*5,data,25);small.enabled=false;
                var smallBurn=small.gameObject.AddComponent<Ver4NeedleStatus>();smallBurn.ApplyFire(runtime,player,true);
                float smallHP=small.HP;yield return new WaitForSeconds(1.05f);
                Check(Mathf.Abs(smallHP-small.HP-Ver4HitEffects.MaxHealth(small)*.005f)<.001f,"fractional burn damages low-health monster despite integer popup");
                smallBurn.ConsumeBurn(out _);yield return new WaitForEndOfFrame();
                Check(!small.GetComponent<ShiniBurnVisual>().TintVisible,"orange overlay clears when burn stacks are consumed");
                small.gameObject.SetActive(false);
                Check(skill.TryActivate()&&skill.Active&&skill.IsSummoning,"R begins active and phoenix together");
                Check(skill.CooldownRemaining>34.9f,"cooldown starts on activation");
                Check(!skill.TryActivate(),"repeat activation rejected");
                Check(Mathf.Approximately(skill.MovementMultiplier,1.3f),"active increases movement speed by thirty percent");
                Check(Mathf.Approximately(fire.CurrentSpawnInterval,1),"active halves pool interval");
                int before=fire.PoolCount;fire.AdvanceMovement(true,1.01f);
                Check(fire.PoolCount==before+1,"active movement creates pool after one second");
                Check(FindObjectsOfType<SpriteRenderer>().Any(x=>x.name=="Shini fire tornado"&&x.enabled),"active still erupts existing and new pools");
                hp=target.HP;yield return new WaitForSeconds(.3f);Check(Mathf.Approximately(hp,target.HP),"tornado does not hit during early growth");
                yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
                Check(target.HP<hp,"tornado strikes at rising midpoint");
                Check(!skill.IsSummoning&&skill.Active,"short phoenix transitions to active form");
                var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
                Check(skill.Definition.burningIdle.Contains(body.sprite)||skill.Definition.burningWalk.Contains(body.sprite),"burning character appears after embrace");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Shini-lava-tornado.png"));
                fire.SpawnPool(anchor+Vector3.right*2,Time.time);yield return new WaitForEndOfFrame();
                var newest=FindObjectsOfType<SpriteRenderer>().Single(x=>x.name=="Shini fire tornado"&&Vector3.Distance(x.transform.position,anchor+Vector3.right*2)<.01f);
                Check(newest.enabled,"new active pool still erupts automatically");
                ui=ApothecaryUI.Instance;ui.OpenRunBook();float remaining=skill.CooldownRemaining;yield return new WaitForSecondsRealtime(.3f);Check(skill.CooldownRemaining==remaining,"TAB freezes all skill timing");ui.CloseRunBook();
                target.gameObject.SetActive(false);skill.Tick(100);
                Check(!tint.enabled&&!flames.enabled,"pooled monster clears both burn visual layers");
                Check(skill.MovementMultiplier==1&&fire.CurrentSpawnInterval==2,"active expiry restores speed and spawn interval");
                yield return new WaitForSeconds(6.2f);Check(fire.PoolCount==0,"pools expire independently after six seconds");
                fire.SpawnPool(player.transform.position,Time.time);
                Check(!FindObjectsOfType<SpriteRenderer>().Any(x=>x.name=="Shini fire tornado"&&x.enabled),"ordinary pool waits for activation");
                player.LookDirection=Vector2.right;
                Check(player.TryDash(),"passive dash works without active");
                Check(fire.PoolCount==1&&FindObjectsOfType<SpriteRenderer>().Any(x=>x.name=="Shini fire tornado"&&x.enabled),"dash erupts existing pool without extra spawn");
                Check(!player.TryDash()&&fire.PoolCount==1,"rejected dash does not trigger extra pool");
                yield return new WaitForSeconds(.8f);
                yield return new WaitForSeconds(1);
                player.AddDashCharge(1);Check(player.TryDash(),"second valid dash can reactivate an existing pool");
                Check(FindObjectsOfType<SpriteRenderer>().Any(x=>x.name=="Shini fire tornado"&&x.enabled),"finished eruption restarts on next dash");
                yield return new WaitForSeconds(.8f);
                Vector3 start=player.transform.position;float until=Time.time+2.3f;while(Time.time<until){player.Move(Vector2.right);yield return new WaitForFixedUpdate();}player.Move(Vector2.zero);Check(Vector3.Distance(start,player.transform.position)>.1f,"real movement changes position");Check(fire.PoolCount>=1,"real ordinary movement creates passive pool");
                Check(!FindObjectsOfType<MeshRenderer>().Any(x=>x.name=="Shini fire path"),"old continuous trail is removed");
                var dead=player.CaptureRunSceneState();dead.CurrentHealth=0;player.RestoreRunSceneState(dead);yield return null;
                Check(fire.PoolCount==0&&!skill.CanActivate,"death clears pools and blocks skills");
                // Only the requested Hyuki stack cap; do not change or test his pending motion remake.
                CrossSceneData.CharacterBlueprint=ApothecaryUI.Instance.Config.characters.Single(c=>c.name=="혁이");
                SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                level=FindObjectOfType<LevelManager>();level.enabled=false;player=level.PlayerCharacter;skill=player.Skills;
                foreach(var monster in FindObjectsOfType<Monster>())monster.gameObject.SetActive(false);
                skill.RestoreSleep(59,0);skill.Tick(1000);
                Check(skill.SleepStacks==60&&skill.SleepSeconds==60,"Hyuki idle accumulation caps at sixty stacks");
                skill.Tick(1000);Check(skill.SleepStacks==60,"long idle cannot exceed twelve crystals");
                yield return new WaitForEndOfFrame();
                Check(player.GetComponent<PhoenixSkillVisual>().CrystalCount==12,"capped sleep displays exactly twelve crystals");
                player.Move(Vector2.right);skill.Tick(.01f);
                Check(skill.ConsumedSleepStacks==60&&Mathf.Approximately(skill.MovementMultiplier,5.8f),"consumed sleep bonus caps at plus 480 percent");
                skill.RestoreSleep(999,999);
                Check(skill.SleepStacks==60&&skill.ConsumedSleepStacks==60,"old oversized saved stacks clamp on restore");
                skill.Tick(.01f);skill.Tick(10);Check(skill.MovementMultiplier==1,"capped movement buff still expires");
                Check(!errors,"no native errors");passed=true;
            }
            finally{Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;Debug.Log("[ShiniPlayer] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("[ShiniPlayer] FAIL "+message);Debug.Log("[ShiniPlayer] PASS "+message);}
    }
}
#endif
