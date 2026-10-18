using System;
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
        Button prescriptionClaim, overchargeButton;
        Action skillRewardCallback;
        bool skillRewardQueued;
        PrescriptionRuntime Prescription => level!=null && level.PlayerCharacter!=null?level.PlayerCharacter.GetComponent<PrescriptionRuntime>():null;
        void BuildPrescriptionHud()
        {
            prescriptionHud=Rect("Prescription HUD",content,0,0,1,1);
            prescriptionScroll=AugmentPanelTheme.Rect("Vision scroll dock",prescriptionHud).gameObject.AddComponent<PrescriptionScrollView>();
            prescriptionScroll.Build(runtimeFont!=null?runtimeFont:Config.font,()=>{
                prescriptionExpanded=!prescriptionExpanded;prescriptionScroll.SetExpanded(prescriptionExpanded);
            },()=>Prescription?.TryClaim(FindObjectOfType<EntityManager>()?.AbilitySelectionDialog),prescriptionExpanded,level?.PlayerCharacter?.Blueprint??character);
            prescriptionTitle=prescriptionScroll.Title;
            for(int i=0;i<3;i++)prescriptionRows[i]=prescriptionScroll.Rows[i];
            prescriptionClaim=prescriptionScroll.Claim;
            overchargeButton=ActionButton(prescriptionHud,"혈전 과충전 / Q",.70f,.14f,.97f,.22f,()=>FindNearbyOvercharge()?.Begin(),true);
            overchargeButton.gameObject.SetActive(false);
        }
        BloodClotOvercharge FindNearbyOvercharge()
        {
            foreach(var charge in FindObjectsOfType<BloodClotOvercharge>())if(charge.PlayerInside)return charge;
            return null;
        }
        void UpdatePrescriptionUI()
        {
            if(Page=="hud")
            {
                bool rewardOpen=level!=null && level.EntityManager!=null && level.EntityManager.AbilitySelectionDialog.MenuOpen;
                if(content!=null)content.gameObject.SetActive(!rewardOpen);
                if(prescriptionHud!=null)prescriptionHud.gameObject.SetActive(!rewardOpen);
                var quest=Prescription;
                if(quest!=null && prescriptionTitle!=null)
                {
                    prescriptionTitle.text=quest.Claimed?"퀘스트  3/3 · 완료":$"퀘스트  {quest.Completed}/3";
                    prescriptionScroll.Tag.text=$"[{PrescriptionRuntime.Tags[quest.Tag]}] 퀘스트";
                    for(int i=0;i<3;i++)if(prescriptionRows[i]!=null)
                    {
                        string line=$"{quest.Description(i)}\n{Mathf.FloorToInt(quest.Progress(i))}/{quest.Target(i):0}";
                        prescriptionRows[i].text=quest.IsComplete(i)?"<s>"+line+"</s>  완료":line;
                        prescriptionRows[i].color=quest.IsComplete(i)?new Color(.35f,.4f,.3f):Ink;
                    }
                    if(prescriptionClaim!=null)
                    {
                        prescriptionClaim.interactable=quest.Ready&&Time.timeScale>0;
                        prescriptionClaim.GetComponentInChildren<TextMeshProUGUI>().text=quest.Claimed?"고귀 증강 획득 완료":quest.Ready?"고귀 증강 선택":"퀘스트 3개 완료 → 고귀 증강";
                    }
                }
                if(overchargeButton!=null)
                {
                    var charge=FindNearbyOvercharge();
                    overchargeButton.gameObject.SetActive(charge!=null&&!charge.Started&&!MiniStageRuntimeState.IsInsideMiniStage);
                    overchargeButton.interactable=Time.timeScale>0;
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
            HudTooltip.Bind(gear.gameObject,()=>"탐험 기록 · 설정",()=>"캐릭터정보와 설정을 확인합니다.\nTAB: 캐릭터정보 / ESC: 설정");

        }
        public bool RequestSkillReward(Action complete)
        {
            if(skillRewardQueued || CurrentSkills==null)return false;
            skillRewardQueued=true;skillRewardCallback=complete;
            return true;
        }
        void BuildSkillReward()
        {
            Label(content,"처방 강화",.25f,.84f,.75f,.94f,34).color=Color.white;
            Label(content,"패시브 · 액티브 · 대쉬 중 하나를 선택하세요",.15f,.75f,.85f,.82f,23).color=Color.white;
            var skills=CurrentSkills;
            if(skills==null)return;
            for(int i=0;i<2;i++)
            {
                bool active=i==1;float x=.055f+i*.30f;
                Panel(content,x,.20f,x+.28f,.72f);
                var d=skills.Definition;int current=active?skills.ActiveLevel:skills.PassiveLevel;int max=active?skills.ActiveMaxLevel:skills.PassiveMaxLevel;
                ImageAt(content,active?d.activeIcon:d.passiveIcon,x+.06f,.49f,x+.22f,.68f);
                Label(content,active?"액티브 강화":"패시브 강화",x+.02f,.425f,x+.26f,.49f,23);
                Label(content,skills.UpgradeDescription(active),x+.025f,.30f,x+.255f,.425f,17);
                bool available=skills.CanUpgrade(active);
                var upgradeButton=ActionButton(content,available?$"Lv.{current} → Lv.{current+1}":current>=max?"최대 레벨":"최대 강화 제한",x+.025f,.22f,x+.255f,.295f,()=>FinishSkillReward(active),true,available);
                if(!available)HudTooltip.Bind(upgradeButton.gameObject,()=>"강화 제한",()=>skills.UpgradeUnavailableReason(active));
            }
            Panel(content,.655f,.20f,.935f,.72f);
            var dash=Rect("Shared dash reward icon",content,.725f,.49f,.865f,.68f).gameObject.AddComponent<SharedDashIcon>();
            dash.color=new Color(.12f,.65f,.63f);dash.raycastTarget=false;
            Label(content,$"대쉬 +1 · {skills.DashRewardUpgrades}/{CharacterSkillRuntime.MaxDashRewardUpgrades}",.675f,.425f,.915f,.49f,23);
            Label(content,"최대 대쉬 횟수 +1\n대쉬 충전 1회 즉시 획득\n한 판에 최대 3회 강화",.68f,.30f,.91f,.425f,17);
            ActionButton(content,skills.CanUpgradeDash?"대쉬 횟수 증가":"최대 강화",.695f,.22f,.895f,.295f,FinishDashReward,true,skills.CanUpgradeDash);
            if(!skills.CanUpgrade(false) && !skills.CanUpgrade(true) && !skills.CanUpgradeDash)
            {
                Label(content,"강화 완료 · 최대 체력의 20% 회복으로 대체",.12f,.12f,.88f,.185f,20).color=Color.white;
                ActionButton(content,"회복하고 계속",.34f,.025f,.66f,.105f,()=>FinishSkillReward(null),true);
            }
            else Label(content,"액티브와 패시브 중 한 스킬만 최대 레벨까지 강화할 수 있습니다.",.12f,.10f,.88f,.17f,17).color=Color.white;
        }
        void FinishSkillReward(bool? active)
        {
            if(!skillRewardQueued || CurrentSkills==null)return;
            if(active.HasValue && !CurrentSkills.TryUpgrade(active.Value))return;
            if(!active.HasValue)level.PlayerCharacter.GainHealth(level.PlayerCharacter.MaxHealth*.2f);
            skillRewardQueued=false;var callback=skillRewardCallback;skillRewardCallback=null;
            CloseRunBook();
            callback?.Invoke();
        }
        void FinishDashReward()
        {
            if(!skillRewardQueued||level==null||level.PlayerCharacter==null)return;
            if(CurrentSkills==null||!CurrentSkills.TryUpgradeDash())return;
            skillRewardQueued=false;
            var callback=skillRewardCallback;skillRewardCallback=null;CloseRunBook();callback?.Invoke();
        }
    }
}
