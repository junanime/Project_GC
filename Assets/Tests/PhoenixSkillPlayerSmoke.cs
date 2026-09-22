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
            if(Environment.GetCommandLineArgs().Contains("-phoenixSkillsSmoke"))
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
                foreach(string name in new[]{"혁이","신이"})
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
                        var data=level.CurrentLevelBlueprint.monsters[0].monsterBlueprints[0];target=level.EntityManager.SpawnMonster(0,(Vector2)player.transform.position+Vector2.right*2,data,500);target.enabled=false;
                    }
                    Check(skill.TryActivate(),name+" native skill activation");
                    if(skill.IsHyuki)Check(target.GetComponent<NeuralBlockedMonsterStatus>()?.IceRemaining>4.9f,"active initially applies five-second freeze");
                    if(skill.IsShini)Check(skill.IsSummoning&&!skill.Active&&skill.CooldownRemaining==0,"Shini summon delays both timers");
                    for(int i=0;i<15;i++)
                    {
                        yield return new WaitForSeconds(.2f);yield return new WaitForEndOfFrame();
                        if(i==2)foreach(var r in player.GetComponentsInChildren<Renderer>())if(r.sharedMaterial!=null)Check(r.sharedMaterial.shader.isSupported,name+" shader supported: "+r.sharedMaterial.shader.name);
                        if(skill.IsHyuki&&i==5)Check(player.GetComponent<HyukiSnowVisual>().StormCount>=100,"storm combines large snowflakes and ice fragments");
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
                        yield return new WaitForSeconds(2.2f);Check(target.GetComponent<NeuralBlockedMonsterStatus>()==null,"freeze releases at five seconds");
                    }
                    else
                    {
                        Check(skill.Active&&skill.ActiveRemaining>14&&skill.CooldownRemaining>34,"Shini timers begin after phoenix animation");
                        player.Move(Vector2.up);yield return new WaitForSeconds(1.2f);player.Move(Vector2.zero);yield return new WaitForEndOfFrame();
                        var mesh=FindObjectsOfType<MeshFilter>().First(f=>f.name=="Shini fire path"&&f.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Vampire/PhoenixFire").sharedMesh;
                        var positions=mesh.vertices;var uv=mesh.uv;bool continuous=true;
                        for(int i=0;i+7<positions.Length;i+=4)continuous&=Vector3.Distance(positions[i+1],positions[i+4])<.001f&&Vector2.Distance(uv[i+1],uv[i+4])<.001f;
                        Check(continuous,"vertical fire endpoints and UVs join without gaps");ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"Shini-vertical-fire.png"));
                        yield return null;
                    }
                }
                Check(!errors,"native session has no errors");passed=true;
            }
            finally{Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;Debug.Log("[PhoenixPlayer] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("[PhoenixPlayer] FAIL "+message);Debug.Log("[PhoenixPlayer] PASS "+message);}
    }
}
#endif
