#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Vampire.Tests
{
    public sealed class AriSkillPlayerSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Environment.GetCommandLineArgs().Contains("-ariSkillsSmoke"))
            {var go=new GameObject("Ari verification");DontDestroyOnLoad(go);go.AddComponent<AriSkillPlayerSmoke>();}
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
                CrossSceneData.CharacterBlueprint=ApothecaryUI.Instance.Config.characters.Single(c=>c.name=="아리");
                CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();SceneManager.LoadScene(1);
                yield return new WaitForSecondsRealtime(2);
                var level=FindObjectOfType<LevelManager>();level.enabled=false;var player=level.PlayerCharacter;
                foreach(var m in FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
                var skill=player.Skills;var ari=player.GetComponent<AriSkillRuntime>();var needle=FindObjectOfType<SyringeDartAbility>();
                needle.enabled=false;needle.StopAllCoroutines();foreach(var p in FindObjectsOfType<Projectile>())p.gameObject.SetActive(false);
                Check(skill.IsAri&&ari!=null,"selected Ari owns its dedicated runtime");
                Check(needle.HasAcupunctureFormationAugment(),"Ari starts with actual formation effect");
                Check(FindObjectOfType<AbilityManager>().GetComponentsInChildren<SyringeSpecialAugmentAbility>(true).Count(a=>a.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.AcupunctureFormation&&a.Owned)==1,"formation owned exactly once");
                var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
                var target=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*.15f,data,500);target.enabled=false;
                Physics2D.SyncTransforms();player.LookDirection=Vector2.right;float hp=target.HP;
                Check(player.TryDash(),"ordinary roll starts");
                Check(Mathf.Approximately(hp-target.HP,10),"normal body contact deals exactly ten");
                float normalRadius=ari.ContactRadius;ari.Sweep(player.transform.position,player.transform.position);
                Check(Mathf.Approximately(hp-target.HP,10)&&ari.DashHitCount==1,"same target only once per dash");
                Check(Mathf.Approximately(ari.ContactRadius,ari.BaseBodyDiameter*1.1f*.5f),"normal contact diameter is 1.1 times body size");
                Check(target.GetComponent<AriDashPush>()!=null&&Mathf.Approximately(target.GetComponent<AriDashPush>().RemainingDistance,.3f),"ordinary roll schedules exactly 0.3 distance");
                target.gameObject.SetActive(false);
                yield return new WaitForSeconds(.05f);
                Check(FindObjectsOfType<SyringeProjectile>().Length>0,"starting formation actually emits during roll");
                yield return new WaitForSeconds(.4f);
                foreach(var p in FindObjectsOfType<Projectile>())p.gameObject.SetActive(false);
                var controller=FindObjectOfType<AcupunctureFormationController>();if(controller!=null)controller.enabled=false;
                var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
                var rootScale=player.transform.localScale;float baseRecharge=player.DashRechargeTime;
                var collider=player.GetComponent<Collider2D>();Vector2 colliderSize=collider.bounds.size;
                float originalHP=player.CurrentHealth;int beforeActive=player.CurrentDashCharges;
                Check(skill.TryActivate()&&skill.Active&&!skill.IsSummoning&&!skill.IsCutin&&Time.timeScale==1,"active transforms directly without summon lock or cut-in");
                Check(player.CurrentDashCharges>0&&Mathf.Approximately(player.EffectiveDashRechargeTime,.5f),"active grants charge and half-second recharge");
                Check(!skill.TryActivate(),"active cooldown rejects repeat activation");
                Check(player.CurrentHealth==originalHP,"transformation preserves current HP");
                Check(player.CurrentDashCharges==Mathf.Min(player.MaxDashCharges,beforeActive+1),"transformation grants exactly one charge capped at maximum");
                Check(skill.Definition.ariPhoenixIdle.Length==8&&skill.Definition.ariPhoenixWalk.Length==8&&skill.Definition.ariPhoenixDash.Length==8&&skill.Definition.ariPhoenixTransform.Length==8,"all four final animation sets contain eight frames");
                Check(skill.Definition.passiveName=="데굴데굴"&&skill.Definition.activeName=="나 멋지지","final Korean skill names linked");
                Check(skill.Definition.passiveIcon!=null&&skill.Definition.activeIcon!=null&&skill.Definition.passiveIcon!=player.Blueprint.profileSprite&&skill.Definition.activeIcon!=skill.Definition.ariPhoenixIdle[0],"dedicated rolling and adult close-up skill icons linked");
                yield return new WaitForEndOfFrame();Check(ari.FormVisible&&body.forceRenderingOff,"controlled phoenix replaces only visual body");
                var visualScale=ari.VisualScale;
                yield return new WaitForSeconds(.65f);
                Check(skill.Definition.ariPhoenixIdle.Contains(ari.CurrentFormSprite),"transformation settles into final idle animation");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Ari-marble-idle.png"));
                Check(player.transform.localScale==rootScale&&Vector2.Distance(collider.bounds.size,colliderSize)<.0001f,"transformation preserves hurtbox and root scale");
                var start=player.transform.position;player.Move(Vector2.up);yield return new WaitForSeconds(.25f);
                Check(ari.VisualScale==visualScale,"walking never rescales adult form");
                Check(skill.Definition.ariPhoenixWalk.Contains(ari.CurrentFormSprite),"movement uses final walk frames");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Ari-marble-walk.png"));
                player.Move(Vector2.zero);
                Check(player.transform.position.y>start.y+.01f,"directional movement remains controllable while transformed");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Ari-rock-walk.png"));
                target=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*.15f,data,500);target.enabled=false;
                Physics2D.SyncTransforms();player.LookDirection=Vector2.right;hp=target.HP;int charges=player.CurrentDashCharges;
                Check(player.TryDash(),"enhanced dash starts");
                Check(Mathf.Approximately(hp-target.HP,35),"enhanced contact replaces ten with thirty-five");
                Check(Mathf.Approximately(ari.ContactRadius,ari.AdultBodyDiameter*1.3f*.5f),"enhanced contact diameter is 1.3 times adult body size");
                Check(Mathf.Approximately(target.GetComponent<AriDashPush>().RemainingDistance,.6f),"adult roll schedules exactly 0.6 distance");
                target.gameObject.SetActive(false);
                yield return new WaitForEndOfFrame();
                Check(ari.VisualScale==visualScale&&player.CurrentHealth==originalHP&&Vector2.Distance(collider.bounds.size,colliderSize)<.0001f,"dash preserves adult scale HP and hurtbox");
                Check(skill.Definition.ariPhoenixDash.Contains(ari.CurrentFormSprite),"dash uses final airborne wing frames");
                var far=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*8,data,500);far.enabled=false;Physics2D.SyncTransforms();hp=far.HP;
                ari.Sweep(player.transform.position,(Vector2)player.transform.position+Vector2.right*12);
                Check(Mathf.Approximately(hp-far.HP,35),"swept contact does not tunnel past enemies");far.gameObject.SetActive(false);
                skill.Tick(8);Check(!skill.Active&&ari.Transformed,"form stays through an already-started enhanced dash");
                Check(player.EffectiveDashRechargeTime==baseRecharge,"expiry restores original recharge without modifying base stat");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Ari-marble-dash.png"));
                yield return new WaitForSeconds(.3f);yield return new WaitForEndOfFrame();
                Check(ari.FormVisible,"expired enhanced dash finishes before reversion visuals");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Ari-marble-revert.png"));
                yield return new WaitForSeconds(1f);yield return new WaitForEndOfFrame();
                Check(!ari.FormVisible&&!body.forceRenderingOff,"dash end returns to original Ari appearance");
                Check(player.CurrentHealth==originalHP&&Vector2.Distance(collider.bounds.size,colliderSize)<.0001f,"reversion preserves HP and hurtbox");
                player.RefreshSkillDashRecharge(true);Check(player.TryDash(),"normal roll works after reversion");
                yield return new WaitForEndOfFrame();
                Check(player.Blueprint.dashSpriteSequence.Contains(body.sprite)&&!ari.FormVisible,"normal passive retains existing egg-roll artwork");
                yield return new WaitForSeconds(.5f);
                // Physical distance measurement isolated from AI and player collision, not just requested values.
                var probe=new GameObject("Ari push distance probe");probe.AddComponent<CircleCollider2D>().radius=.05f;
                var rb=probe.AddComponent<Rigidbody2D>();rb.gravityScale=0;rb.drag=0;
                probe.transform.position=(Vector2)player.transform.position+Vector2.up*5;
                Physics2D.SyncTransforms();yield return new WaitForFixedUpdate();
                var push=probe.AddComponent<AriDashPush>();Vector2 probeStart=rb.position;
                push.Begin(rb,Vector2.right,.3f);yield return new WaitForSeconds(.2f);
                Check(Mathf.Abs(rb.position.x-probeStart.x-.3f)<.005f,"normal knockback physically travels 0.3; measured="+(rb.position.x-probeStart.x));
                probeStart=rb.position;push.Begin(rb,Vector2.right,.6f);yield return new WaitForSeconds(.2f);
                Check(Mathf.Abs(rb.position.x-probeStart.x-.6f)<.005f,"adult knockback physically travels 0.6; measured="+(rb.position.x-probeStart.x));
                var wall=new GameObject("Ari push wall probe");wall.transform.position=rb.position+Vector2.right*.25f;
                wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,1);
                Physics2D.SyncTransforms();probeStart=rb.position;push.Begin(rb,Vector2.right,.6f);yield return new WaitForSeconds(.2f);
                Check(rb.position.x-probeStart.x<.2f,"push stops before solid wall");Destroy(wall);
                Destroy(probe);
                skill.Restore(0,4,20);yield return new WaitForEndOfFrame();
                Check(ari.FormVisible&&Mathf.Approximately(player.EffectiveDashRechargeTime,.5f),"active form and fast recharge restore from run state");
                player.RefreshSkillDashRecharge(true);Check(player.TryDash(),"restored active can dash");
                int afterDash=player.CurrentDashCharges;yield return new WaitForSeconds(.6f);
                Check(player.CurrentDashCharges>afterDash,"half-second recharge actually refills charge");
                Time.timeScale=0;float remaining=skill.ActiveRemaining;yield return new WaitForSecondsRealtime(.1f);
                Check(skill.ActiveRemaining==remaining&&!player.TryDash(),"pause freezes buff and rejects dash");Time.timeScale=1;
                var state=player.CaptureRunSceneState();state.CurrentHealth=0;player.RestoreRunSceneState(state);yield return new WaitForEndOfFrame();
                Check(!ari.FormVisible&&!skill.CanActivate,"death clears form and disables active");
                Check(!errors,"no native runtime errors");passed=true;
            }
            finally{Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;Debug.Log("[AriPlayer] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("[AriPlayer] FAIL "+message);Debug.Log("[AriPlayer] PASS "+message);}
    }
}
#endif
