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
                var runtime=needle.GetCurrentSpecialRuntime();runtime.ver4HitDamage=needle.GetEffectiveDamage();status.ApplyFire(runtime,player,true);status.ApplyFire(runtime,player,true);
                Check(status.BurnStacks==3,"needle and pool share three-stack cap");
                float max=Ver4HitEffects.MaxHealth(target);float expected=needle.GetEffectiveDamage()*5.5f+max*.005f*9;
                hp=target.HP;fire.Detonate(target);Check(Mathf.Abs(hp-target.HP-expected)<.1f,"detonation equals base plus all stacks plus remaining ticks");
                Check(status.BurnStacks==0,"detonation atomically consumes every stack");
                Check(status.ConsumeBurn(out var consumed)==0&&consumed==0,"spent stacks cannot be cashed out twice");
                Check(skill.TryActivate()&&skill.Active&&skill.IsSummoning,"R begins active and phoenix together");
                Check(skill.CooldownRemaining>34.9f,"cooldown starts on activation");
                Check(!skill.TryActivate(),"repeat activation rejected");
                hp=target.HP;yield return new WaitForSeconds(.3f);Check(Mathf.Approximately(hp,target.HP),"tornado does not hit during early growth");
                yield return new WaitForSeconds(.4f);yield return new WaitForEndOfFrame();
                Check(target.HP<hp,"tornado strikes at rising midpoint");
                Check(!skill.IsSummoning&&skill.Active,"short phoenix transitions to active form");
                var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
                Check(skill.Definition.burningIdle.Contains(body.sprite)||skill.Definition.burningWalk.Contains(body.sprite),"burning character appears after embrace");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Shini-lava-tornado.png"));
                fire.SpawnPool(anchor+Vector3.right*2,Time.time);yield return new WaitForEndOfFrame();
                Check(FindObjectsOfType<SpriteRenderer>().Count(x=>x.name=="Shini fire tornado"&&x.enabled)>=3,"new active pool also erupts");
                ui=ApothecaryUI.Instance;ui.OpenRunBook();float remaining=skill.CooldownRemaining;yield return new WaitForSecondsRealtime(.3f);Check(skill.CooldownRemaining==remaining,"TAB freezes all skill timing");ui.CloseRunBook();
                target.gameObject.SetActive(false);skill.Tick(100);yield return new WaitForSeconds(6.2f);Check(fire.PoolCount==0,"pools expire independently after six seconds");
                Vector3 start=player.transform.position;float until=Time.time+2.3f;while(Time.time<until){player.Move(Vector2.right);yield return new WaitForFixedUpdate();}player.Move(Vector2.zero);Check(Vector3.Distance(start,player.transform.position)>.1f,"real movement changes position");Check(fire.PoolCount>=1,"real ordinary movement creates passive pool");
                Check(!FindObjectsOfType<MeshRenderer>().Any(x=>x.name=="Shini fire path"),"old continuous trail is removed");
                var dead=player.CaptureRunSceneState();dead.CurrentHealth=0;player.RestoreRunSceneState(dead);yield return null;
                Check(fire.PoolCount==0&&!skill.CanActivate,"death clears pools and blocks skills");
                Check(!errors,"no native errors");passed=true;
            }
            finally{Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;Debug.Log("[ShiniPlayer] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("[ShiniPlayer] FAIL "+message);Debug.Log("[ShiniPlayer] PASS "+message);}
    }
}
#endif
