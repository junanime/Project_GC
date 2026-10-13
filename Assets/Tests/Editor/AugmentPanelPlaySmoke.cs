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
    public static class AugmentPanelPlaySmoke
    {
        const string Key="AugmentPanelSmoke";
        static bool started;
        static int checks;
        static double deadline;
        static AugmentPanelPlaySmoke() { if(SessionState.GetBool(Key,false)) Attach(); }
        public static void Run()
        {
            SessionState.SetBool(Key,true); SessionState.SetBool(Key+"Done",false); SessionState.SetBool(Key+"Failed",false);
            EditorSceneManager.OpenScene("Assets/Scenes/Game/Level 1.unity");
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            Attach(); EditorApplication.EnterPlaymode();
        }
        public static void RefreshAndRun(){Vampire.EditorTools.TrainingUIInstaller.Install();Run();}
        static void Attach()
        {
            deadline=EditorApplication.timeSinceStartup+240; started=false;checks=0;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            Application.logMessageReceived-=Log; Application.logMessageReceived+=Log;
        }
        static void Log(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)SessionState.SetBool(Key+"Failed",true); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>deadline)SessionState.SetBool(Key+"Failed",true);
            if(SessionState.GetBool(Key+"Failed",false)||SessionState.GetBool(Key+"Done",false))
            {
                if(EditorApplication.isPlaying){Time.timeScale=1;EditorApplication.ExitPlaymode();return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                bool failed=SessionState.GetBool(Key+"Failed",false);SessionState.SetBool(Key,false);
                Debug.Log("[AugmentPanelSmoke] FINISHED failed="+failed+" checks="+checks);EditorApplication.Exit(failed?1:0);return;
            }
            var level=Object.FindObjectOfType<LevelManager>();
            if(!EditorApplication.isPlaying||started||level==null||level.PlayerCharacter==null||level.CurrentLevelTime<1)return;
            started=true;level.StartCoroutine(Checks(level));
        }
        static void Check(bool valid,string message)
        { if(!valid)throw new Exception("[AugmentPanelSmoke] FAIL "+message); checks++;Debug.Log("[AugmentPanelSmoke] PASS "+message); }
        static object Get(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        static string AcquisitionDescription(Ability source)
        {
            var owned=typeof(Ability).GetField("owned",BindingFlags.Instance|BindingFlags.NonPublic);
            bool before=(bool)owned.GetValue(source);
            try { owned.SetValue(source,false);return source.Description; }
            finally { owned.SetValue(source,before); }
        }
        static void Capture(string name)
        {
            Directory.CreateDirectory(Path.GetFullPath("../work"));
            typeof(Ver4PlaySmoke).GetMethod("CaptureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"augment-panel-"+name+".png"});
        }
        static void TextFits(AbilityCard card,string context)
        {
            foreach(string field in new[]{"nameText","descriptionText"})
            {
                var text=(TextMeshProUGUI)Get(card,field); text.ForceMeshUpdate();
                Check(text.textBounds.size.y<=text.rectTransform.rect.height+2 && text.textBounds.size.x<=text.rectTransform.rect.width+2,
                    context+" "+field+" fits ("+text.textBounds.size+" / "+text.rectTransform.rect.size+")");
            }
        }
        static void TrainingTextFits(AbilityCard card,string context)
        {
            TextFits(card,context);
            if(card.UsesNoblePanel)return;
            foreach(var field in new[]{"effectLabel","progressLabel"})
            {
                var text=(TextMeshProUGUI)Get(card,field);text.ForceMeshUpdate();
                Check(text.textBounds.size.y<=text.rectTransform.rect.height+2&&text.textBounds.size.x<=text.rectTransform.rect.width+2,context+" "+field+" fits");
            }
        }
        static Rect ScreenRect(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var canvas=rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            Vector2 min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]),max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static IEnumerator Checks(LevelManager level)
        {
            var prefs=GamePreferences.Current.Copy();prefs.pauseOnFocusLoss=false;prefs.muteOnFocusLoss=false;GamePreferences.Apply(prefs,false);
            level.PlayerCharacter.AddMaxHealthBonus(10000);level.PlayerCharacter.GainHealth(10000);level.SetRunFlowPaused(true);
            var camera=Object.FindObjectOfType<CharacterCamera>().GetComponent<Camera>();
            float zoomed=camera.orthographicSize;
            Check(Mathf.Abs(zoomed*camera.aspect-6.25f)<.001f,"Level1 serialized camera half-width is 6.25");
            Vector3 point=level.PlayerCharacter.transform.position;
            float after=(camera.WorldToScreenPoint(point+Vector3.up)-camera.WorldToScreenPoint(point)).magnitude;
            camera.orthographicSize=5/camera.aspect;
            float before=(camera.WorldToScreenPoint(point+Vector3.up)-camera.WorldToScreenPoint(point)).magnitude;
            Capture("camera-before");
            camera.orthographicSize=zoomed;
            Check(Mathf.Abs(after/before-.8f)<.001f,"World sprite projected size is 80 percent of previous camera");
            yield return null;Capture("camera-after");
            var prescription=Object.FindObjectOfType<PrescriptionScrollView>();
            Check(prescription!=null&&Vector3.Distance(prescription.Dock.localScale,Vector3.one*.7f)<.001f,"Prescription retains uniform 70 percent scale");
            var dockRect=ScreenRect(prescription.Dock);
            Check(dockRect.xMin>=0&&dockRect.xMax<=Screen.width+1&&dockRect.yMax<Screen.height-35,"Smaller capsule remains below HUD inside screen");
            var capsuleHits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=dockRect.center},capsuleHits);
            Check(capsuleHits.Any(h=>h.gameObject.GetComponentInParent<Button>()==prescription.Toggle),"Scaled capsule still receives pointer interaction");
            if(prescription.Expanded)prescription.Toggle.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);Capture("prescription-closed");
            prescription.Toggle.onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
            Check(prescription.Expanded&&prescription.Reveal==1,"Scaled capsule unfolds fully");
            Check(prescription.Rows.All(r=>r.gameObject.activeInHierarchy),"Expanded capsule keeps all three quests visible");
            Capture("prescription-open");prescription.Toggle.onClick.Invoke();
            var dialog=level.EntityManager.AbilitySelectionDialog;
            var manager=Object.FindObjectOfType<AbilityManager>();
            if(dialog.MenuOpen)dialog.Close(); dialog.Open(false);
            yield return new WaitForSecondsRealtime(1);
            var cards=dialog.GetComponentsInChildren<AbilityCard>();
            Check(cards.Length==3&&Time.timeScale==0,"Three cards pause gameplay");
            Check(dialog.GetComponentsInChildren<Image>().Any(i=>i.name=="Training banner"&&i.sprite==OctoberArt.Get("TrainingUI/TrainingBanner")),"Approved title, ribbon, leaves and subtitle image installed");
            Check(!dialog.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.name=="Training title"||t.name=="Training subtitle"),"No duplicate text over approved banner");
            var hud=Object.FindObjectsOfType<RectTransform>().Where(r=>r.name.StartsWith("Training HUD")).ToArray();
            Check(hud.Length==3,"Level, herb leaf and coin HUD badges installed");
            Check(hud.SelectMany(r=>r.GetComponentsInChildren<TextMeshProUGUI>()).All(t=>t.font==TrainingUITheme.Font),"Rounded font on compact HUD");
            foreach(var card in cards)TrainingTextFits(card,"Acquisition");
            Check(cards.All(c=>((TextMeshProUGUI)Get(c,"descriptionText")).font==TrainingUITheme.Font),"Async localization preserves rounded card font");
            Capture("acquisition");
            var reroll=(Button)Get(dialog,"rerollButton");reroll.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1);
            Check(!reroll.interactable,"Reroll is used once");
            var rerolled=((List<Ability>)Get(dialog,"displayedAbilities")).ToArray();
            reroll.onClick.Invoke();
            Check(rerolled.SequenceEqual((List<Ability>)Get(dialog,"displayedAbilities")),"Second reroll cannot change offers");
            var source=((Ver4AugmentOffer)rerolled[0]).Source;
            cards[0].transform.Find("Background Button").GetComponent<Button>().onClick.Invoke();
            Check(source.Owned&&!dialog.MenuOpen&&Time.timeScale==1,"Whole-card click awards reward and unpauses");
            cards[0].Selected();Check(!dialog.MenuOpen,"Stale click after close is ignored");
            var parents=manager.GetComponentsInChildren<SyringeSpecialAugmentAbility>(true);
            foreach(var parent in parents)if(!parent.Owned)manager.AcquireVer4Ability(parent);
            dialog.Open(false);yield return new WaitForSecondsRealtime(1);
            Check(reroll.interactable,"Reopening resets reroll allowance");
            var examples=new List<Ver4AugmentOffer>();
            var grades=new[]{AugmentUpgradeGrade.Common,AugmentUpgradeGrade.Rare,AugmentUpgradeGrade.Epic,AugmentUpgradeGrade.Legendary,AugmentUpgradeGrade.Supreme,AugmentUpgradeGrade.Original};
            for(int i=0;i<grades.Length;i++)
            {
                var offer=new GameObject("Theme test preview").AddComponent<Ver4AugmentOffer>();examples.Add(offer);
                offer.Configure(manager.Ver4,grades[i]==AugmentUpgradeGrade.Original?Ver4RewardKind.Original:Ver4RewardKind.Numeric,grades[i],parents[i],0,1,
                    Ver4AugmentCatalog.ParentNames[(int)parents[i].Type]+" · 수치 강화","침의 피해량이 증가합니다\n공격력 +12%");
            }
            for(int start=0;start<2;start++)
            {
                for(int i=0;i<3;i++)cards[i].Init(dialog,examples[start*3+i],0);
                yield return new WaitForSecondsRealtime(.7f);
                foreach(var card in cards)
                {
                    TextFits(card,"Theme gallery");
                    var offer=(Ver4AugmentOffer)Get(card,"ability");
                    var surface=(IdentityShape)Get(card,"trainingFrame");
                    Check(surface.insetColor==new Color(1,.975f,.90f),"Same cream background across every grade");
                    Check(surface.color==AugmentPanelTheme.Accent(offer.Grade),"Glossy border matches "+offer.Grade);
                }
                Capture(start==0?"common-rare-epic":"legendary-supreme-original");
            }
            // Every actual original description, including progress, must remain readable.
            foreach(var parent in parents)
            {
                examples[0].Configure(manager.Ver4,Ver4RewardKind.NewSpecial,AugmentUpgradeGrade.Common,parent,0,0,
                    "특수 증강: "+Ver4AugmentCatalog.ParentNames[(int)parent.Type],AcquisitionDescription(parent)+"\n최초 획득 · 오리지널 Lv.0/9");
                cards[0].Init(dialog,examples[0],0);Canvas.ForceUpdateCanvases();TrainingTextFits(cards[0],parent.Type+" acquisition");
            }
            foreach(var parent in parents)for(int option=0;option<3;option++)
            {
                int index=(int)parent.Type*3+option;
                examples[0].Configure(manager.Ver4,Ver4RewardKind.Original,AugmentUpgradeGrade.Original,parent,option,0,
                    Ver4AugmentCatalog.ParentNames[(int)parent.Type]+" · 오리지널 강화",
                    Ver4AugmentCatalog.OriginalNames[index]+"\n"+Ver4AugmentCatalog.OriginalDescriptions[index]+"\n이 항목 2 → 3/3\n오리지널 Lv.8 → 9/9");
                cards[0].Init(dialog,examples[0],0);Canvas.ForceUpdateCanvases();TrainingTextFits(cards[0],parent.Type+" original "+option);
            }
            for(int page=0;page<(parents.Length+2)/3;page++)
            {
                for(int i=0;i<3;i++)
                {
                    var parent=parents[Mathf.Min(page*3+i,parents.Length-1)];
                    examples[i].Configure(manager.Ver4,Ver4RewardKind.NewSpecial,AugmentUpgradeGrade.Common,parent,0,0,"특수 증강: "+Ver4AugmentCatalog.ParentNames[(int)parent.Type],AcquisitionDescription(parent)+"\n최초 획득 · 오리지널 Lv.0/9");
                    cards[i].Init(dialog,examples[i],0);
                }
                yield return new WaitForSecondsRealtime(.7f);Capture("weapon-gallery-"+page);
            }
            for(int i=0;i<3;i++)
            {
                var parent=parents.Single(p=>p.Type==(i==1?SyringeSpecialAugmentAbility.SpecialAugmentType.DigestiveAcidSacNeedle:SyringeSpecialAugmentAbility.SpecialAugmentType.IceNeedle));
                int index=(int)parent.Type*3+(i==2?0:1);
                examples[i].Configure(manager.Ver4,i==1?Ver4RewardKind.NewSpecial:Ver4RewardKind.Original,i==1?AugmentUpgradeGrade.Epic:AugmentUpgradeGrade.Original,parent,i==2?0:1,0,Ver4AugmentCatalog.ParentNames[(int)parent.Type],
                    i==1?AcquisitionDescription(parent)+"\n최초 획득 · 오리지널 Lv.0/9":Ver4AugmentCatalog.OriginalNames[index]+"\n"+Ver4AugmentCatalog.OriginalDescriptions[index]+"\n이 항목 0 → 1/3\n오리지널 Lv.0 → 1/9");
                cards[i].Init(dialog,examples[i],0);
            }
            yield return new WaitForSecondsRealtime(.7f);Capture("approved-composition");
            for(int i=0;i<3;i++)cards[i].Init(dialog,examples[i+2],0);
            yield return new WaitForSecondsRealtime(.7f);
            foreach(var size in new[]{new Vector2(1280,741),new Vector2(1024,789),new Vector2(1560,741)})
            {
                EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(Vector2.zero,size);
                yield return new WaitForSecondsRealtime(.7f);Canvas.ForceUpdateCanvases();
                Rect previous=default;
                foreach(var card in cards)
                {
                    var rect=ScreenRect((RectTransform)card.transform);
                    Check(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<=Screen.width+1&&rect.yMax<=Screen.height+1,"Card inside viewport "+Screen.width+"x"+Screen.height);
                    Check(previous==default||rect.xMin>previous.xMax,"Three cards do not overlap");previous=rect;
                    var data=new PointerEventData(EventSystem.current){position=rect.center};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                    Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<AbilityCard>()==card,"Card center receives pointer/touch raycast");
                }
                Check(ScreenRect((RectTransform)reroll.transform).yMax<ScreenRect((RectTransform)cards[0].transform).yMin,"Reroll below cards");
                Capture(Screen.width+"x"+Screen.height);
            }
            // Capture a real, selectable mixed-grade reward from the production offer pool.
            var mixed=new List<Ability>();
            foreach(var wanted in new[]{AugmentUpgradeGrade.Rare,AugmentUpgradeGrade.Epic,AugmentUpgradeGrade.Original})
            {
                for(int attempt=0;attempt<200;attempt++)
                {
                    var pool=manager.SelectAbilities();
                    var selected=pool.OfType<Ver4AugmentOffer>().FirstOrDefault(a=>a.Grade==wanted);
                    if(selected!=null){mixed.Add(selected);pool.Remove(selected);}
                    manager.ReturnAbilities(pool); if(selected!=null)break;
                }
            }
            Check(mixed.Count==3,"Production pool supplies rare, epic and original rewards");
            manager.ReturnAbilities((List<Ability>)Get(dialog,"displayedAbilities"));
            typeof(AbilitySelectionDialog).GetField("displayedAbilities",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(dialog,mixed);
            typeof(AbilitySelectionDialog).GetMethod("Populate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{mixed});
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(0,0,1280,741);
            yield return new WaitForSecondsRealtime(1);Capture("actual-rewards");
            var nobles=manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Where(a=>a.AvailableAsNoble).OrderBy(a=>(int)a.Type).ToArray();
            Check(nobles.Length==11,"Eleven current Noble rewards");
            var poison=manager.GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Single(a=>a.Type==SyringeLegendaryAugmentAbility.LegendaryAugmentType.PoisonContagion);
            Check(!poison.RequirementsMet(),"Poison contagion never enters any current offer pool");
            for(int trial=0;trial<40;trial++)
            {
                var pool=manager.Ver4.CreateOffers(true,3);
                Check(pool.All(a=>((Ver4AugmentOffer)a).Source!=poison),"Noble draw excludes postponed poison "+trial);
                manager.ReturnAbilities(pool);
            }
            foreach(var legendSourceTest in nobles)
            {
                examples[0].Configure(manager.Ver4,Ver4RewardKind.LegendaryAbility,AugmentUpgradeGrade.Legendary,legendSourceTest,0,0,
                    "고귀 증강: "+legendSourceTest.Name,legendSourceTest.Description+"\n비전서 보상 · 강화 불가");
                cards[0].Init(dialog,examples[0],0);Canvas.ForceUpdateCanvases();TextFits(cards[0],legendSourceTest.Name+" legend");
                var title=(TextMeshProUGUI)Get(cards[0],"nameText");
                Check(cards[0].UsesNoblePanel&&((Image)Get(cards[0],"cardBackgroundImage")).sprite.name==legendSourceTest.Type.ToString(),"Approved artwork matches "+legendSourceTest.Type);
                Check(!title.enableAutoSizing&&title.fontSize==24&&title.rectTransform.anchorMin==new Vector2(.215f,.345f),"Uniform Noble title geometry "+legendSourceTest.Type);
                Check(title.text==legendSourceTest.Name.Trim()&&!((Image)Get(cards[0],"gradeBorder")).gameObject.activeSelf,"No Noble grade text or plaque "+legendSourceTest.Type);
                Check(!((Image)Get(cards[0],"abilityImage")).enabled,"No duplicate icon layered over overframe "+legendSourceTest.Type);
            }
            for(int page=0;page<4;page++)
            {
                for(int i=0;i<3;i++)
                {
                    var current=nobles[Mathf.Min(page*3+i,nobles.Length-1)];
                    examples[i].Configure(manager.Ver4,Ver4RewardKind.LegendaryAbility,AugmentUpgradeGrade.Legendary,current,0,0,"고귀 증강: "+current.Name,current.Description+"\n비전서 보상 · 강화 불가");
                    cards[i].Init(dialog,examples[i],0);
                }
                yield return new WaitForSecondsRealtime(.7f);Capture("noble-gallery-"+page);
            }
            foreach(var size in new[]{new Vector2(1024,789),new Vector2(1560,741),new Vector2(1280,741)})
            {
                EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).position=new Rect(Vector2.zero,size);
                yield return new WaitForSecondsRealtime(.5f);Canvas.ForceUpdateCanvases();
                Rect previous=default;
                foreach(var card in cards)
                {
                    var rect=ScreenRect((RectTransform)card.transform);
                    Check(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<=Screen.width+1&&rect.yMax<=Screen.height+1,"Full overframe inside viewport "+Screen.width);
                    Check(previous==default||rect.xMin>previous.xMax,"Noble effects never overlap adjacent card");previous=rect;
                    TextFits(card,"Noble resolution "+Screen.width);
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=rect.center},hits);
                    Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<AbilityCard>()==card,"Noble whole-card click target");
                }
                Capture("noble-"+Screen.width);
            }
            dialog.Close();foreach(var offer in examples)Object.Destroy(offer.gameObject);
            dialog.OpenLegendary();yield return new WaitForSecondsRealtime(1);
            Check(dialog.MenuOpen&&!reroll.gameObject.activeSelf,"Legendary rewards still open without reroll");
            foreach(var card in cards)TextFits(card,"Legendary");Capture("legendary-existing-frame");
            var legend=(Ver4AugmentOffer)((List<Ability>)Get(dialog,"displayedAbilities"))[0];var legendSource=legend.Source;
            cards[0].Selected();yield return new WaitForSecondsRealtime(1);
            cards[0].Selected();Check(legendSource.Owned&&!dialog.MenuOpen,"Legendary reward still selectable");
            dialog.Open(false);yield return new WaitForSecondsRealtime(.7f);
            Check(cards.All(c=>!c.UsesNoblePanel&&((Image)Get(c,"abilityImage")).enabled),"Reused Noble cards restore ordinary icons");
            dialog.Close();
            SessionState.SetBool(Key+"Done",true);
        }
    }
}
