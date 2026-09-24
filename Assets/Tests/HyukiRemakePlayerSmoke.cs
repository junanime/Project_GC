#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Vampire.Tests
{
    public sealed class HyukiRemakePlayerSmoke:MonoBehaviour
    {
        bool errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Environment.GetCommandLineArgs().Contains("-hyukiRemakeSmoke")){var go=new GameObject("Hyuki remake verification");DontDestroyOnLoad(go);go.AddComponent<HyukiRemakePlayerSmoke>();}}
        void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors=true;}
        static void Check(bool ok,string message){if(!ok)throw new Exception("[HyukiRemake] FAIL "+message);Debug.Log("[HyukiRemake] PASS "+message);}
        static void Seed(bool success,float chance){for(int i=0;i<10000;i++){UnityEngine.Random.InitState(i);if((UnityEngine.Random.value<chance)==success){UnityEngine.Random.InitState(i);return;}}throw new Exception("No deterministic seed");}
        static void Capture(string name)
        {
            // A hidden/batch player has a black backbuffer; render the actual scene camera explicitly.
            var camera=Camera.main;var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,name+".png"),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
        }
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;bool passed=false;var prefs=GamePreferences.Current.Copy();var random=UnityEngine.Random.state;
            try
            {
                var p=prefs.Copy();p.pauseOnFocusLoss=false;p.muteOnFocusLoss=false;GamePreferences.Apply(p,false);
                yield return new WaitForSecondsRealtime(2);
                CrossSceneData.CharacterBlueprint=ApothecaryUI.Instance.Config.characters.Single(c=>c.name=="혁이");CrossSceneData.StartingLobbyItems=Array.Empty<MerchantItemBlueprint>();
                SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                var level=FindObjectOfType<LevelManager>();level.enabled=false;var player=level.PlayerCharacter;var skill=player.Skills;
                foreach(var m in FindObjectsOfType<Monster>())m.gameObject.SetActive(false);
                var needle=FindObjectOfType<SyringeDartAbility>();needle.enabled=false;needle.StopAllCoroutines();foreach(var projectile in FindObjectsOfType<Projectile>())projectile.gameObject.SetActive(false);
                var runtime=needle.GetCurrentSpecialRuntime();
                Check(skill.Definition.passiveName=="하다보면"&&skill.Definition.passiveIcon.name=="HyukiHadaBomyeon","new passive name and icon connected");
                Check(runtime.ver4.Has(SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle),"starts with ice needle");
                Check(player.Blueprint.idleSpriteSequence.Length==8&&player.Blueprint.dashSpriteSequence.Length==8,"eight idle and dash frames");
                float ppu=player.Blueprint.walkSpriteSequence[0].pixelsPerUnit;
                Check(player.Blueprint.idleSpriteSequence.Concat(player.Blueprint.dashSpriteSequence).All(s=>Mathf.Abs(s.pixelsPerUnit-ppu)<.001f),"walk idle dash share exact pixel scale");
                Check(player.Blueprint.idleSpriteSequence.All(s=>s.pivot==player.Blueprint.idleSpriteSequence[0].pivot),"all idle frames share fixed ground pivot");
                var transformScale=player.transform.localScale;var collider=player.GetComponent<Collider2D>();var colliderSize=collider.bounds.size;float hp=player.CurrentHealth;
                skill.RestoreSleep(999,999);skill.Tick(500);yield return null;
                Check(skill.SleepStacks==0&&skill.MovementMultiplier==1&&player.GetComponent<PhoenixSkillVisual>().CrystalCount==0,"retired sleep speed and crystals removed");
                Check(Mathf.Approximately(skill.IceProcChance,.10f),"base instant chance ten percent");
                var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];
                var a=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*2,data,500);a.enabled=false;
                for(int n=1;n<=4;n++)
                {
                    Seed(false,skill.IceProcChance);Ver4HitEffects.AfterHit(a,runtime,player,~0,false);
                    Check(skill.IceProcFailures==n&&Mathf.Abs(skill.IceProcChance-(.10f+n*.01f))<.0001f,"failed instant roll increases chance "+n);
                    Check(n==4?a.GetComponent<NeuralBlockedMonsterStatus>()?.IceFrozen==true:a.GetComponent<IceChillStatus>().Stacks==n,"independent four-stack freeze "+n);
                }
                Check(a.GetComponent<IceChillStatus>().Stacks==0&&skill.IceProcFailures==4,"guaranteed freeze consumes target stacks but not failures");
                a.GetComponent<IceChillStatus>().Hit(0,player);Check(skill.IceProcFailures==4,"already frozen target does not add failed roll");
                var b=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.left*2,data,500);b.enabled=false;
                Seed(false,skill.IceProcChance);Ver4HitEffects.AfterHit(b,runtime,player,~0,false);
                Check(skill.IceProcFailures==5&&b.GetComponent<IceChillStatus>().Stacks==1,"failure counter belongs to player across targets");
                IceSkillRules.Freeze(b);Check(skill.IceProcFailures==5,"direct active freeze does not reset chance");b.GetComponent<NeuralBlockedMonsterStatus>().ReleaseIce();
                Seed(true,skill.IceProcChance);Ver4HitEffects.AfterHit(b,runtime,player,~0,false);
                Check(b.GetComponent<NeuralBlockedMonsterStatus>().IceFrozen&&skill.IceProcFailures==0&&Mathf.Approximately(skill.IceProcChance,.1f),"instant success resets to ten percent");
                skill.RestoreIceProcFailures(12);var snapshot=player.CaptureRunSceneState();skill.RestoreIceProcFailures(0);player.RestoreRunSceneState(snapshot);
                Check(skill.IceProcFailures==12,"scene snapshot restores independent counter");
                var old=new RunSceneCharacterSnapshot();Check(old.SkillIceProcFailures==0,"legacy saves default new counter to zero");
                skill.RestoreIceProcFailures(999);Check(skill.IceProcFailures==90&&skill.IceProcChance==1,"chance capped at one hundred percent");Check(skill.RollInstantFreeze(1)&&skill.IceProcFailures==0,"one hundred percent guarantees success and resets");
                skill.RestoreIceProcFailures(-3);Check(skill.IceProcFailures==0,"negative save counter clamps");
                skill.RestoreIceProcFailures(8);Time.timeScale=0;yield return new WaitForSecondsRealtime(.2f);Check(skill.IceProcFailures==8,"pause and idle do not alter counter");Time.timeScale=1;
                player.Move(Vector2.zero);player.StartIdleAnimation();yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();Capture("Hyuki-remake-idle");
                player.Move(Vector2.right);yield return new WaitForSeconds(.12f);player.AddDashCharge(1);Check(player.TryDash(),"ice-board dash starts");
                yield return null;yield return new WaitForEndOfFrame();
                var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();Check(player.Blueprint.dashSpriteSequence.Contains(body.sprite),"dash uses approved ice-board art");Capture("Hyuki-remake-dash");
                Check(player.transform.localScale==transformScale&&(collider.bounds.size-colliderSize).sqrMagnitude<.000001f&&player.CurrentHealth==hp,"art swap preserves transform hurtbox and hp");
                yield return new WaitForSeconds(.5f);player.Move(Vector2.zero);yield return new WaitForSeconds(.2f);Check(player.Blueprint.idleSpriteSequence.Contains(body.sprite),"dash returns to fixed-foot idle");
                skill.enabled=false;skill.enabled=true;Check(skill.IceProcFailures==0,"disable clears counter");
                CrossSceneData.CharacterBlueprint=ApothecaryUI.Instance.Config.characters.Single(c=>c.name=="아시");SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(2);
                var other=FindObjectOfType<LevelManager>().PlayerCharacter.Skills;
                Check(!other.RollInstantFreeze(.99f)&&other.IceProcFailures==0&&Mathf.Approximately(other.IceProcChance,.1f),"other characters do not accumulate Hyuki failures");
                Check(!errors,"native run has no runtime errors");passed=true;
            }
            finally{Time.timeScale=1;UnityEngine.Random.state=random;GamePreferences.Apply(prefs,false);Application.logMessageReceived-=Log;Debug.Log("[HyukiRemake] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
    }
}
#endif
