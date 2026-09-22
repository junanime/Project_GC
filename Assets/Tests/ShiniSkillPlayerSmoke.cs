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
            {var go=new GameObject("Shini player verification");DontDestroyOnLoad(go);go.AddComponent<ShiniSkillPlayerSmoke>();}
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
                var level=FindObjectOfType<LevelManager>();level.enabled=false;var player=level.PlayerCharacter;var skill=player.Skills;var trail=player.GetComponent<ShiniSkillRuntime>();
                foreach(var monster in FindObjectsOfType<Monster>())monster.gameObject.SetActive(false);
                var needle=FindObjectOfType<SyringeDartAbility>();needle.enabled=false;
                Check(needle.GetCurrentSpecialRuntime().ver4.Has(SyringeSpecialAugmentAbility.SpecialAugmentType.FireNeedle),"starting fire progression");
                Check(player.TryDash(),"dash starts");yield return new WaitForSeconds(.35f);Check(trail.SegmentCount>0,"dash leaves real trail");
                Check(skill.TryActivate()&&!skill.IsCutin,"active starts immediately");player.Move(Vector2.right);yield return new WaitForSeconds(.5f);player.Move(Vector2.zero);
                Check(skill.IsSummoning&&!skill.Active&&skill.CooldownRemaining==0,"native summon defers buff and cooldown");
                var renderer=FindObjectsOfType<MeshRenderer>().First(r=>r.name=="Shini fire path");
                Check(renderer.sharedMaterial.shader.name=="Vampire/ShiniFireTrail"&&renderer.sharedMaterial.shader.isSupported,"fire shader included and supported");
                Check(renderer.sharedMaterial.mainTexture!=null,"animated fire texture loaded");
                foreach(var direction in new[]{Vector3.left,Vector3.up,Vector3.down})
                {
                    trail.Sample(player.transform.position,player.transform.position+direction,true,Time.time);
                    bool rises=true;
                    foreach(var meshFilter in FindObjectsOfType<MeshFilter>().Where(f=>f.name=="Shini fire path"))
                    {
                        var vertices=meshFilter.sharedMesh.vertices;
                        for(int i=0;i<vertices.Length;i+=4)
                            rises &= meshFilter.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Vampire/PhoenixFire"
                                ? Mathf.Abs(vertices[i+2].x-vertices[i].x-1.2f)<.001f && Mathf.Abs(vertices[i+2].y-vertices[i].y)<.001f
                                : Mathf.Abs(vertices[i+2].y-vertices[i].y-.6f)<.001f;
                    }
                    Check(rises,"flame rises in world up for "+direction);
                }
                yield return new WaitForSeconds(2.8f);
                yield return new WaitForEndOfFrame();
                var body=player.GetComponentInChildren<SpriteAnimator>().GetComponent<SpriteRenderer>();
                Check(skill.Definition.burningIdle.Contains(body.sprite)||skill.Definition.burningWalk.Contains(body.sprite),"transformed character rendered");
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"Shini-native-proof.png"));yield return null;
                skill.Tick(100);yield return new WaitForSeconds(3.2f);Check(trail.SegmentCount==0,"native trail expires");
                Check(!errors,"no native errors");passed=true;
            }
            finally{Time.timeScale=1;GamePreferences.Apply(original,false);Application.logMessageReceived-=Log;Debug.Log("[ShiniPlayer] FINISHED passed="+passed);Application.Quit(passed?0:1);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception("[ShiniPlayer] FAIL "+message);Debug.Log("[ShiniPlayer] PASS "+message);}
    }
}
#endif
