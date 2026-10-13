#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Vampire.Tests
{
 public sealed class ShiniEmberPlayerSmoke:MonoBehaviour
 {
  float deadline;bool failed;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot(){if(Environment.GetCommandLineArgs().Contains("-shiniEmberSmoke")&&FindObjectOfType<ShiniEmberPlayerSmoke>()==null){var go=new GameObject("Shini ember player test");DontDestroyOnLoad(go);go.AddComponent<ShiniEmberPlayerSmoke>();}}
  void Awake(){deadline=Time.realtimeSinceStartup+90;Application.runInBackground=true;Application.logMessageReceived+=Log;}
  void Log(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)failed=true;}
  void Update(){if(failed||Time.realtimeSinceStartup>deadline)Application.Quit(1);}
  void Check(bool ok,string label){if(!ok)throw new Exception(label);Debug.Log("EMBER_PLAYER_CHECK "+label);}
  IEnumerator Start()
  {
   var prefs=GamePreferences.Current.Copy();var test=prefs.Copy();test.pauseOnFocusLoss=false;test.muteOnFocusLoss=false;GamePreferences.Apply(test,false);
   yield return new WaitForSecondsRealtime(2);
   var ui=ApothecaryUI.Instance;Check(ui!=null,"lobby loads");CrossSceneData.CharacterBlueprint=ui.Config.characters.Single(c=>c.skills!=null&&c.skills.kind==CharacterSkillDefinition.SkillKind.Shini);
   CrossSceneData.ClearStartingLobbyItems();CrossSceneData.ClearPendingRunSceneTransfer();SceneManager.LoadScene(1);yield return new WaitForSecondsRealtime(3);
   var level=FindObjectOfType<LevelManager>();Check(level?.PlayerCharacter?.Skills?.Breath!=null,"Shini selected and runtime linked in Windows player");
   var tutorial=FindObjectOfType<TutorialGuide>();if(tutorial!=null){tutorial.Close();tutorial.enabled=false;}Time.timeScale=1;ui=ApothecaryUI.Instance;
   var breath=level.PlayerCharacter.Skills.Breath;breath.RestoreFuel(24);
   var button=FindObjectsOfType<Button>().First(b=>b.name=="Active skill R");var input=button.GetComponent<ShiniSkillButton>();
   var e=new PointerEventData(EventSystem.current){pointerId=7,button=PointerEventData.InputButton.Left};input.OnPointerDown(e);yield return new WaitForSeconds(.7f);
   Check(breath.Fuel==18&&breath.Busy,"actual HUD press consumes six and inflates chest");ScreenCapture.CaptureScreenshot(Application.dataPath+"/ember-player-chest.png");
   yield return new WaitForSeconds(.6f);Check(breath.Emitting,"continuous fan renders in player");ScreenCapture.CaptureScreenshot(Application.dataPath+"/ember-player-flame.png");
   yield return new WaitForSeconds(1.5f);Check(breath.Fuel<18,"hold consumes extra fuel");
   var reward=level.EntityManager.AbilitySelectionDialog;reward.Open();int fuel=breath.Fuel;input.OnPointerExit(e);
   yield return new WaitForSecondsRealtime(.3f);Check(reward.MenuOpen&&breath.Emitting&&breath.Fuel==fuel,"reward freezes ongoing fire and fuel in player");
   reward.Close();yield return new WaitForSeconds(.27f);Check(breath.Emitting&&breath.Fuel<fuel,"existing hold resumes after reward in player");
   level.PlayerCharacter.LookDirection=Vector2.left;yield return null;yield return null;
   var fire=level.PlayerCharacter.GetComponentsInChildren<SpriteRenderer>().Single(x=>x.name=="Shini continuous fan fire");
   Check(Vector2.Dot(fire.transform.right,Vector2.left)>.999f,"fire turns with player during hold");
   input.OnPointerUp(e);yield return new WaitForSeconds(.6f);Check(!breath.Busy,"release recovers without stuck input");
   Check(button.GetComponent<ApothecaryButtonFeedback>().sparkle!=null,"original gold highlight component remains");
   breath.RestoreFuel(6);input.OnPointerDown(e);input.OnPointerUp(e);button.onClick.Invoke();Check(breath.Fuel==0,"tap click has no duplicate cost");yield return new WaitForSeconds(3);Check(!breath.Busy,"last-fuel tap completes");
   ui.OpenSkillHelp();yield return null;Check(ui.Page=="run","skill help opens existing run panel");GamePreferences.Apply(prefs,false);
   Debug.Log("EMBER_PLAYER_PASS");yield return new WaitForSecondsRealtime(.3f);Application.Quit(0);
  }
 }
}
#endif
