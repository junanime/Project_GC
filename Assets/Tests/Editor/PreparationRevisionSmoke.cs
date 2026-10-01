using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class PreparationRevisionSmoke
    {
        const string Key="PreparationRevisionSmoke";
        const string Output="Library/PreparationRevisionProof";
        const string BackupPath="Library/PreparationRevisionSave.json";
        static double deadline;
        static bool launched;
        [Serializable] class Save {public string key;public bool exists;public int value;}
        [Serializable] class Backup {public Save[] values;public string settings;public bool hasSettings;}
        static PreparationRevisionSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void RefreshAndRun(){Vampire.EditorTools.PreparationRevisionInstaller.Install();Run();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var config=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            var keys=config.characters.Select(StartingNeedleSelection.SaveKey)
                .Concat(config.characters.Select(c=>"LobbyUnlock.Character."+c.name))
                .Concat(config.weapons.Select(w=>"LobbyUnlock.Needle."+w.Type))
                .Concat(new[]{"LobbySilverCoins","Coins"}).Distinct().ToArray();
            File.WriteAllText(BackupPath,JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Save{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray(),settings=PlayerPrefs.GetString(GamePreferences.SaveKey),hasSettings=PlayerPrefs.HasKey(GamePreferences.SaveKey)}));
            foreach(var c in config.characters){PlayerPrefs.DeleteKey(StartingNeedleSelection.SaveKey(c));PlayerPrefs.DeleteKey("LobbyUnlock.Character."+c.name);}
            foreach(var w in config.weapons)PlayerPrefs.DeleteKey("LobbyUnlock.Needle."+w.Type);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scripts/boyoung/test/Main Menu.unity");
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+240;launched=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}}
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Revision smoke timeout");SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                var b=JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath));
                foreach(var v in b.values){if(v.exists)PlayerPrefs.SetInt(v.key,v.value);else PlayerPrefs.DeleteKey(v.key);}
                if(b.hasSettings)PlayerPrefs.SetString(GamePreferences.SaveKey,b.settings);else PlayerPrefs.DeleteKey(GamePreferences.SaveKey);
                PlayerPrefs.Save();SessionState.SetBool(Key,false);
                Debug.Log("[PreparationRevision] FINISHED failed="+SessionState.GetBool(Key+"Failed",false));
                EditorApplication.Exit(SessionState.GetBool(Key+"Failed",false)?1:0);return;
            }
            if(!launched&&EditorApplication.isPlaying&&ApothecaryUI.Instance!=null)
            {
                launched=true;var host=new GameObject("Preparation revision QA");Object.DontDestroyOnLoad(host);
                host.AddComponent<PreparationRevisionRunner>().StartCoroutine(CheckAll());
            }
        }
        static void Check(bool condition,string message)
        {if(!condition)throw new Exception("[PreparationRevision] FAIL "+message);Debug.Log("[PreparationRevision] PASS "+message);}
        static Button Named(string name)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.name==name);
        static void Click(string name)
        {
            var button=Named(name);Check(button.IsInteractable(),"Enabled "+name);
            Canvas.ForceUpdateCanvases();var ev=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            Check(hits.Count>0&&hits[0].gameObject==button.gameObject,"Raycast reaches "+name);
            button.onClick.Invoke();
        }
        static IEnumerator Settle(){yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();}
        static void Capture(string name)
        {var tex=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());Object.Destroy(tex);}
        static IEnumerator E()
        {
            if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
            // Hidden batch editors do not dispatch keyboard state in their regular focused GameView loop.
            // Pump the actual input backend, then exercise the normal Update handler in that input frame.
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(UnityEngine.InputSystem.Key.E));InputSystem.Update();
            Check(GameInput.GetKeyDown(KeyCode.E),"Keyboard E reaches game input backend");
            foreach(var interaction in Object.FindObjectsOfType<LootInteraction>())interaction.SendMessage("Update");
            yield return null;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();yield return null;
        }
        static void Move(Character player,Vector2 point){player.GetComponent<Rigidbody2D>().position=point;player.transform.position=point;Physics2D.SyncTransforms();}
        static IEnumerator CheckAll()
        {
            var prefs=GamePreferences.Current.Copy();prefs.reducedMotion=false;prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);Application.runInBackground=true;
            yield return Settle();
            var config=ApothecaryUI.Instance.Config;
            EventSystem.current.GetComponent<InputSystemUIInputModule>().enabled=false;
            for(int n=0;n<config.characters.Length;n++)
            {
                var ui=ApothecaryUI.Instance;ui.Show("prepare");yield return Settle();Click("Choice 0 "+n);yield return Settle();
                var c=config.characters[n];Check(ui.SelectedCharacter==c,"Profile selects "+c.name);
                for(int j=0;j<config.characters.Length;j++)Check(Named("Choice 0 "+j).GetComponentsInChildren<Image>().Any(i=>i.sprite==config.characters[j].profileSprite),"Original bust profile "+config.characters[j].name);
                Check(!ui.GetComponentsInChildren<Button>().Any(b=>b.name=="Button ‹"&&((RectTransform)b.transform).anchorMin.x<.45f),"No left character arrows");
                Check(ui.GetComponentsInChildren<PreparationSkillPreview>().Single().character==c,"Skill motion follows selected character");
                var frames=ui.GetComponentsInChildren<IdentityShape>().Where(f=>f.name=="Identity skill frame").ToArray();
                Check(frames.Length==2&&frames.All(f=>Mathf.Abs(f.rectTransform.rect.width-f.rectTransform.rect.height)<.1f&&f.color==PreparationIdentity.ColorFor(c)),"Matching square skill frames "+c.name);
                foreach(string title in new[]{"기본 능력치","무기","유물","아이템 1","아이템 2"})Check(ui.GetComponentsInChildren<IdentityShape>().Any(f=>f.name=="Category "+title&&f.color==PreparationIdentity.ColorFor(c)),"Colored category "+title);
                Check(!ui.GetComponentsInChildren<Button>().Any(b=>b.name.EndsWith(" · 정보")),"Inline description has no information button");
                Check(StartingNeedleSelection.Selected(config,c)!=null&&(int)StartingNeedleSelection.Selected(config,c).Type==StartingNeedleSelection.DefaultId(c),"Default weapon "+c.name);
                Capture("prepare-"+OctoberArt.CharacterKey(c));
                if(n==0)
                {
                    Click("Button 무기");yield return Settle();
                    var iceTile=ui.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Choice 1 ")&&b.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text=="빙결침"));
                    Click(iceTile.name);yield return Settle();Capture("prepare-weapons");
                    Check(!ui.GetComponentsInChildren<TextMeshProUGUI>().First(t=>t.name=="Selection description").isTextOverflowing,"Long ice-needle description fits inline");
                    Click("Button 유물");yield return Settle();Capture("prepare-relics");
                    Click("Button 아이템");yield return Settle();Capture("prepare-items");
                    // Cross-character weapon choice persists independently.
                    var ice=config.weapons.First(w=>w.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle);
                    StartingNeedleSelection.Set(ice,c);Check(StartingNeedleSelection.Selected(config,c)==ice,"Cross-character weapon selectable");
                    Check(StartingNeedleSelection.Selected(config,config.characters[1])!=ice,"Other character keeps its own loadout");PlayerPrefs.DeleteKey(StartingNeedleSelection.SaveKey(c));
                }
                Click("Button 출전하기");yield return new WaitForSecondsRealtime(2);
                yield return Settle();ui=ApothecaryUI.Instance;var level=Object.FindObjectOfType<LevelManager>();var player=level.PlayerCharacter;
                player.AddMaxHealthBonus(10000);player.GainHealth(10000);
                var manager=Object.FindObjectOfType<AbilityManager>();
                var parents=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
                Check(parents.Single(w=>(int)w.Type==StartingNeedleSelection.DefaultId(c)).Owned,"Level1 starts with selected default "+c.name);
                Check(ui.GetComponentsInChildren<IdentityShape>().Count(f=>f.name=="Identity skill frame"&&f.color==PreparationIdentity.ColorFor(c))==2,"Level1 uses same character skill frames");
                Capture("hud-"+OctoberArt.CharacterKey(c));
                if(n==0)yield return FieldChecks(config,level,player,manager,parents);
                Time.timeScale=1;SceneManager.LoadScene("Main Menu");yield return new WaitForSecondsRealtime(1.4f);
            }
            // A manually chosen cross-character weapon must replace, not stack with, the default.
            var hyuki=config.characters.First(c=>OctoberArt.CharacterKey(c)=="Hyuki");
            var wind=config.weapons.First(w=>w.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.WindNeedle);
            StartingNeedleSelection.Set(wind,hyuki);ApothecaryUI.Instance.Show("prepare");yield return Settle();
            Click("Choice 0 "+Array.IndexOf(config.characters,hyuki));yield return Settle();Click("Button 출전하기");yield return new WaitForSecondsRealtime(2);
            var finalParents=Object.FindObjectOfType<AbilityManager>().GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
            Check(finalParents.Count(w=>w.Owned)==1&&finalParents.Single(w=>w.Owned).Type==wind.Type,"Cross-character choice replaces innate default without duplicate grants");
            var finalPlayer=Object.FindObjectOfType<LevelManager>().PlayerCharacter;finalPlayer.AddMaxHealthBonus(10000);finalPlayer.GainHealth(10000);
            var terminal=Object.FindObjectOfType<FinalBossSummonInteractable>();Check(terminal.TryAutomaticSummon(),"New altar can summon boss");
            yield return new WaitForSecondsRealtime(5);Check(Object.FindObjectOfType<SnailBossRuntime>()!=null,"Roll-cake boss arrives after altar animation");Capture("boss-summoned");
            SessionState.SetBool(Key+"Done",true);
        }
        static IEnumerator FieldChecks(ApothecaryUIConfig config,LevelManager level,Character player,AbilityManager manager,SyringeSpecialAugmentAbility[] parents)
        {
            var locked=parents.First(w=>w.Type==SyringeSpecialAugmentAbility.SpecialAugmentType.Poison);
            Check(!StartingNeedleSelection.Owned(locked)&&!locked.RequirementsMet(),"Locked needle cannot be acquired");
            LobbyUnlockSave.Unlock("Needle",locked.Type.ToString());Check(locked.RequirementsMet(),"Explicit unlock enables acquisition");
            locked.Select();PlayerPrefs.DeleteKey("LobbyUnlock.Needle."+locked.Type);
            var runtime=manager.GetComponentInChildren<Ver4AugmentRuntime>(true);
            for(int roll=0;roll<50;roll++)
            {
                var offers=runtime.CreateOffers(false,3).Cast<Ver4AugmentOffer>().ToArray();
                Check(offers.Length>0&&offers.All(o=>o.Parent==null||StartingNeedleSelection.Owned(o.Parent)),"Locked owned needle excluded from roll "+roll);
                foreach(var o in offers)Object.Destroy(o.gameObject);
            }
            var owner=config.characters.First(c=>StartingNeedleSelection.DefaultId(c)==18);
            PlayerPrefs.SetInt("LobbyUnlock.Character."+owner.name,0);
            Check(!StartingNeedleSelection.Owned(parents.First(w=>(int)w.Type==18)),"Starter unlock follows character ownership");
            PlayerPrefs.DeleteKey("LobbyUnlock.Character."+owner.name);
            var scroll=ApothecaryUI.Instance.GetComponentInChildren<PrescriptionScrollView>();scroll.Toggle.onClick.Invoke();yield return Settle();
            Check(scroll.Expanded&&scroll.Reveal>.99f&&scroll.Rows.Length==3,"Capsule prescription unfolds all three quests");Capture("prescription-open");
            scroll.Toggle.onClick.Invoke();yield return Settle();Check(!scroll.Expanded&&scroll.Reveal==0,"Capsule prescription folds closed");
            Check(SnailBossDebugSpawn.SpawnFieldMinis(level)==2,"Field wave spawns two instead of three");
            yield return Settle();
            var snail=Object.FindObjectsOfType<AcidLeechMonster>().First();var shadow=snail.transform.Find("Shadow");
            Check(shadow!=null&&Mathf.Abs(shadow.localPosition.y+.025f)<.001f,"Mini-snail shadow hugs the feet");
            Move(player,snail.transform.position+Vector3.down*2);yield return Settle();Capture("snail-shadow");
            var altar=Object.FindObjectOfType<FinalBossSummonInteractable>();
            Check(altar!=null,"Boss altar spawned");
            var solid=altar.transform.Find("Roll cake altar solid base");
            Check(solid!=null&&solid.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Static&&!solid.GetComponent<Collider2D>().isTrigger,"Altar has static solid collision");
            Check(altar.GetComponent<MapMarker>().Icon==OctoberArt.Get("OctoberUI/BossAltarIcon"),"Altar minimap icon assigned");
            Move(player,(Vector2)solid.position+new Vector2(-2,.3f));yield return Settle();
            var body=player.GetComponent<Rigidbody2D>();var target=body.position+Vector2.right*4;
            Check(BloodClotObstacle.ClampDash(body,target).x<solid.position.x,"Dash sweep cannot cross altar");Capture("boss-altar");
            // Move the NPC and a fresh chest into a clear space, then drive actual E-key events.
            Vector2 area=(Vector2)player.transform.position+Vector2.down*8;Move(player,area);
            var npc=Object.FindObjectOfType<MerchantNPC>();
            if(npc==null)npc=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/hyeon/prepabs/Merchant.prefab")).GetComponent<MerchantNPC>();
            Check(npc!=null,"Merchant prefab available");npc.transform.position=area+Vector2.right*.85f;
            Physics2D.SyncTransforms();yield return Settle();
            Check(ApothecaryUI.Instance.Page=="hud"&&Time.timeScale>0,"Touching merchant no longer opens shop");
            Check(npc.GetComponent<PixelInteractionPrompt>()!=null,"Merchant uses existing animated E prompt");Capture("merchant-prompt");
            yield return E();yield return Settle();Check(ApothecaryUI.Instance.Page=="merchant"&&Time.timeScale==0,"E opens merchant");
            MerchantUIManager.Instance.CloseShop();yield return Settle();Check(Time.timeScale>0,"Merchant close resumes run");
            npc.transform.position=area+Vector2.right*5;Physics2D.SyncTransforms();yield return Settle();
            int opened=0;bool sharedInputBlocked=false;
            Action<Chest> observed=c=>{opened++;sharedInputBlocked=!GameInput.TryConsumeInteraction(KeyCode.E);};Chest.OnAnyChestOpened+=observed;
            var chest=level.EntityManager.SpawnChest(level.CurrentLevelBlueprint.chestBlueprint,area+Vector2.right*.8f);
            yield return new WaitForSecondsRealtime(.9f);Check(opened==0&&chest.CanInteract,"Chest waits for E after approach");
            Check(chest.GetComponent<MapMarker>().Icon==OctoberArt.Get("OctoberUI/ItemChestIcon"),"Chest minimap icon replaces red square");
            Check(chest.GetComponent<PixelInteractionPrompt>()!=null,"Chest uses existing animated E prompt");Capture("chest-prompt");
            yield return E();yield return new WaitForSecondsRealtime(.4f);Check(opened==1,"E opens chest exactly once");
            Check(sharedInputBlocked,"Chest callback cannot reuse E for an adjacent return portal");
            yield return E();Check(opened==1,"Repeated E cannot duplicate chest reward");Chest.OnAnyChestOpened-=observed;
            Time.timeScale=1;
        }
    }
    public sealed class PreparationRevisionRunner:MonoBehaviour { }
}
