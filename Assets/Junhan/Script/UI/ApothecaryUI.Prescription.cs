using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public sealed partial class ApothecaryUI
    {
        bool prescriptionExpanded;
        float prescriptionCloseAt;
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
                prescriptionExpanded=!prescriptionExpanded;prescriptionCloseAt=Time.unscaledTime+4f;prescriptionScroll.SetExpanded(prescriptionExpanded);
            },()=>Prescription?.TryClaim(FindObjectOfType<EntityManager>()?.AbilitySelectionDialog),prescriptionExpanded);
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
                if(prescriptionExpanded && Time.unscaledTime>=prescriptionCloseAt)
                {prescriptionExpanded=false;if(prescriptionScroll!=null)prescriptionScroll.SetExpanded(false);}
                bool rewardOpen=level!=null && level.EntityManager!=null && level.EntityManager.AbilitySelectionDialog.MenuOpen;
                if(content!=null)content.gameObject.SetActive(!rewardOpen);
                if(prescriptionHud!=null)prescriptionHud.gameObject.SetActive(!rewardOpen);
                var quest=Prescription;
                if(quest!=null && prescriptionTitle!=null)
                {
                    prescriptionTitle.text=quest.Claimed?"비전서 · 완료":$"비전서  {quest.Completed}/3";
                    prescriptionScroll.Tag.text=$"[{PrescriptionRuntime.Tags[quest.Tag]}] 비전 처방전";
                    for(int i=0;i<3;i++)if(prescriptionRows[i]!=null)
                    {
                        string line=$"{quest.Description(i)}\n{Mathf.FloorToInt(quest.Progress(i))}/{quest.Target(i):0}";
                        prescriptionRows[i].text=quest.IsComplete(i)?"<s>"+line+"</s>  완료":line;
                        prescriptionRows[i].color=quest.IsComplete(i)?new Color(.35f,.4f,.3f):Ink;
                    }
                    if(prescriptionClaim!=null)
                    {
                        prescriptionClaim.interactable=quest.Ready&&Time.timeScale>0;
                        prescriptionClaim.GetComponentInChildren<TextMeshProUGUI>().text=quest.Claimed?"고귀 증강 획득 완료":quest.Ready?"고귀 증강 선택":"처방 3개 완료 → 고귀 증강";
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
            prescriptionScroll.Dock.anchoredPosition=corner-prescriptionHud.rect.max-new Vector2(0,78);
            if(hudNavigation!=null)hudNavigation.anchoredPosition=new Vector2(0,-2-330*prescriptionScroll.Reveal);
            if(skillHudRoot!=null)
            {
                Vector2 bottom=content.InverseTransformPoint(prescriptionSafeCorners[3]);
                skillHudRoot.anchoredPosition=bottom-new Vector2(content.rect.xMax,content.rect.yMin)+new Vector2(-24,48);
            }
        }
        void BuildHudNavigation()
        {
            hudNavigation=Rect("HUD navigation",prescriptionScroll.Dock,0,0,1,0);
            hudNavigation.pivot=new Vector2(.5f,1);hudNavigation.sizeDelta=new Vector2(0,58);
            ActionButton(hudNavigation,"상태",.04f,0,.48f,1,OpenRunBook).name="HUD status";
            ActionButton(hudNavigation,"설정",.52f,0,.96f,1,OpenSettings).name="HUD settings";
        }
        public bool RequestSkillReward(Action complete)
        {
            if(skillRewardQueued || CurrentSkills==null)return false;
            skillRewardQueued=true;skillRewardCallback=complete;
            return true;
        }
        void BuildSkillReward()
        {
            Label(content,"처방 강화",.25f,.84f,.75f,.94f,34);
            Label(content,"패시브 · 액티브 · 대쉬 중 하나를 선택하세요",.15f,.75f,.85f,.82f,23);
            var skills=CurrentSkills;
            if(skills==null)return;
            for(int i=0;i<2;i++)
            {
                bool active=i==1;float x=.055f+i*.30f;
                Panel(content,x,.20f,x+.28f,.72f);
                var d=skills.Definition;int current=active?skills.ActiveLevel:skills.PassiveLevel;
                ImageAt(content,active?d.activeIcon:d.passiveIcon,x+.06f,.49f,x+.22f,.68f);
                Label(content,active?"액티브 강화":"패시브 강화",x+.02f,.425f,x+.26f,.49f,23);
                Label(content,skills.UpgradeDescription(active),x+.025f,.30f,x+.255f,.425f,17);
                ActionButton(content,current>=5?"최대 레벨":$"Lv.{current} → Lv.{current+1}",x+.04f,.22f,x+.24f,.295f,()=>FinishSkillReward(active),true,current<5);
            }
            Panel(content,.655f,.20f,.935f,.72f);
            var dash=Rect("Shared dash reward icon",content,.725f,.49f,.865f,.68f).gameObject.AddComponent<SharedDashIcon>();
            dash.color=new Color(.12f,.65f,.63f);dash.raycastTarget=false;
            Label(content,"대쉬 +1",.675f,.425f,.915f,.49f,25);
            Label(content,"최대 대쉬 횟수 +1\n대쉬 충전 1회 즉시 획득",.68f,.30f,.91f,.425f,18);
            ActionButton(content,"대쉬 횟수 증가",.695f,.22f,.895f,.295f,FinishDashReward,true);
            if(skills.PassiveLevel>=5 && skills.ActiveLevel>=5)
            {
                Label(content,"두 스킬 모두 최대 레벨 · 최대 체력의 20% 회복으로 대체",.12f,.12f,.88f,.185f,20);
                ActionButton(content,"회복하고 계속",.34f,.025f,.66f,.105f,()=>FinishSkillReward(null),true);
            }
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
            skillRewardQueued=false;level.PlayerCharacter.AddDashCharge(1);
            var callback=skillRewardCallback;skillRewardCallback=null;CloseRunBook();callback?.Invoke();
        }
    }
}
