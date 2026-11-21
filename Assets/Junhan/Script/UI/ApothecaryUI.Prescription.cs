using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        bool prescriptionExpanded=true;
        RectTransform prescriptionHud;
        RectTransform hudNavigation;
        PrescriptionScrollView prescriptionScroll;
        readonly Vector3[] prescriptionSafeCorners=new Vector3[4];
        TextMeshProUGUI prescriptionTitle;
        readonly TextMeshProUGUI[] prescriptionRows=new TextMeshProUGUI[3];
        Action skillRewardCallback;
        bool skillRewardQueued;
        string[] analyticsSkillOffers;
        PrescriptionRuntime Prescription => level!=null && level.PlayerCharacter!=null?level.PlayerCharacter.GetComponent<PrescriptionRuntime>():null;
        void BuildPrescriptionHud()
        {
            prescriptionHud=Rect("Prescription HUD",content,0,0,1,1);
            prescriptionScroll=AugmentPanelTheme.Rect("Vision scroll dock",prescriptionHud).gameObject.AddComponent<PrescriptionScrollView>();
            prescriptionScroll.Build(runtimeFont!=null?runtimeFont:Config.font,()=>{
                prescriptionExpanded=!prescriptionExpanded;prescriptionScroll.SetExpanded(prescriptionExpanded);
            },prescriptionExpanded,level?.PlayerCharacter?.Blueprint??character);
            prescriptionTitle=prescriptionScroll.Title;
            for(int i=0;i<3;i++)prescriptionRows[i]=prescriptionScroll.Rows[i];
        }
        void UpdatePrescriptionUI()
        {
            if(Page=="hud")
            {
                bool rewardOpen=level!=null && level.EntityManager!=null && level.EntityManager.AbilitySelectionDialog.MenuOpen;
                var quest=Prescription;
                if (!rewardOpen && !skillRewardQueued && quest != null && !quest.Claimed && quest.Ready &&
                    level != null && !level.IsLevelEnded && !level.IsRunFlowPaused &&
                    !StageEntryLoading.IsLoading && !MiniStageRuntimeState.IsInsideMiniStage &&
                    level.EntityManager != null)
                    rewardOpen = quest.TryClaim(level.EntityManager.AbilitySelectionDialog);
                if(content!=null)content.gameObject.SetActive(!rewardOpen);
                if(prescriptionHud!=null)prescriptionHud.gameObject.SetActive(!rewardOpen);
                if(quest!=null && prescriptionTitle!=null)
                {
                    prescriptionTitle.text=quest.Claimed?"고귀 퀘스트  3/3 · 완료":$"고귀 퀘스트  {quest.Completed}/3";
                    prescriptionScroll.Tag.text=$"고귀 퀘스트 · {PrescriptionRuntime.Tags[quest.Tag]}";
                    for(int i=0;i<3;i++)if(prescriptionRows[i]!=null)
                    {
                        string line=$"{quest.Description(i)}\n{Mathf.FloorToInt(quest.Progress(i))}/{quest.Target(i):0}";
                        prescriptionRows[i].text=quest.IsComplete(i)?"<s>"+line+"</s>  완료":line;
                        prescriptionRows[i].color=quest.IsComplete(i)?new Color(.35f,.4f,.3f):Ink;
                    }
                    prescriptionScroll.RewardStatus.text=quest.Claimed?"고귀 증강 획득 완료":quest.Ready?"고귀 증강 선택 대기 중":"모두 완료하면 고귀 증강 선택";
                }
                if(skillRewardQueued && Time.timeScale>0 && CurrentSkills!=null && !CurrentSkills.IsCutin)
                {
                    var dialog=FindObjectOfType<EntityManager>()?.AbilitySelectionDialog;
                    if(dialog==null || !dialog.MenuOpen)
                    {previousTime=Time.timeScale;ownsPause=true;Time.timeScale=0;Show("skillReward");}
                }
            }
        }
        void LateUpdate()
        {
            if(Page!="hud"||prescriptionScroll==null||safe==null)return;
            safe.GetWorldCorners(prescriptionSafeCorners);
            Vector2 corner=prescriptionHud.InverseTransformPoint(prescriptionSafeCorners[2]);
            prescriptionScroll.Dock.anchoredPosition=corner-prescriptionHud.rect.max-new Vector2(14,78);
            if(hudNavigation!=null)hudNavigation.anchoredPosition=corner-content.rect.max-new Vector2(8,4);
            if(skillHudRoot!=null)
            {
                Vector2 bottom=content.InverseTransformPoint(prescriptionSafeCorners[3]);
                skillHudRoot.anchoredPosition=bottom-new Vector2(content.rect.xMax,content.rect.yMin)+new Vector2(-24,48);
            }
        }
        void BuildHudNavigation()
        {
            hudNavigation=Rect("HUD navigation",content,1,1,1,1);
            hudNavigation.pivot=Vector2.one;hudNavigation.sizeDelta=new Vector2(52,52);
            var gear=ActionButton(hudNavigation,"",0,0,1,1,OpenRunBook);
            gear.name="HUD journal gear";
            var feedback=gear.GetComponent<ApothecaryButtonFeedback>();feedback.useThemeStates=false;feedback.body.enabled=false;
            var face=RecordShape("Gear cream frame",feedback.visual,0,0,1,1,RecordGold,true,25);
            face.insetColor=new Color(1,.94f,.81f);face.transform.SetAsFirstSibling();
            var glyph=Rect("Gear symbol",feedback.visual,.22f,.22f,.78f,.78f).gameObject.AddComponent<BookNavigationGraphic>();
            glyph.gear=true;glyph.color=new Color(.18f,.42f,.39f);glyph.raycastTarget=false;
            HudTooltip.Bind(gear.gameObject,()=>"탐험 기록 · 설정",()=>$"캐릭터정보와 설정을 확인합니다.\n{GameInput.Label(GameKeyAction.Journal)}: 캐릭터정보 / Esc: 설정");

        }
        public bool RequestSkillReward(Action complete)
        {
            if(skillRewardQueued || CurrentSkills==null)return false;
            skillRewardQueued=true;skillRewardCallback=complete;
            var options=new List<string>();
            if(CurrentSkills.CanUpgrade(false))options.Add("passive|패시브 강화|level="+CurrentSkills.PassiveLevel);
            if(CurrentSkills.CanUpgrade(true))options.Add("active|액티브 강화|level="+CurrentSkills.ActiveLevel);
            if(CurrentSkills.CanUpgradeDash)options.Add("dash|대쉬 횟수 증가|level="+CurrentSkills.DashRewardUpgrades);
            if(options.Count==0)options.Add("heal|최대 체력 20% 회복");
            analyticsSkillOffers=options.ToArray();
            GStarAnalytics.Record("bloodclot_reward_offered","skill_reward",character!=null?character.name:"",analyticsSkillOffers);
            return true;
        }
        void BuildSkillReward()
        {
            Label(content,"처방 강화",.25f,.84f,.75f,.94f,34).color=Color.white;
            var skills=CurrentSkills;
            if(skills==null)return;
            if (!skills.CanUpgrade(false) && !skills.CanUpgrade(true) && !skills.CanUpgradeDash)
            {
                Label(content,"강화가 모두 끝났어요. 회복 보상을 받으세요.",.15f,.75f,.85f,.82f,23).color=Color.white;
                var heal = Rect("회복 보상",content,.34f,.20f,.66f,.72f);
                Panel(heal,0,0,1,1);
                ImageAt(heal,level.PlayerInventory.Slots[1].IconImage.sprite,.23f,.45f,.77f,.88f);
                Label(heal,"최대 체력의 20% 회복",.05f,.25f,.95f,.46f,23);
                var button = ActionButton(heal,"회복하고 계속",.08f,.04f,.92f,.2f,()=>FinishSkillReward(null),true);
                NumberedCardChoice.Bind(heal,button,0,false,true);
                return;
            }
            Label(content,"패시브 · 액티브 · 대시 중 하나를 선택하세요",.15f,.75f,.85f,.82f,23).color=Color.white;
            for(int i=0;i<3;i++)
            {
                float x=.055f+i*.30f;
                var card=Rect("처방 보상 "+(i+1),content,x,.20f,x+.28f,.72f);
                Panel(card,0,0,1,1);
                Button button;
                if (i==2)
                {
                    var dash=Rect("Shared dash reward icon",card,.25f,.56f,.75f,.92f).gameObject.AddComponent<SharedDashIcon>();
                    dash.color=new Color(.12f,.65f,.63f);dash.raycastTarget=false;
                    Label(card,$"대시 +1 · {skills.DashRewardUpgrades}/{CharacterSkillRuntime.MaxDashRewardUpgrades}",.05f,.43f,.95f,.56f,23);
                    Label(card,"최대 대시 횟수 +1\n대시 충전 1회 즉시 획득\n한 판에 최대 3회 강화",.07f,.19f,.93f,.43f,17);
                    button=ActionButton(card,skills.CanUpgradeDash?"대시 횟수 증가":"최대 강화",.09f,.04f,.91f,.18f,FinishDashReward,true,skills.CanUpgradeDash);
                }
                else
                {
                    bool active=i==1;
                    var d=skills.Definition;
                    int current=active?skills.ActiveLevel:skills.PassiveLevel;
                    int max=active?skills.ActiveMaxLevel:skills.PassiveMaxLevel;
                    ImageAt(card,active?d.activeIcon:d.passiveIcon,.21f,.56f,.79f,.92f);
                    Label(card,active?"액티브 강화":"패시브 강화",.07f,.43f,.93f,.56f,23);
                    Label(card,skills.UpgradeDescription(active),.09f,.19f,.91f,.43f,17);
                    bool available=skills.CanUpgrade(active);
                    button=ActionButton(card,available?$"Lv.{current} → Lv.{current+1}":current>=max?"최대 레벨":"최대 강화 제한",.09f,.04f,.91f,.18f,()=>FinishSkillReward(active),true,available);
                    if(!available)HudTooltip.Bind(button.gameObject,()=>"강화 제한",()=>skills.UpgradeUnavailableReason(active));
                }
                NumberedCardChoice.Bind(card,button,i,false,true);
            }
            Label(content,"액티브와 패시브 중 한 스킬만 최대 레벨까지 강화할 수 있습니다.",.12f,.10f,.88f,.17f,17).color=Color.white;
        }
        void FinishSkillReward(bool? active)
        {
            if(!skillRewardQueued || CurrentSkills==null)return;
            if(active.HasValue && !CurrentSkills.TryUpgrade(active.Value))return;
            if(!active.HasValue)level.PlayerCharacter.GainHealth(level.PlayerCharacter.MaxHealth*.2f);
            string reward=active.HasValue?(active.Value?"active":"passive"):"heal";
            GStarAnalytics.Record("bloodclot_reward_choice",reward,active.HasValue?
                (active.Value?"액티브 강화|level="+CurrentSkills.ActiveLevel:"패시브 강화|level="+CurrentSkills.PassiveLevel):"최대 체력 20% 회복",analyticsSkillOffers);
            skillRewardQueued=false;var callback=skillRewardCallback;skillRewardCallback=null;
            CloseRunBook();
            callback?.Invoke();
        }
        void FinishDashReward()
        {
            if(!skillRewardQueued||level==null||level.PlayerCharacter==null)return;
            if(CurrentSkills==null||!CurrentSkills.TryUpgradeDash())return;
            GStarAnalytics.Record("bloodclot_reward_choice","dash","대쉬 횟수 증가|level="+CurrentSkills.DashRewardUpgrades,analyticsSkillOffers);
            skillRewardQueued=false;
            var callback=skillRewardCallback;skillRewardCallback=null;CloseRunBook();callback?.Invoke();
        }
    }
}
