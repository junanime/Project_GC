using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    [InitializeOnLoad]
    public static class SupplyResultsSmoke
    {
        const string Key="SupplyResultsSmoke",Output="Library/SupplyResultsProof";
        static double deadline;static bool started;
        [Serializable] class Entry{public string id;}
        [Serializable] class Catalog{public Entry[] entries;}
        [Serializable] class Saved{public string key;public bool exists;public int value;}
        [Serializable] class Backup{public Saved[] values;}
        static SupplyResultsSmoke(){if(SessionState.GetBool(Key,false))Attach();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if(File.Exists(Output+"/errors.txt"))File.Delete(Output+"/errors.txt");
            Vampire.EditorTools.SupplyCapsuleArtImporter.Prepare();
            if(File.Exists(Output+"/prefs.json"))Restore();
            var catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("TutorialClips/catalog").text);
            var keys=catalog.entries.Select(e=>TutorialGuide.SeenKey(e.id)).Concat(new[]{"Coins","LobbySilverCoins"}).Distinct().ToArray();
            File.WriteAllText(Output+"/prefs.json",JsonUtility.ToJson(new Backup{values=keys.Select(k=>new Saved{key=k,exists=PlayerPrefs.HasKey(k),value=PlayerPrefs.GetInt(k)}).ToArray()}));
            foreach(var e in catalog.entries)PlayerPrefs.SetInt(TutorialGuide.SeenKey(e.id),1);
            SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Done",false);SessionState.SetBool(Key+"Failed",false);
            CrossSceneData.CharacterBlueprint=AssetDatabase.LoadAssetAtPath<CharacterBlueprint>("Assets/Blueprints/Characters/Main Character Blueprint.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach();EditorApplication.EnterPlaymode();
        }
        static void Restore()
        {
            foreach(var s in JsonUtility.FromJson<Backup>(File.ReadAllText(Output+"/prefs.json")).values)
            {if(s.exists)PlayerPrefs.SetInt(s.key,s.value);else PlayerPrefs.DeleteKey(s.key);}
            PlayerPrefs.Save();File.Delete(Output+"/prefs.json");
        }
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+360;started=false;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;
        }
        static void Log(string text,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
            {File.AppendAllText(Output+"/errors.txt",text+"\n"+stack+"\n");SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool(Key+"Failed",true);SessionState.SetBool(Key+"Done",true);}
            if(SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Restore();SessionState.SetBool(Key,false);bool failed=SessionState.GetBool(Key+"Failed",false);
                File.WriteAllText(Output+"/result.txt",failed?"FAIL":"PASS");Debug.Log("[SupplyResults] FINISHED failed="+failed);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!started&&EditorApplication.isPlaying&&level!=null&&level.PlayerCharacter!=null&&level.CurrentLevelTime>.1f)
            {started=true;level.StartCoroutine(Checks(level));}
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception("[SupplyResults] FAIL "+message);Debug.Log("[SupplyResults] PASS "+message);}
        static FieldInfo Field(object obj,string name)
        {for(var t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);if(f!=null)return f;}throw new MissingFieldException(name);}
        static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
        static object Call(object o,string name,params object[] args)
        {for(var t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args);}throw new MissingMethodException(name);}
        static Button Button(string text)=>ApothecaryUI.Instance.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>()?.text==text);
        static IEnumerator Capture(string name){yield return null;ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");yield return null;yield return null;}
        static IEnumerator Checks(LevelManager level)
        {
            Application.runInBackground=true;Time.timeScale=1;Time.captureDeltaTime=1f/60;
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            var guide=Object.FindObjectOfType<TutorialGuide>();if(guide!=null){guide.Close();guide.enabled=false;}
            foreach(var b in Object.FindObjectsOfType<MonoBehaviour>())
                if(b is StageEventDirector||b is TimedSpecialMonsterSpawner||b is AcidToadSpawn||b is NPCSpawner){b.enabled=false;b.StopAllCoroutines();}
            level.enabled=false;level.EntityManager.enabled=false;level.EntityManager.StopAllCoroutines();
            foreach(var m in level.EntityManager.LivingMonsters.ToArray())m.gameObject.SetActive(false);
            var player=level.PlayerCharacter;Set(player,"isInvincible",true);
            var manager=Object.FindObjectOfType<AbilityManager>();var needle=SyringeAbilityResolver.FindOwnedOrFirst(manager);needle.enabled=false;needle.StopAllCoroutines();
            var ui=ApothecaryUI.Instance;var stats=Object.FindObjectOfType<StatsManager>();
            Check(SupplyCapsuleArt.Closed!=null&&SupplyCapsuleArt.Parachute!=null,"imported capsule and parachute sprites");
            Check(SupplyCapsuleArt.Closed.rect.width<SupplyCapsuleArt.Closed.texture.width,"alpha padding trimmed via metadata, original PNG preserved");
            // Actual augment buttons, hover and paused reward close.
            var dialog=Object.FindObjectOfType<AbilitySelectionDialog>(true);dialog.Open();yield return null;
            var reroll=dialog.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>()?.text=="새로고침");
            var skip=dialog.GetComponentsInChildren<Button>().First(b=>b.name=="Skip augment");
            Check(Mathf.Abs(((RectTransform)reroll.transform).anchoredPosition.x+((RectTransform)skip.transform).anchoredPosition.x)<.01f,"buttons symmetric around old center");
            var feedback=reroll.GetComponent<ApothecaryButtonFeedback>();var original=feedback.body.sprite;
            feedback.OnPointerEnter(new PointerEventData(EventSystem.current));yield return null;yield return null;
            Check(feedback.body.sprite==original&&feedback.body.color==Color.white&&feedback.IsHighlighted,"hover retains mint art and only enables edge feedback");
            Check(skip.GetComponent<ApothecaryButtonFeedback>().body.sprite!=original,"skip uses distinct coral art");
            reroll.onClick.Invoke();yield return null;Check(!reroll.interactable,"reroll consumed once");
            yield return new WaitForSecondsRealtime(.7f);yield return Capture("01-augment-buttons");
            int historyBefore=AugmentHistoryManager.Instance.Entries.Count;skip.onClick.Invoke();yield return null;
            Check(!dialog.MenuOpen&&Time.timeScale==1&&AugmentHistoryManager.Instance.Entries.Count==historyBefore,"skip resumes without granting an augment");
            // Warning, exact ten active seconds, airborne input, pause, landing and same size.
            var director=level.GetComponent<SupplyCapsuleDirector>();
            Vector3 target=player.transform.position+Vector3.right*1.6f+Vector3.up*.3f;var capsule=director.AnnounceDrop(target);
            Check(!capsule.Landed&&!capsule.GetComponent<Collider2D>().enabled,"warning has no interactable collider");
            yield return new WaitForSeconds(.3f);yield return Capture("02-supply-warning");
            yield return new WaitForSeconds(3.8f);
            var bodyScale=capsule.Visual.localScale;Check(capsule.StartedDescent&&!capsule.Landed,"capsule descending after warning");
            var groundShadow=capsule.GetComponentsInChildren<SpriteRenderer>().First(r=>r.name=="Capsule ground shadow");
            var shadowPosition=groundShadow.transform.position;float airborneShadowAlpha=groundShadow.color.a;
            Check(groundShadow.transform.parent==capsule.transform&&!groundShadow.transform.IsChildOf(capsule.Visual),"landing shadow is independent of airborne capsule");
            Check(!capsule.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Pink landing droplet"),"no landing droplets before ground contact");
            Check(SupplyCapsuleArt.Closed.texture.name=="ClosedClean","capsule uses cleaned art without baked-in dust or ground shadow");
            Check(!(bool)Call(capsule,"ExecuteInteraction",player),"airborne interaction rejected");
            var frozen=capsule.Visual.position;Time.timeScale=0;yield return new WaitForSecondsRealtime(.15f);
            Check(capsule.Visual.position==frozen,"descent freezes during game pause");Time.timeScale=1;
            Directory.CreateDirectory(Output+"/frames");
            for(int frame=0;frame<438;frame++)
            {
                if(frame%6==0)ScreenCapture.CaptureScreenshot(Output+"/frames/drop-"+(frame/6).ToString("D3")+".png");
                if(frame==270)ScreenCapture.CaptureScreenshot(Output+"/03-parachute-descent.png");
                if(frame==357)ScreenCapture.CaptureScreenshot(Output+"/04-landing-splash.png");
                Check(Vector3.Distance(groundShadow.transform.position,shadowPosition)<.001f,"shadow remains on landing ground "+frame);
                if(!capsule.Landed)Check(!capsule.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Pink landing droplet"),"airborne has no landing spray "+frame);
                yield return null;
            }
            Check(capsule.Landed&&capsule.GetComponent<Collider2D>().enabled&&capsule.Visual.localScale==bodyScale,"landing at ten active seconds enables collider without scale swap");
            Check(groundShadow.color.a>airborneShadowAlpha,"shadow becomes denser toward contact");
            player.transform.position=target;player.GetComponent<Rigidbody2D>().position=target;Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.9f);
            Check(capsule.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name=="Pixel E Interaction Prompt"&&r.enabled),"E keycap visible only after landed and player is nearby");
            yield return Capture("05-landed-keycap");
            capsule.TryInteract();yield return new WaitForSeconds(.75f);
            Check(capsule.Opened&&capsule.RewardOrb!=null,"E interaction hinges right half and spawns golden orb");
            yield return Capture("06-hinge-and-orb");
            // Move away for bounce so pickup doesn't occur before its visual completes.
            player.transform.position=target+Vector3.down*3;player.GetComponent<Rigidbody2D>().position=player.transform.position;
            yield return new WaitForSeconds(1.3f);var orb=capsule.RewardOrb;
            Check(orb.Choices.Length==3&&orb.Choices.Distinct().Count()==3,"three distinct not-owned relic offers");
            var choices=orb.Choices.ToArray();
            player.transform.position=orb.transform.position;player.GetComponent<Rigidbody2D>().position=player.transform.position;Physics2D.SyncTransforms();
            for(int frame=0;frame<120&&ui.Page!="supplyRelic";frame++)yield return null;
            Check(ui.Page=="supplyRelic","golden orb automatically collects inside pickup range");
            Check(ui.Page=="supplyRelic"&&Time.timeScale==0,"relic selection pauses combat");
            int previousLevel=player.CurrentLevel;var queuedLevel=(IEnumerator)Call(player,"LevelUpCoroutine");
            Check(queuedLevel.MoveNext()&&player.CurrentLevel==previousLevel,"queued level-up waits for relic selection to close");
            (queuedLevel as IDisposable)?.Dispose();
            ui.GetComponentsInChildren<Button>().First(b=>b.name=="Supply relic choice 1").onClick.Invoke();yield return null;
            Check(ui.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Supply relic description").text==choices[1].Description,"selected relic description displayed on right");
            yield return Capture("07-relic-choice");
            Button("나중에 받기").onClick.Invoke();yield return null;
            Check(ui.Page=="hud"&&Time.timeScale==1&&orb.Choices.SequenceEqual(choices),"cancel resumes with same reward choices");
            Check(orb.TryCollect(),"reward can be reopened without reroll");yield return null;
            var equipped=player.Relics.Equipped;int before=player.Relics.Owned.Count();string saved=RelicSaveData.GetEquippedRelicId();
            Button("유물 획득").onClick.Invoke();yield return null;
            Check(player.Relics.Owned.Count()==before+1&&player.Relics.Equipped==equipped&&RelicSaveData.GetEquippedRelicId()==saved,"reward adds run relic without replacing lobby relic");
            Check(stats.SupplyCapsulesCollected==1&&ui.Page=="hud"&&Time.timeScale==1,"claim counted exactly once and gameplay resumes");
            var state=player.Relics.Capture();player.Relics.Restore(state);Check(player.Relics.Has(choices[0].relicId),"extra relic survives run-state restore");
            Check(!player.Relics.AddReward(choices[0]),"duplicate relic rejected");
            float expected=player.Relics.Owned.Where(r=>r.effectType==choices[0].effectType).Sum(r=>r.effectValue);
            Check(Mathf.Approximately(player.Relics.Bonus(choices[0].effectType),expected),"all owned relic effects contribute");
            ui.OpenRunBook();yield return null;Check(ui.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Record 0 slot ")&&b.interactable)==player.Relics.Owned.Count(),"TAB includes additional relics");ui.CloseRunBook();
            // Data order, aggregation, all icons within bounds, both result palettes.
            var tracker=AugmentDamageTracker.Instance;tracker.ResetDamageData();
            string[] sources={"기본 침","화염침","소화액낭침","빙침","유도침","폭발침","불꽃 토네이도"};
            float[] amounts={42000,30000,24000,18000,3000,2000,1000};
            for(int i=0;i<sources.Length;i++)tracker.RecordDamage(sources[i],amounts[i]);
            var rows=RunResultData.DamageRows(tracker.AugmentDamages);
            Check(rows.Count==5&&rows[4].damage==6000&&rows.Sum(r=>r.damage)==120000,"top four and others preserve all source damage");
            for(int n=1;n<=150;n++)
            {
                var grid=RunResultData.IconGrid(n,243,60);float cell=Mathf.Min(48,243f/grid.x,60f/grid.y);
                Check(grid.x*grid.y>=n&&grid.x*cell<=243.01f&&grid.y*cell<=60.01f,"icon bounds "+n);
            }
            foreach(var item in ui.Config.items)OctoberItemRuntime.Get(player).Give(item);
            for(int i=0;i<36;i++)AugmentHistoryManager.Instance.RecordGeneralAugment("qa/"+i,"강화 "+i,ui.Config.basicNeedle);
            stats.RecordMiniStageClear();stats.RecordHealing(123);
            for(int pass=0;pass<2;pass++)
            {
                ApothecaryUI.TryShowResult(pass==1);yield return null;
                var numbers=ui.GetComponentsInChildren<TMP_Text>().Where(t=>t.name.StartsWith("Result damage ")).ToArray();
                Check(numbers.Length==5&&numbers.All(t=>!t.text.Contains("%")),"result numeric-only contribution "+pass);
                var icons=ui.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Result inventory ")&&i.name.Contains(" icon ")).ToArray();
                Check(icons.Length>=36+player.Relics.Owned.Count(),"result shows inventory icons rather than counts "+pass);
                foreach(var icon in icons)
                {
                    var r=icon.rectTransform;Check(r.anchorMin.x>=0&&r.anchorMin.y>=-.001f&&r.anchorMax.x<=1.001f&&r.anchorMax.y<=1.001f,"icon inside inventory box");
                }
                yield return Capture(pass==1?"09-success":"08-failure");
            }
            var resultCharacter=Field(ui,"character").GetValue(ui) as CharacterBlueprint;
            foreach(var skillName in new[]{"AshiSkills","AriSkills","HyukiSkills","ShiniSkills"})
            {
                var preview=Object.Instantiate(resultCharacter);
                preview.skills=Resources.Load<CharacterSkillDefinition>(skillName);
                Set(ui,"character",preview);Set(ui,"passed",false);ui.Show("result");yield return null;
                var resultImage=Field(ui,"portrait").GetValue(ui) as Image;
                Check(resultImage!=null&&resultImage.sprite==CharacterExitSequence.ResultPortrait(preview),"failure portrait matches nonlethal exit "+skillName);
                yield return Capture("result-exit-"+skillName);
                Object.Destroy(preview);
            }
            Set(ui,"character",resultCharacter);
            File.WriteAllText(Output+"/checks.txt","PASS: symmetric fixed-color buttons, skip and reroll; exact ten-second supply timing, pause, airborne gate, fixed size, landing splash, E keycap, cap hinge, orb bounce, reusable relic panel, cancel/reopen stable choices, additional relic stacking/state persistence/TAB, top-4+other damage aggregation, numeric labels, 150 inventory counts within bounds, success/failure rendering.");
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
